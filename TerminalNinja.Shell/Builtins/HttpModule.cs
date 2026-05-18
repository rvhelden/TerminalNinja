using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using TerminalNinja.Shell.PowerShell;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Builtins;

/// <summary>
/// The <c>http</c> module — make HTTP requests from NinjaShell. Responses come
/// back as records (<c>{ status, status_text, ok, headers, body, url,
/// elapsed_ms }</c>) so they pipe cleanly into <c>obj.dump</c>, <c>select</c>,
/// etc. JSON is parsed via <see cref="JsonToNValue"/> and written via
/// <see cref="NValueToJson"/> — both reflection-free so the module compiles
/// under <c>TreatWarningsAsErrors=true</c> and <c>IsAotCompatible=true</c>. We
/// avoid <c>System.Net.Http.Json</c> extensions on purpose: they use
/// reflection-based <c>JsonSerializer</c> paths that don't survive AOT
/// trimming.
/// </summary>
public static class HttpModule
{
    /// <summary>Register the <c>http</c> module into the default-environment builder.</summary>
    public static void Register(ImmutableDictionary<string, NValue>.Builder b)
    {
        BuiltinRegistry.RegisterModule(b, "http",
            ("get",      new NFunc(args => Verb("http.get",    HttpMethod.Get,    args), -1)),
            ("post",     new NFunc(args => Verb("http.post",   HttpMethod.Post,   args), -1)),
            ("put",      new NFunc(args => Verb("http.put",    HttpMethod.Put,    args), -1)),
            ("patch",    new NFunc(args => Verb("http.patch",  HttpMethod.Patch,  args), -1)),
            ("delete",   new NFunc(args => Verb("http.delete", HttpMethod.Delete, args), -1)),
            ("head",     new NFunc(args => Verb("http.head",   HttpMethod.Head,   args), -1)),
            ("request",  new NFunc(Request, -1)),
            ("download", new NFunc(Download, -1)),
            ("stream",   new NFunc(Stream,   -1)));
    }

