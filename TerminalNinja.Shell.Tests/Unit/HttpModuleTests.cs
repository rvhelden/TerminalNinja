using System.Net;
using System.Net.Sockets;
using System.Text;
using TerminalNinja.Shell.Builtins;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Tests.Unit;

public class HttpModuleTests
{
    private static NValue Run(string source)
        => NinjaEvaluator.EvalSource(source, BuiltinRegistry.CreateDefaultEnv()).Value;

    private static int GetFreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        try { return ((IPEndPoint)l.LocalEndpoint).Port; }
        finally { l.Stop(); }
    }

    /// <summary>
    /// Spin up an HttpListener bound to 127.0.0.1, run <paramref name="test"/>
    /// with the resulting base URL, then tear the listener down. Handler is
    /// invoked once per request on a background thread; the listener is stopped
    /// from the foreground after the test body returns.
    /// </summary>
    private static async Task WithServer(
        Action<HttpListenerContext> handler,
        Func<string, Task> test)
    {
        var port = GetFreePort();
        var prefix = $"http://127.0.0.1:{port}/";
        var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();

        var cts = new CancellationTokenSource();
        var serverTask = Task.Run(() =>
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    HttpListenerContext ctx;
                    try { ctx = listener.GetContext(); }
                    catch (HttpListenerException) { return; }
                    catch (ObjectDisposedException) { return; }
                    try { handler(ctx); }
                    catch (Exception ex)
                    {
                        try
                        {
                            ctx.Response.StatusCode = 500;
                            var body = Encoding.UTF8.GetBytes("server-handler-threw: " + ex.Message);
                            ctx.Response.OutputStream.Write(body, 0, body.Length);
                        }
                        catch { /* ignore */ }
                    }
                    finally
                    {
                        try { ctx.Response.Close(); } catch { /* ignore */ }
                    }
                }
            }
            finally { try { listener.Stop(); } catch { /* ignore */ } }
        });

        try
        {
            await test(prefix);
        }
        finally
        {
            cts.Cancel();
            try { listener.Stop(); } catch { /* ignore */ }
            try { listener.Close(); } catch { /* ignore */ }
            await Task.WhenAny(serverTask, Task.Delay(2000));
        }
    }

    // ─── basic verbs ────────────────────────────────────────────────────────

    [Test]
    public async Task HttpGet_ReturnsRecordWithStatusOkAndBody()
    {
        await WithServer(
            ctx =>
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/plain";
                var bytes = Encoding.UTF8.GetBytes("hello");
                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            },
            async url =>
            {
                var v = Run($"http.get(\"{url}\")");
                if (v is not NRecord r) throw new InvalidOperationException("expected record");
                await Assert.That(r.Fields["status"]).IsEqualTo((NValue)new NInt(200));
                await Assert.That(r.Fields["ok"]).IsEqualTo((NValue)new NBool(true));
                await Assert.That(r.Fields["body"]).IsEqualTo((NValue)new NString("hello"));
            });
    }

    [Test]
    public async Task HttpGet_404_OkFalseStatusTextNotFound()
    {
        await WithServer(
            ctx => { ctx.Response.StatusCode = 404; },
            async url =>
            {
                var v = Run($"http.get(\"{url}missing\")");
                if (v is not NRecord r) throw new InvalidOperationException();
                await Assert.That(r.Fields["status"]).IsEqualTo((NValue)new NInt(404));
                await Assert.That(r.Fields["ok"]).IsEqualTo((NValue)new NBool(false));
            });
    }

    [Test]
    public async Task HttpGet_JsonContentType_AutoParsesBody()
    {
        await WithServer(
            ctx =>
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "application/json";
                var bytes = Encoding.UTF8.GetBytes("{\"name\":\"alpha\",\"age\":40}");
                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            },
            async url =>
            {
                var v = Run($"http.get(\"{url}\").body.name");
                await Assert.That(v).IsEqualTo((NValue)new NString("alpha"));
            });
    }

    [Test]
    public async Task HttpGet_JsonOption_ForcesParseEvenWithoutHeader()
    {
        await WithServer(
            ctx =>
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/plain";  // server lies
                var bytes = Encoding.UTF8.GetBytes("[1,2,3]");
                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            },
            async url =>
            {
                var v = Run($"http.get(\"{url}\", {{ json: true }}).body");
                if (v is not NList list) throw new InvalidOperationException();
                await Assert.That(list.Items.Length).IsEqualTo(3);
            });
    }

    [Test]
    public async Task HttpPost_JsonBodyIsSerializedAndContentTypeSet()
    {
        string? receivedBody = null;
        string? receivedCt = null;
        await WithServer(
            ctx =>
            {
                using var sr = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
                receivedBody = sr.ReadToEnd();
                receivedCt = ctx.Request.ContentType;
                ctx.Response.StatusCode = 201;
            },
            async url =>
            {
                Run($"http.post(\"{url}\", {{ json: true, body: {{ hello: \"world\" }} }})");
                await Assert.That(receivedBody).IsEqualTo("{\"hello\":\"world\"}");
                await Assert.That(receivedCt).IsNotNull();
                await Assert.That(receivedCt!).Contains("application/json");
            });
    }

    [Test]
    public async Task HttpPost_FormBody_UrlEncoded()
    {
        string? receivedBody = null;
        string? receivedCt = null;
        await WithServer(
            ctx =>
            {
                using var sr = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
                receivedBody = sr.ReadToEnd();
                receivedCt = ctx.Request.ContentType;
                ctx.Response.StatusCode = 200;
            },
            async url =>
            {
                Run($"http.post(\"{url}\", {{ form: {{ a: \"1\", b: \"two words\" }} }})");
                await Assert.That(receivedCt).IsNotNull();
                await Assert.That(receivedCt!).Contains("application/x-www-form-urlencoded");
                await Assert.That(receivedBody).IsEqualTo("a=1&b=two+words");
            });
    }

    [Test]
    public async Task HttpRequest_QueryRecord_AppendedAndEncoded()
    {
        string? receivedQuery = null;
        await WithServer(
            ctx =>
            {
                receivedQuery = ctx.Request.Url?.Query;
                ctx.Response.StatusCode = 200;
            },
            async url =>
            {
                Run($"http.get(\"{url}\", {{ query: {{ q: \"two words\", page: \"1\" }} }})");
                await Assert.That(receivedQuery).IsNotNull();
                // ImmutableSortedDictionary sorts keys, so 'page' precedes 'q' in the
                // emitted query string. Verify both pairs are present and encoded.
                await Assert.That(receivedQuery!).Contains("page=1");
                await Assert.That(receivedQuery!).Contains("q=two%20words");
            });
    }

    [Test]
    public async Task HttpGet_CustomHeader_IsSent()
    {
        string? receivedAuth = null;
        await WithServer(
            ctx =>
            {
                receivedAuth = ctx.Request.Headers["X-Echo"];
                ctx.Response.StatusCode = 200;
            },
            async url =>
            {
                Run($"http.get(\"{url}\", {{ headers: {{ \"X-Echo\": \"abc123\" }} }})");
                await Assert.That(receivedAuth).IsEqualTo("abc123");
            });
    }

    [Test]
    public async Task HttpGet_BearerAuth_AddsAuthorizationHeader()
    {
        string? receivedAuth = null;
        await WithServer(
            ctx =>
            {
                receivedAuth = ctx.Request.Headers["Authorization"];
                ctx.Response.StatusCode = 200;
            },
            async url =>
            {
                Run($"http.get(\"{url}\", {{ auth: {{ type: \"bearer\", token: \"tk_42\" }} }})");
                await Assert.That(receivedAuth).IsEqualTo("Bearer tk_42");
            });
    }

    [Test]
    public async Task HttpGet_BasicAuth_AddsBase64Header()
    {
        string? receivedAuth = null;
        await WithServer(
            ctx =>
            {
                receivedAuth = ctx.Request.Headers["Authorization"];
                ctx.Response.StatusCode = 200;
            },
            async url =>
            {
                Run($"http.get(\"{url}\", {{ auth: {{ type: \"basic\", user: \"u\", pass: \"p\" }} }})");
                var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("u:p"));
                await Assert.That(receivedAuth).IsEqualTo(expected);
            });
    }

    [Test]
    public async Task HttpDelete_VerbReachesServer()
    {
        string? method = null;
        await WithServer(
            ctx => { method = ctx.Request.HttpMethod; ctx.Response.StatusCode = 204; },
            async url =>
            {
                var v = Run($"http.delete(\"{url}\")");
                if (v is not NRecord r) throw new InvalidOperationException();
                await Assert.That(r.Fields["status"]).IsEqualTo((NValue)new NInt(204));
                await Assert.That(method).IsEqualTo("DELETE");
            });
    }

    [Test]
    public async Task HttpHead_BodyIsUnit()
    {
        await WithServer(
            ctx =>
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.AddHeader("X-Test", "yes");
            },
            async url =>
            {
                var v = Run($"http.head(\"{url}\")");
                if (v is not NRecord r) throw new InvalidOperationException();
                await Assert.That(r.Fields["body"] is NUnit).IsTrue();
                if (r.Fields["headers"] is not NRecord h) throw new InvalidOperationException();
                await Assert.That(h.Fields["x-test"]).IsEqualTo((NValue)new NString("yes"));
            });
    }

    // ─── option-validation errors ───────────────────────────────────────────

    [Test]
    public async Task HttpPost_BodyAndFormTogether_Throws()
    {
        await Assert.That(() => Run("http.post(\"http://127.0.0.1:1/\", { body: \"x\", form: { a: \"1\" } })"))
            .ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task HttpGet_NonStringUrl_Throws()
    {
        await Assert.That(() => Run("http.get(42)")).ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task HttpGet_OptionsNotRecord_Throws()
    {
        await Assert.That(() => Run("http.get(\"http://127.0.0.1:1/\", \"opts\")"))
            .ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task HttpRequest_MissingUrl_Throws()
    {
        await Assert.That(() => Run("http.request({ method: \"GET\" })"))
            .ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task HttpGet_Timeout_RaisesEvaluatorException()
    {
        await WithServer(
            ctx =>
            {
                Thread.Sleep(500);  // longer than the test's 50ms timeout
                ctx.Response.StatusCode = 200;
            },
            async url =>
            {
                await Assert.That(() => Run($"http.get(\"{url}\", {{ timeout: 50 }})"))
                    .ThrowsExactly<EvaluatorException>();
            });
    }

    // ─── redirects ──────────────────────────────────────────────────────────

    [Test]
    public async Task HttpGet_FollowsRedirectByDefault_ReturnsFinalBody()
    {
        await WithServer(
            ctx =>
            {
                if (ctx.Request.Url!.AbsolutePath == "/start")
                {
                    ctx.Response.StatusCode = 302;
                    ctx.Response.AddHeader("Location", "/end");
                }
                else
                {
                    ctx.Response.StatusCode = 200;
                    var b = Encoding.UTF8.GetBytes("arrived");
                    ctx.Response.ContentType = "text/plain";
                    ctx.Response.OutputStream.Write(b, 0, b.Length);
                }
            },
            async url =>
            {
                var v = Run($"http.get(\"{url}start\")");
                if (v is not NRecord r) throw new InvalidOperationException();
                await Assert.That(r.Fields["body"]).IsEqualTo((NValue)new NString("arrived"));
                if (r.Fields["url"] is not NString u) throw new InvalidOperationException();
                await Assert.That(u.Value).EndsWith("/end");
            });
    }

    [Test]
    public async Task HttpGet_FollowRedirectsFalse_Returns3xx()
    {
        await WithServer(
            ctx =>
            {
                ctx.Response.StatusCode = 301;
                ctx.Response.AddHeader("Location", "/elsewhere");
            },
            async url =>
            {
                var v = Run($"http.get(\"{url}\", {{ follow_redirects: false }})");
                if (v is not NRecord r) throw new InvalidOperationException();
                await Assert.That(r.Fields["status"]).IsEqualTo((NValue)new NInt(301));
            });
    }

    // ─── download ───────────────────────────────────────────────────────────

    [Test]
    public async Task HttpDownload_WritesFile_ReportsBytesAndPath()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "ninja_http_dl_" + Guid.NewGuid().ToString("N") + ".bin");
        var payload = new byte[5000];
        for (int i = 0; i < payload.Length; i++) payload[i] = (byte)(i & 0xFF);

        try
        {
            await WithServer(
                ctx =>
                {
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "application/octet-stream";
                    ctx.Response.OutputStream.Write(payload, 0, payload.Length);
                },
                async url =>
                {
                    var v = Run($"http.download(\"{url}\", \"{tempPath.Replace("\\", "\\\\")}\")");
                    if (v is not NRecord r) throw new InvalidOperationException();
                    await Assert.That(r.Fields["bytes"]).IsEqualTo((NValue)new NInt(payload.Length));
                    if (r.Fields["path"] is not NString p) throw new InvalidOperationException();
                    await Assert.That(File.Exists(p.Value)).IsTrue();
                    await Assert.That(new FileInfo(p.Value).Length).IsEqualTo(payload.Length);
                });
        }
        finally { try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { } }
    }

    [Test]
    public async Task HttpDownload_NonSuccessStatus_Throws_AndDoesNotLeavePartFile()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "ninja_http_dl_" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            await WithServer(
                ctx => { ctx.Response.StatusCode = 500; },
                async url =>
                {
                    await Assert.That(() => Run($"http.download(\"{url}\", \"{tempPath.Replace("\\", "\\\\")}\")"))
                        .ThrowsExactly<EvaluatorException>();
                    await Assert.That(File.Exists(tempPath + ".part")).IsFalse();
                    await Assert.That(File.Exists(tempPath)).IsFalse();
                });
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            try { if (File.Exists(tempPath + ".part")) File.Delete(tempPath + ".part"); } catch { }
        }
    }

    // ─── stream ─────────────────────────────────────────────────────────────

    [Test]
    public async Task HttpStream_NonSse_YieldsLineStrings()
    {
        await WithServer(
            ctx =>
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/plain";
                var bytes = Encoding.UTF8.GetBytes("one\ntwo\nthree\n");
                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            },
            async url =>
            {
                // materialize forces the lazy NSeq into an NList we can introspect.
                var v = Run($"materialize(http.stream(\"{url}\"))");
                if (v is not NList list) throw new InvalidOperationException();
                await Assert.That(list.Items.Length).IsEqualTo(3);
                await Assert.That(list.Items[0]).IsEqualTo((NValue)new NString("one"));
                await Assert.That(list.Items[2]).IsEqualTo((NValue)new NString("three"));
            });
    }

    [Test]
    public async Task HttpStream_Sse_ParsesFramesIntoRecords()
    {
        await WithServer(
            ctx =>
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/event-stream";
                // Two SSE frames separated by a blank line. The first has all
                // four fields; the second only a data line.
                var bytes = Encoding.UTF8.GetBytes(
                    "event: greet\ndata: hello\nid: 1\nretry: 1000\n\ndata: world\n\n");
                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            },
            async url =>
            {
                var v = Run($"materialize(http.stream(\"{url}\"))");
                if (v is not NList list) throw new InvalidOperationException();
                await Assert.That(list.Items.Length).IsEqualTo(2);
                if (list.Items[0] is not NRecord first) throw new InvalidOperationException();
                await Assert.That(first.Fields["event"]).IsEqualTo((NValue)new NString("greet"));
                await Assert.That(first.Fields["data"]).IsEqualTo((NValue)new NString("hello"));
                await Assert.That(first.Fields["id"]).IsEqualTo((NValue)new NString("1"));
                await Assert.That(first.Fields["retry"]).IsEqualTo((NValue)new NInt(1000));
                if (list.Items[1] is not NRecord second) throw new InvalidOperationException();
                await Assert.That(second.Fields["data"]).IsEqualTo((NValue)new NString("world"));
                await Assert.That(second.Fields["event"] is NUnit).IsTrue();
            });
    }
}
