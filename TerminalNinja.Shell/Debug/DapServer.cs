using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using TerminalNinja.Shell.Language.Protocol;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Debug;

/// <summary>
/// Debug Adapter Protocol server — the protocol I/O dispatcher. Owns the
/// read-dispatch-write loop, hands script execution off to a
/// <see cref="DapSession"/>. Slice B scope: launch, breakpoints, stackTrace,
/// scopes/variables, continue. Stepping ships in a follow-up slice.
/// </summary>
public sealed class DapServer
{
    private readonly object _seqLock = new();
    private int _seq;
    private DapWriter _writer = null!;
    private DapSession? _session;
    private bool _terminated;

    /// <summary>Drive the read-dispatch-write loop until the input stream closes or the client disconnects.</summary>
    public void Run(Stream input, Stream output)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        var reader = new JsonRpcReader(input);
        _writer = new DapWriter(new JsonRpcWriter(output), NextSeq);

        while (!_terminated)
        {
            JsonDocument? message;
            try
            {
                message = reader.ReadMessage();
            }
            catch (EndOfStreamException)
            {
                break;
            }
            catch (JsonException ex)
            {
                _writer.WriteEvent("output", w =>
                {
                    w.WriteString("category", "console");
                    w.WriteString("output", $"ninja-dap: ignoring malformed message: {ex.Message}{Environment.NewLine}");
                });
                continue;
            }
            if (message is null) break;
            using (message) Dispatch(message.RootElement);
        }