    // ─── client ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Shared client. Auto-redirect / cookies disabled so we can drive the
    /// redirect loop ourselves (and report the final URL back to the caller)
    /// and so cookies don't leak between unrelated builtin invocations. Default
    /// timeout is <see cref="Timeout.InfiniteTimeSpan"/> because the per-request
    /// <c>timeout</c> option is enforced through a <see cref="CancellationTokenSource"/>.
    /// </summary>
    private static readonly HttpClient Client = new(
        new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
        })
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };

    private const int DefaultTimeoutMs = 100_000;
    private const int MaxRedirects = 10;

    // ─── verb dispatchers ───────────────────────────────────────────────────

    private static NValue Verb(string op, HttpMethod method, NValue[] args)
    {
        if (args.Length is < 1 or > 2)
            throw new EvaluatorException($"{op} expects 1 or 2 arguments, got {args.Length}");
        var url = RequireString(args[0], op, "url");
        var opts = args.Length == 2 ? RequireRecord(args[1], op, "options") : null;
        return Execute(op, method, url, opts);
    }

    private static NValue Request(NValue[] args)
    {
        const string op = "http.request";
        if (args.Length != 1) throw new EvaluatorException($"{op} expects 1 argument (options record), got {args.Length}");
        var opts = RequireRecord(args[0], op, "options");
        var url = ReadString(opts, "url", null)
            ?? throw new EvaluatorException($"{op}: 'url' is required");
        var methodStr = ReadString(opts, "method", "GET")!;
        var method = ParseMethod(op, methodStr);
        return Execute(op, method, url, opts);
    }

    // ─── core execution ─────────────────────────────────────────────────────

    private static NValue Execute(string op, HttpMethod method, string url, NRecord? opts)
    {
        var timeout = ReadTimeout(opts, op);
        var followRedirects = ReadBool(opts, "follow_redirects", true);
        var parseJsonRequested = ReadBool(opts, "json", false);

        var sw = Stopwatch.StartNew();
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            var currentUrl = AppendQuery(op, url, opts);
            var currentMethod = method;
            HttpResponseMessage? response = null;

            for (int hop = 0; hop <= MaxRedirects; hop++)
            {
                response?.Dispose();
                using var req = BuildRequest(op, currentMethod, currentUrl, opts, hop == 0);
                response = Client.Send(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);

                if (!followRedirects || !IsRedirect(response.StatusCode)) break;
                if (hop == MaxRedirects)
                    throw new EvaluatorException($"{op}: too many redirects (>{MaxRedirects})");
                if (response.Headers.Location is not { } loc)
                    throw new EvaluatorException($"{op}: redirect {(int)response.StatusCode} from '{currentUrl}' had no Location header");

                currentUrl = loc.IsAbsoluteUri ? loc.ToString() : new Uri(new Uri(currentUrl), loc).ToString();
                if (response.StatusCode is HttpStatusCode.SeeOther or HttpStatusCode.Found or HttpStatusCode.MovedPermanently)
                {
                    if (currentMethod != HttpMethod.Head && currentMethod != HttpMethod.Get)
                        currentMethod = HttpMethod.Get;
                }
            }

            sw.Stop();
            var finalResponse = response!;
            using (finalResponse)
            {
                return BuildResponse(op, finalResponse, sw.ElapsedMilliseconds, currentUrl, parseJsonRequested, method);
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            throw new EvaluatorException($"{op}: timeout after {timeout.TotalMilliseconds:F0} ms");
        }
        catch (HttpRequestException ex)
        {
            throw new EvaluatorException($"{op}: {ex.Message}", ex);
        }
    }

    private static HttpRequestMessage BuildRequest(string op, HttpMethod method, string url, NRecord? opts, bool firstHop)
    {
        var req = new HttpRequestMessage(method, url);

        // Body / form / json — request content. Only set on the original hop; on
        // redirect we let Execute fall through with a fresh BuildRequest call that
        // sees firstHop=false (above) — but the redirect path passes the same opts,
        // so we'd double-attach. Keep it simple: only attach body on firstHop.
        if (firstHop)
        {
            var hasBody  = opts?.Fields.ContainsKey("body") ?? false;
            var hasForm  = opts?.Fields.ContainsKey("form") ?? false;
            var jsonFlag = ReadBool(opts, "json", false);
            if (hasBody && hasForm)
                throw new EvaluatorException($"{op}: 'body' and 'form' are mutually exclusive");

            if (hasBody)
            {
                var bodyVal = opts!.Fields["body"];
                if (jsonFlag)
                {
                    byte[] bytes;
                    try { bytes = NValueToJson.SerializeToUtf8Bytes(bodyVal); }
                    catch (EvaluatorException ex) { throw new EvaluatorException($"{op}: body — {ex.Message}", ex); }
                    req.Content = new ByteArrayContent(bytes);
                    req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
                }
                else if (bodyVal is NString s)
                {
                    req.Content = new StringContent(s.Value, Encoding.UTF8);
                }
                else
                {
                    throw new EvaluatorException($"{op}: 'body' must be a string when 'json' is not true (got {ValueFormatter.TypeName(bodyVal)})");
                }
            }
            else if (hasForm)
            {
                if (opts!.Fields["form"] is not NRecord form)
                    throw new EvaluatorException($"{op}: 'form' must be a record");
                var pairs = new List<KeyValuePair<string, string>>(form.Fields.Count);
                foreach (var kv in form.Fields)
                    pairs.Add(new KeyValuePair<string, string>(kv.Key, StringifyForForm(op, kv.Value)));
                req.Content = new FormUrlEncodedContent(pairs);
            }
        }

        // Auth — honoured every hop (most servers accept the credential on the
        // redirected URL too; if the user wants stricter behaviour they can
        // disable redirects).
        if (opts?.Fields.TryGetValue("auth", out var authVal) == true)
            ApplyAuth(op, req, authVal);

        // Accept / user-agent shortcuts.
        if (ReadString(opts, "accept", null) is { } accept)
            req.Headers.Accept.ParseAdd(accept);
        if (ReadString(opts, "user_agent", null) is { } ua)
            req.Headers.UserAgent.ParseAdd(ua);

        // Generic headers — applied last so a user-supplied Authorization can
        // override the convenience auth shortcut if both are set.
        if (opts?.Fields.TryGetValue("headers", out var headersVal) == true)
        {
            if (headersVal is not NRecord headers)
                throw new EvaluatorException($"{op}: 'headers' must be a record");
            foreach (var kv in headers.Fields)
                ApplyHeader(op, req, kv.Key, kv.Value);
        }

        return req;
    }

    private static NValue BuildResponse(
        string op,
        HttpResponseMessage response,
        long elapsedMs,
        string finalUrl,
        bool parseJsonRequested,
        HttpMethod originalMethod)
    {
        var statusCode = (int)response.StatusCode;
        var ok = statusCode >= 200 && statusCode < 300;
        var headerRecord = HeadersToRecord(response);

        NValue body;
        bool bytesOnly = false;
        if (originalMethod == HttpMethod.Head)
        {
            body = NUnit.Instance;
        }
        else
        {
            // Read fully to a string. ReadAsStream + manual decoding would let
            // us avoid the extra buffer copy for tiny responses, but the simple
            // path matches FsModule's eager-read style.
            byte[] bytes;
            using (var src = response.Content.ReadAsStream())
            using (var dst = new MemoryStream())
            {
                src.CopyTo(dst);
                bytes = dst.ToArray();
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            bool jsonByCt = contentType is not null
                && (contentType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
                    || contentType.EndsWith("+json", StringComparison.OrdinalIgnoreCase));

            if (parseJsonRequested || jsonByCt)
            {
                var text = SafeDecode(bytes, response.Content.Headers.ContentType?.CharSet);
                if (string.IsNullOrWhiteSpace(text))
                {
                    body = NUnit.Instance;
                }
                else
                {
                    try { body = JsonToNValue.Parse(text); }
                    catch (Exception ex) when (ex is not EvaluatorException)
                    {
                        // Don't blow up if the server lied about content-type; surface
                        // the raw body so callers can debug.
                        if (parseJsonRequested)
                            throw new EvaluatorException($"{op}: response was not valid JSON: {ex.Message}", ex);
                        body = new NString(text);
                    }
                }
            }
            else
            {
                var charset = response.Content.Headers.ContentType?.CharSet;
                if (LooksTextual(bytes))
                {
                    body = new NString(SafeDecode(bytes, charset));
                }
                else
                {
                    body = new NInt(bytes.LongLength);
                    bytesOnly = true;
                }
            }
        }

        var b = ImmutableSortedDictionary.CreateBuilder<string, NValue>(StringComparer.Ordinal);
        b["status"] = new NInt(statusCode);
        b["status_text"] = new NString(ReasonPhraseFor(response));
        b["ok"] = new NBool(ok);
        b["headers"] = headerRecord;
        b["body"] = body;
        if (bytesOnly) b["bytes_only"] = new NBool(true);
        b["url"] = new NString(finalUrl);
        b["elapsed_ms"] = new NInt(elapsedMs);
        b["__display"] = new NString("body");
        b["__columns"] = new NList(ImmutableArray.Create<NValue>(
            new NString("status"),
            new NString("url"),
            new NString("elapsed_ms")));
        return new NRecord(b.ToImmutable());
    }

    // ─── download ───────────────────────────────────────────────────────────

    private static NValue Download(NValue[] args)
    {
        const string op = "http.download";
        if (args.Length is < 2 or > 3)
            throw new EvaluatorException($"{op} expects 2 or 3 arguments, got {args.Length}");
        var url = RequireString(args[0], op, "url");
        var path = RequireString(args[1], op, "path");
        var opts = args.Length == 3 ? RequireRecord(args[2], op, "options") : null;

        var timeout = ReadTimeout(opts, op);
        var fullPath = Path.GetFullPath(path);
        var tempPath = fullPath + ".part";
        var sw = Stopwatch.StartNew();
        using var cts = new CancellationTokenSource(timeout);

        try
        {
            using var req = BuildRequest(op, HttpMethod.Get, AppendQuery(op, url, opts), opts, firstHop: true);
            using var response = Client.Send(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);

            if (!response.IsSuccessStatusCode)
                throw new EvaluatorException($"{op}: server returned {(int)response.StatusCode} {ReasonPhraseFor(response)}");

            long bytes;
            using (var src = response.Content.ReadAsStream())
            using (var dst = File.Create(tempPath))
            {
                src.CopyTo(dst);
                bytes = dst.Length;
            }
            // Replace existing target atomically-ish: File.Move with overwrite is the
            // closest cross-platform primitive .NET gives us short of P/Invoke.
            if (File.Exists(fullPath)) File.Delete(fullPath);
            File.Move(tempPath, fullPath);
            sw.Stop();

            var b = ImmutableSortedDictionary.CreateBuilder<string, NValue>(StringComparer.Ordinal);
            b["status"] = new NInt((int)response.StatusCode);
            b["headers"] = HeadersToRecord(response);
            b["path"] = new NString(fullPath);
            b["bytes"] = new NInt(bytes);
            b["elapsed_ms"] = new NInt(sw.ElapsedMilliseconds);
            b["__display"] = new NString("path");
            b["__columns"] = new NList(ImmutableArray.Create<NValue>(
                new NString("path"), new NString("bytes"), new NString("status")));
            return new NRecord(b.ToImmutable());
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            TryDelete(tempPath);
            throw new EvaluatorException($"{op}: timeout after {timeout.TotalMilliseconds:F0} ms");
        }
        catch (HttpRequestException ex)
        {
            TryDelete(tempPath);
            throw new EvaluatorException($"{op}: {ex.Message}", ex);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private static void TryDelete(string p) { try { if (File.Exists(p)) File.Delete(p); } catch { /* swallow */ } }

    // ─── stream ─────────────────────────────────────────────────────────────

    private static NValue Stream(NValue[] args)
    {
        const string op = "http.stream";
        if (args.Length is < 1 or > 2)
            throw new EvaluatorException($"{op} expects 1 or 2 arguments, got {args.Length}");
        var url = RequireString(args[0], op, "url");
        var opts = args.Length == 2 ? RequireRecord(args[1], op, "options") : null;
        // Stream lifecycle: we open the response when the NSeq is enumerated,
        // not at the call site. That keeps the seq re-iterable (each pass
        // reissues the request) — matches the lazy-seq contract on NSeq.
        return new NSeq(StreamLines(op, url, opts));
    }

    private static IEnumerable<NValue> StreamLines(string op, string url, NRecord? opts)
    {
        var timeout = ReadTimeout(opts, op);
        using var cts = new CancellationTokenSource(timeout);
        using var req = BuildRequest(op, HttpMethod.Get, AppendQuery(op, url, opts), opts, firstHop: true);

        HttpResponseMessage response;
        try { response = Client.Send(req, HttpCompletionOption.ResponseHeadersRead, cts.Token); }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        { throw new EvaluatorException($"{op}: timeout after {timeout.TotalMilliseconds:F0} ms"); }
        catch (HttpRequestException ex)
        { throw new EvaluatorException($"{op}: {ex.Message}", ex); }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new EvaluatorException($"{op}: server returned {(int)response.StatusCode} {ReasonPhraseFor(response)}");

            var contentType = response.Content.Headers.ContentType?.MediaType;
            bool sse = string.Equals(contentType, "text/event-stream", StringComparison.OrdinalIgnoreCase);

            using var src = response.Content.ReadAsStream();
            using var reader = new StreamReader(src, Encoding.UTF8);

            if (sse)
            {
                foreach (var frame in ReadSseFrames(reader))
                    yield return frame;
            }
            else
            {
                string? line;
                while ((line = reader.ReadLine()) is not null)
                    yield return new NString(line);
            }
        }
    }

    private static IEnumerable<NValue> ReadSseFrames(StreamReader reader)
    {
        var dataBuf = new StringBuilder();
        string? eventName = null;
        string? id = null;
        long? retry = null;

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
            {
                if (dataBuf.Length > 0 || eventName is not null || id is not null || retry is not null)
                {
                    yield return BuildSseFrame(eventName, dataBuf, id, retry);
                    dataBuf.Clear();
                    eventName = null;
                    id = null;
                    retry = null;
                }
                continue;
            }
            if (line[0] == ':') continue; // SSE comment

            int colon = line.IndexOf(':');
            string field; string value;
            if (colon < 0) { field = line; value = string.Empty; }
            else
            {
                field = line.Substring(0, colon);
                value = line.Substring(colon + 1);
                if (value.Length > 0 && value[0] == ' ') value = value.Substring(1);
            }

            switch (field)
            {
                case "data":
                    if (dataBuf.Length > 0) dataBuf.Append('\n');
                    dataBuf.Append(value);
                    break;
                case "event": eventName = value; break;
                case "id": id = value; break;
                case "retry":
                    if (long.TryParse(value, out var r)) retry = r;
                    break;
            }
        }
        // Flush trailing frame even without final blank line (servers occasionally
        // close the connection right after the last data: line).
        if (dataBuf.Length > 0 || eventName is not null || id is not null || retry is not null)
            yield return BuildSseFrame(eventName, dataBuf, id, retry);
    }

    private static NValue BuildSseFrame(string? eventName, StringBuilder data, string? id, long? retry)
    {
        var b = ImmutableSortedDictionary.CreateBuilder<string, NValue>(StringComparer.Ordinal);
        b["event"] = eventName is null ? NUnit.Instance : new NString(eventName);
        b["data"] = new NString(data.ToString());
        b["id"] = id is null ? NUnit.Instance : new NString(id);
        b["retry"] = retry is null ? NUnit.Instance : new NInt(retry.Value);
        b["__display"] = new NString("data");
        return new NRecord(b.ToImmutable());
    }

    // ─── helpers ────────────────────────────────────────────────────────────

    private static HttpMethod ParseMethod(string op, string s) => s.ToUpperInvariant() switch
    {
        "GET" => HttpMethod.Get,
        "POST" => HttpMethod.Post,
        "PUT" => HttpMethod.Put,
        "PATCH" => HttpMethod.Patch,
        "DELETE" => HttpMethod.Delete,
        "HEAD" => HttpMethod.Head,
        "OPTIONS" => HttpMethod.Options,
        _ => throw new EvaluatorException($"{op}: unsupported method '{s}'")
    };

    private static bool IsRedirect(HttpStatusCode code)
    {
        var c = (int)code;
        return c is 301 or 302 or 303 or 307 or 308;
    }

    private static string AppendQuery(string op, string url, NRecord? opts)
    {
        if (opts?.Fields.TryGetValue("query", out var qv) != true) return url;
        if (qv is not NRecord q) throw new EvaluatorException($"{op}: 'query' must be a record");
        if (q.Fields.Count == 0) return url;

        var sb = new StringBuilder(url);
        sb.Append(url.Contains('?') ? '&' : '?');
        bool first = true;
        foreach (var kv in q.Fields)
        {
            if (!first) sb.Append('&');
            first = false;
            sb.Append(Uri.EscapeDataString(kv.Key));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(StringifyForForm(op, kv.Value)));
        }
        return sb.ToString();
    }

    private static string StringifyForForm(string op, NValue v) => v switch
    {
        NString s => s.Value,
        NInt i => i.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        NFloat f => f.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        NBool b => b.Value ? "true" : "false",
        NUnit => string.Empty,
        _ => throw new EvaluatorException($"{op}: query/form values must be string/int/float/bool/unit (got {ValueFormatter.TypeName(v)})")
    };

    private static void ApplyAuth(string op, HttpRequestMessage req, NValue authVal)
    {
        if (authVal is not NRecord auth) throw new EvaluatorException($"{op}: 'auth' must be a record");
        if (!auth.Fields.TryGetValue("type", out var typeVal) || typeVal is not NString typeStr)
            throw new EvaluatorException($"{op}: 'auth.type' must be a string (\"basic\" or \"bearer\")");

        switch (typeStr.Value.ToLowerInvariant())
        {
            case "basic":
                {
                    var user = ReadString(auth, "user", null) ?? throw new EvaluatorException($"{op}: 'auth.user' required for basic auth");
                    var pass = ReadString(auth, "pass", null) ?? throw new EvaluatorException($"{op}: 'auth.pass' required for basic auth");
                    var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}"));
                    req.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
                    break;
                }
            case "bearer":
                {
                    var token = ReadString(auth, "token", null) ?? throw new EvaluatorException($"{op}: 'auth.token' required for bearer auth");
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    break;
                }
            default:
                throw new EvaluatorException($"{op}: unknown auth type '{typeStr.Value}' (use \"basic\" or \"bearer\")");
        }
    }

    private static void ApplyHeader(string op, HttpRequestMessage req, string name, NValue value)
    {
        if (value is not NString s) throw new EvaluatorException($"{op}: header '{name}' must be a string (got {ValueFormatter.TypeName(value)})");
        // ContentType / ContentLength etc. must go on req.Content; HttpClient
        // distinguishes "content" headers from "request" headers and throws if
        // we add them to the wrong collection. TryAddWithoutValidation on both
        // collections sidesteps the distinction without changing the wire
        // representation.
        if (!req.Headers.TryAddWithoutValidation(name, s.Value))
        {
            if (req.Content is not null)
                req.Content.Headers.TryAddWithoutValidation(name, s.Value);
        }
    }

    private static NRecord HeadersToRecord(HttpResponseMessage response)
    {
        var b = ImmutableSortedDictionary.CreateBuilder<string, NValue>(StringComparer.Ordinal);
        foreach (var h in response.Headers)
            b[h.Key.ToLowerInvariant()] = new NString(string.Join(", ", h.Value));
        if (response.Content is not null)
            foreach (var h in response.Content.Headers)
                b[h.Key.ToLowerInvariant()] = new NString(string.Join(", ", h.Value));
        return new NRecord(b.ToImmutable());
    }

    private static string ReasonPhraseFor(HttpResponseMessage r) =>
        r.ReasonPhrase ?? r.StatusCode.ToString();

    private static TimeSpan ReadTimeout(NRecord? opts, string op)
    {
        if (opts?.Fields.TryGetValue("timeout", out var v) != true)
            return TimeSpan.FromMilliseconds(DefaultTimeoutMs);
        if (v is not NInt ni) throw new EvaluatorException($"{op}: 'timeout' must be an int (milliseconds)");
        if (ni.Value <= 0) throw new EvaluatorException($"{op}: 'timeout' must be positive");
        return TimeSpan.FromMilliseconds(ni.Value);
    }

    private static bool ReadBool(NRecord? r, string key, bool defaultValue)
    {
        if (r is null) return defaultValue;
        if (!r.Fields.TryGetValue(key, out var v)) return defaultValue;
        if (v is NBool b) return b.Value;
        throw new EvaluatorException($"option '{key}' must be a bool");
    }

    private static string? ReadString(NRecord? r, string key, string? defaultValue)
    {
        if (r is null) return defaultValue;
        if (!r.Fields.TryGetValue(key, out var v)) return defaultValue;
        if (v is NString s) return s.Value;
        throw new EvaluatorException($"option '{key}' must be a string");
    }

    private static string RequireString(NValue v, string op, string name) => v switch
    {
        NString s => s.Value,
        _ => throw new EvaluatorException($"{op}: '{name}' must be a string (got {ValueFormatter.TypeName(v)})")
    };

    private static NRecord RequireRecord(NValue v, string op, string name) => v switch
    {
        NRecord r => r,
        _ => throw new EvaluatorException($"{op}: '{name}' must be a record (got {ValueFormatter.TypeName(v)})")
    };

    /// <summary>
    /// Decode bytes using the response's declared charset when we can resolve
    /// it, falling back to UTF-8. <see cref="Encoding.GetEncoding(string)"/>
    /// without the EncodingProvider only knows a handful of legacy charsets in
    /// AOT, so we swallow the resolution exception and use UTF-8 — the body is
    /// still readable for the common "text/plain; charset=ISO-8859-1" case.
    /// </summary>
    private static string SafeDecode(byte[] bytes, string? charset)
    {
        if (string.IsNullOrEmpty(charset)) return Encoding.UTF8.GetString(bytes);
        Encoding enc;
        try { enc = Encoding.GetEncoding(charset); }
        catch { enc = Encoding.UTF8; }
        return enc.GetString(bytes);
    }

    /// <summary>
    /// Cheap heuristic — if the first KB has no NUL bytes and decodes as valid
    /// UTF-8, treat the response as text and return its decoded string. Lets
    /// callers pipe the body through text builtins without forcing them to use
    /// <c>http.download</c> for binary endpoints.
    /// </summary>
    private static bool LooksTextual(byte[] bytes)
    {
        int n = Math.Min(bytes.Length, 1024);
        for (int i = 0; i < n; i++) if (bytes[i] == 0) return false;
        try { _ = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes, 0, n); return true; }
        catch { return false; }
    }
}