        // Let the worker drain (terminated event etc.) before tearing the
        // process down — bounded so a runaway script can't wedge teardown.
        _session?.Join(TimeSpan.FromSeconds(5));
    }

    private int NextSeq()
    {
        lock (_seqLock) return ++_seq;
    }

    private void Dispatch(JsonElement message)
    {
        if (!message.TryGetProperty("type", out var typeEl)) return;
        var type = typeEl.GetString();
        if (type != "request") return;

        var requestSeq = message.TryGetProperty("seq", out var seqEl) && seqEl.ValueKind == JsonValueKind.Number
            ? seqEl.GetInt32()
            : 0;
        var command = message.TryGetProperty("command", out var cmdEl) ? cmdEl.GetString() : null;
        message.TryGetProperty("arguments", out var args);

        switch (command)
        {
            case "initialize":
                HandleInitialize(requestSeq);
                break;
            case "launch":
                HandleLaunch(requestSeq, args);
                break;
            case "setBreakpoints":
                HandleSetBreakpoints(requestSeq, args);
                break;
            case "setExceptionBreakpoints":
                _writer.WriteAck(requestSeq, "setExceptionBreakpoints");
                break;
            case "configurationDone":
                HandleConfigurationDone(requestSeq);
                break;
            case "threads":
                _writer.WriteThreads(requestSeq);
                break;
            case "stackTrace":
                HandleStackTrace(requestSeq, args);
                break;
            case "scopes":
                HandleScopes(requestSeq, args);
                break;
            case "variables":
                HandleVariables(requestSeq, args);
                break;
            case "continue":
                HandleContinue(requestSeq);
                break;
            case "next":
                HandleStep(requestSeq, "next", StepMode.Over);
                break;
            case "stepIn":
                HandleStep(requestSeq, "stepIn", StepMode.In);
                break;
            case "stepOut":
                HandleStep(requestSeq, "stepOut", StepMode.Out);
                break;
            case "disconnect":
            case "terminate":
                _writer.WriteAck(requestSeq, command);
                _session?.Dispose();
                _terminated = true;
                break;
            default:
                _writer.WriteErrorResponse(requestSeq, command ?? "<unknown>", $"command not supported: {command}");
                break;
        }
    }

    private void HandleInitialize(int requestSeq)
    {
        // Create the session up front so setBreakpoints (which VS Code sends
        // before launch) has somewhere to store its data.
        _session ??= new DapSession(_writer);
        _writer.WriteResponse(requestSeq, "initialize", w =>
        {
            w.WriteStartObject();
            w.WriteBoolean("supportsConfigurationDoneRequest", true);
            w.WriteBoolean("supportsTerminateRequest", true);
            w.WriteEndObject();
        });
        _writer.WriteEvent("initialized", static _ => { });
    }

    private void HandleLaunch(int requestSeq, JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty("program", out var progEl)
            || progEl.ValueKind != JsonValueKind.String)
        {
            _writer.WriteErrorResponse(requestSeq, "launch", "launch arguments must include a 'program' string");
            return;
        }
        var program = progEl.GetString() ?? "";
        _session ??= new DapSession(_writer);
        var err = _session.Load(program);
        if (err is not null)
        {
            _writer.WriteErrorResponse(requestSeq, "launch", err);
            return;
        }
        _writer.WriteAck(requestSeq, "launch");
    }

    private void HandleSetBreakpoints(int requestSeq, JsonElement args)
    {
        // VS Code sends setBreakpoints between the `initialized` event and
        // `launch`, so we accept it before a program is loaded — the session
        // is created during `initialize`.
        _session ??= new DapSession(_writer);
        string sourcePath = "";
        if (args.ValueKind == JsonValueKind.Object && args.TryGetProperty("source", out var src)
            && src.TryGetProperty("path", out var pathEl) && pathEl.ValueKind == JsonValueKind.String)
        {
            sourcePath = pathEl.GetString() ?? "";
        }
        var lines = new List<int>();
        if (args.ValueKind == JsonValueKind.Object && args.TryGetProperty("breakpoints", out var bps)
            && bps.ValueKind == JsonValueKind.Array)
        {
            foreach (var bp in bps.EnumerateArray())
            {
                if (bp.TryGetProperty("line", out var lineEl) && lineEl.ValueKind == JsonValueKind.Number)
                    lines.Add(lineEl.GetInt32());
            }
        }
        var results = _session.SetBreakpoints(sourcePath, lines);
        _writer.WriteResponse(requestSeq, "setBreakpoints", w =>
        {
            w.WriteStartObject();
            w.WriteStartArray("breakpoints");
            foreach (var r in results)
            {
                w.WriteStartObject();
                w.WriteBoolean("verified", r.Verified);
                w.WriteNumber("line", r.Line);
                if (r.Message is not null) w.WriteString("message", r.Message);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        });
    }

    private void HandleConfigurationDone(int requestSeq)
    {
        _writer.WriteAck(requestSeq, "configurationDone");
        if (_session is null || !_session.IsLoaded) return;

        _writer.WriteEvent("thread", w =>
        {
            w.WriteString("reason", "started");
            w.WriteNumber("threadId", 1);
        });
        _session.Start();
    }

    private void HandleStackTrace(int requestSeq, JsonElement args)
    {
        var frames = _session?.SnapshotStack() ?? Array.Empty<FrameView>();
        var sourcePath = _session?.Program ?? "";
        _writer.WriteResponse(requestSeq, "stackTrace", w =>
        {
            w.WriteStartObject();
            w.WriteStartArray("stackFrames");
            foreach (var f in frames)
            {
                w.WriteStartObject();
                w.WriteNumber("id", f.Id);
                w.WriteString("name", f.Name);
                w.WriteNumber("line", f.Line);
                w.WriteNumber("column", f.Column);
                if (sourcePath.Length > 0)
                {
                    w.WriteStartObject("source");
                    w.WriteString("name", Path.GetFileName(sourcePath));
                    w.WriteString("path", sourcePath);
                    w.WriteEndObject();
                }
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteNumber("totalFrames", frames.Count);
            w.WriteEndObject();
        });
    }

    private void HandleScopes(int requestSeq, JsonElement args)
    {
        int frameId = 0;
        if (args.ValueKind == JsonValueKind.Object && args.TryGetProperty("frameId", out var fidEl)
            && fidEl.ValueKind == JsonValueKind.Number)
        {
            frameId = fidEl.GetInt32();
        }
        _writer.WriteResponse(requestSeq, "scopes", w =>
        {
            w.WriteStartObject();
            w.WriteStartArray("scopes");
            w.WriteStartObject();
            w.WriteString("name", "Locals");
            w.WriteString("presentationHint", "locals");
            // variablesReference is the frame id directly — Slice B only has a
            // single Locals scope per frame, so the mapping is 1:1.
            w.WriteNumber("variablesReference", frameId);
            w.WriteBoolean("expensive", false);
            w.WriteEndObject();
            w.WriteEndArray();
            w.WriteEndObject();
        });
    }

    private void HandleVariables(int requestSeq, JsonElement args)
    {
        int reference = 0;
        if (args.ValueKind == JsonValueKind.Object && args.TryGetProperty("variablesReference", out var refEl)
            && refEl.ValueKind == JsonValueKind.Number)
        {
            reference = refEl.GetInt32();
        }
        var locals = _session?.GetLocals(reference);
        _writer.WriteResponse(requestSeq, "variables", w =>
        {
            w.WriteStartObject();
            w.WriteStartArray("variables");
            if (locals is not null)
            {
                foreach (var (name, value) in locals)
                {
                    w.WriteStartObject();
                    w.WriteString("name", name);
                    w.WriteString("value", FormatValue(value));
                    w.WriteString("type", TypeName(value));
                    w.WriteNumber("variablesReference", 0);
                    w.WriteEndObject();
                }
            }
            w.WriteEndArray();
            w.WriteEndObject();
        });
    }

    private void HandleContinue(int requestSeq)
    {
        _session?.Continue();
        _writer.WriteResponse(requestSeq, "continue", w =>
        {
            w.WriteStartObject();
            w.WriteBoolean("allThreadsContinued", true);
            w.WriteEndObject();
        });
    }

    private void HandleStep(int requestSeq, string command, StepMode mode)
    {
        _session?.Step(mode);
        _writer.WriteAck(requestSeq, command);
    }

    private static string FormatValue(NValue v) => v switch
    {
        NUnit => "()",
        NBool b => b.Value ? "true" : "false",
        NInt i => i.Value.ToString(CultureInfo.InvariantCulture),
        NFloat f => f.Value.ToString("G17", CultureInfo.InvariantCulture),
        NString s => $"\"{s.Value}\"",
        NList list => $"list[{list.Items.Length}]",
        NRecord rec => $"record{{{rec.Fields.Count}}}",
        NVariant => "variant",
        NSeq => "seq",
        NFunc fn => $"fn(arity={fn.Arity})",
        _ => v.GetType().Name,
    };

    private static string TypeName(NValue v) => v switch
    {
        NUnit => "unit",
        NBool => "bool",
        NInt => "int",
        NFloat => "float",
        NString => "string",
        NList => "list",
        NRecord => "record",
        NVariant => "variant",
        NSeq => "seq",
        NFunc => "fn",
        _ => v.GetType().Name,
    };
}

/// <summary>
/// DAP-flavoured writer over <see cref="JsonRpcWriter"/>. DAP envelopes use the
/// LSP framing but a slightly different JSON shape: every message has a
/// <c>seq</c>, a <c>type</c> ("request" / "response" / "event"), and either a
/// <c>command</c> / <c>event</c> name. Responses additionally have a
/// <c>request_seq</c>, <c>success</c>, and an optional <c>body</c>.
/// </summary>
internal sealed class DapWriter
{
    private readonly JsonRpcWriter _rpc;
    private readonly Func<int> _nextSeq;

    public DapWriter(JsonRpcWriter rpc, Func<int> nextSeq)
    {
        _rpc = rpc;
        _nextSeq = nextSeq;
    }

    public void WriteResponse(int requestSeq, string command, Action<Utf8JsonWriter> writeBody)
    {
        var buf = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(buf))
        {
            w.WriteStartObject();
            w.WriteNumber("seq", _nextSeq());
            w.WriteString("type", "response");
            w.WriteNumber("request_seq", requestSeq);
            w.WriteBoolean("success", true);
            w.WriteString("command", command);
            w.WritePropertyName("body");
            writeBody(w);
            w.WriteEndObject();
        }
        _rpc.WriteMessage(buf);
    }

    public void WriteAck(int requestSeq, string command)
    {
        var buf = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(buf))
        {
            w.WriteStartObject();
            w.WriteNumber("seq", _nextSeq());
            w.WriteString("type", "response");
            w.WriteNumber("request_seq", requestSeq);
            w.WriteBoolean("success", true);
            w.WriteString("command", command);
            w.WriteEndObject();
        }
        _rpc.WriteMessage(buf);
    }

    public void WriteErrorResponse(int requestSeq, string command, string message)
    {
        var buf = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(buf))
        {
            w.WriteStartObject();
            w.WriteNumber("seq", _nextSeq());
            w.WriteString("type", "response");
            w.WriteNumber("request_seq", requestSeq);
            w.WriteBoolean("success", false);
            w.WriteString("command", command);
            w.WriteString("message", message);
            w.WriteEndObject();
        }
        _rpc.WriteMessage(buf);
    }

    public void WriteEvent(string @event, Action<Utf8JsonWriter> writeBody)
    {
        var buf = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(buf))
        {
            w.WriteStartObject();
            w.WriteNumber("seq", _nextSeq());
            w.WriteString("type", "event");
            w.WriteString("event", @event);
            w.WriteStartObject("body");
            writeBody(w);
            w.WriteEndObject();
            w.WriteEndObject();
        }
        _rpc.WriteMessage(buf);
    }

    public void WriteThreads(int requestSeq)
    {
        WriteResponse(requestSeq, "threads", w =>
        {
            w.WriteStartObject();
            w.WriteStartArray("threads");
            w.WriteStartObject();
            w.WriteNumber("id", 1);
            w.WriteString("name", "main");
            w.WriteEndObject();
            w.WriteEndArray();
            w.WriteEndObject();
        });
    }

    public void WriteOutputEvent(string category, string text)
    {
        WriteEvent("output", w =>
        {
            w.WriteString("category", category);
            w.WriteString("output", text);
        });
    }
}

/// <summary>
/// Forwards every chunk of text written to <c>Console.Out</c> / <c>Console.Error</c>
/// during script execution to the DAP client as an <c>output</c> event. Without
/// this redirection, script <c>print</c> calls would land on the same stdout
/// the DAP framing is using, corrupting the protocol stream.
/// </summary>
internal sealed class DapTextWriter : TextWriter
{
    private readonly DapWriter _writer;
    private readonly string _category;
    private readonly StringBuilder _buffer = new();

    public DapTextWriter(DapWriter writer, string category)
    {
        _writer = writer;
        _category = category;
    }

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value) => Write(value.ToString());

    public override void Write(string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        lock (_buffer)
        {
            _buffer.Append(value);
            FlushLines();
        }
    }

    public override void WriteLine(string? value)
    {
        lock (_buffer)
        {
            if (!string.IsNullOrEmpty(value)) _buffer.Append(value);
            _buffer.Append(Environment.NewLine);
            FlushLines();
        }
    }

    public override void Flush()
    {
        lock (_buffer)
        {
            if (_buffer.Length > 0)
            {
                _writer.WriteOutputEvent(_category, _buffer.ToString());
                _buffer.Clear();
            }
        }
    }

    private void FlushLines()
    {
        int lastNewline = -1;
        for (int i = 0; i < _buffer.Length; i++)
        {
            if (_buffer[i] == '\n') lastNewline = i;
        }
        if (lastNewline < 0) return;
        var chunk = _buffer.ToString(0, lastNewline + 1);
        _buffer.Remove(0, lastNewline + 1);
        _writer.WriteOutputEvent(_category, chunk);
    }
}
