using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using TerminalNinja.Shell.Debug;
using TerminalNinja.Shell.Language.Protocol;

namespace TerminalNinja.Shell.Tests.Unit.Debug;

/// <summary>
/// In-process integration tests for the DAP adapter. Drives <see cref="DapServer"/>
/// over a pair of blocking streams so the test thread can send a request and
/// then wait for events the server emits asynchronously (stopped, terminated).
/// </summary>
public class DapServerTests
{
    [Test]
    public async Task LaunchSimpleScript_RunsToTermination()
    {
        var script = WriteTempScript("let a = 10\nlet b = a + 5\nprintln(b)\n");
        using var driver = StartServer();

        driver.Send(1, "initialize", new { clientID = "test" });
        driver.Send(2, "launch", new { program = script });
        driver.Send(3, "configurationDone", new { });

        // Initial handshake — response, initialized event, then more responses.
        var initResp = driver.ReadUntil(m => m.IsResponseTo("initialize"));
        await Assert.That(initResp.RootElement.GetProperty("success").GetBoolean()).IsTrue();

        // ReadUntil throws on timeout, so reaching this line implies terminated was emitted.
        driver.ReadUntil(m => m.IsEvent("terminated"));
    }

    [Test]
    public async Task Breakpoint_StopsThenContinues()
    {
        var script = WriteTempScript("let a = 10\nlet b = a + 5\nprintln(b)\n");
        using var driver = StartServer();

        driver.Send(1, "initialize", new { clientID = "test" });
        driver.Send(2, "launch", new { program = script });
        driver.Send(3, "setBreakpoints", new
        {
            source = new { path = script },
            breakpoints = new[] { new { line = 2 } }
        });
        driver.Send(4, "configurationDone", new { });

        var stopped = driver.ReadUntil(m => m.IsEvent("stopped"));
        var reason = stopped.RootElement.GetProperty("body").GetProperty("reason").GetString();
        await Assert.That(reason).IsEqualTo("breakpoint");

        driver.Send(5, "continue", new { threadId = 1 });
        driver.ReadUntil(m => m.IsEvent("terminated"));
    }

    [Test]
    public async Task StackTrace_AtBreakpoint_ReportsCurrentLine()
    {
        var script = WriteTempScript("let a = 10\nlet b = a + 5\nprintln(b)\n");
        using var driver = StartServer();

        driver.Send(1, "initialize", new { clientID = "test" });
        driver.Send(2, "launch", new { program = script });
        driver.Send(3, "setBreakpoints", new
        {
            source = new { path = script },
            breakpoints = new[] { new { line = 2 } }
        });
        driver.Send(4, "configurationDone", new { });
        driver.ReadUntil(m => m.IsEvent("stopped"));

        driver.Send(5, "stackTrace", new { threadId = 1 });
        var stackResp = driver.ReadUntil(m => m.IsResponseTo("stackTrace"));
        var frames = stackResp.RootElement.GetProperty("body").GetProperty("stackFrames");
        await Assert.That(frames.GetArrayLength()).IsGreaterThan(0);
        // Top frame's line should be the breakpoint line (or close to it — the
        // *first* statement on line 2 might be an inner sub-expression).
        var topLine = frames[0].GetProperty("line").GetInt32();
        await Assert.That(topLine).IsEqualTo(2);

        driver.Send(6, "continue", new { threadId = 1 });
        driver.ReadUntil(m => m.IsEvent("terminated"));
    }

    [Test]
    public async Task Variables_AtBreakpoint_IncludesBindingFromPrecedingStatement()
    {
        var script = WriteTempScript("let a = 10\nlet b = a + 5\nprintln(b)\n");
        using var driver = StartServer();

        driver.Send(1, "initialize", new { clientID = "test" });
        driver.Send(2, "launch", new { program = script });
        // Break on line 3 so both `a` and `b` are already bound.
        driver.Send(3, "setBreakpoints", new
        {
            source = new { path = script },
            breakpoints = new[] { new { line = 3 } }
        });
        driver.Send(4, "configurationDone", new { });
        driver.ReadUntil(m => m.IsEvent("stopped"));

        driver.Send(5, "stackTrace", new { threadId = 1 });
        var stackResp = driver.ReadUntil(m => m.IsResponseTo("stackTrace"));
        var topFrameId = stackResp.RootElement.GetProperty("body")
            .GetProperty("stackFrames")[0].GetProperty("id").GetInt32();

        driver.Send(6, "variables", new { variablesReference = topFrameId });
        var varsResp = driver.ReadUntil(m => m.IsResponseTo("variables"));
        var vars = varsResp.RootElement.GetProperty("body").GetProperty("variables");
        var names = new List<string>();
        foreach (var v in vars.EnumerateArray()) names.Add(v.GetProperty("name").GetString()!);
        await Assert.That(names).Contains("a");
        await Assert.That(names).Contains("b");

        driver.Send(7, "continue", new { threadId = 1 });
        driver.ReadUntil(m => m.IsEvent("terminated"));
    }

    [Test]
    public async Task SetBreakpoints_BeforeLaunch_StillTriggers()
    {
        // VS Code's real client sends setBreakpoints *between* the `initialized`
        // event and `launch`. Our earlier tests sent it after launch — this
        // regression test pins the production ordering.
        var script = WriteTempScript("let a = 10\nlet b = a + 5\nprintln(b)\n");
        using var driver = StartServer();

        driver.Send(1, "initialize", new { clientID = "test" });
        driver.ReadUntil(m => m.IsEvent("initialized"));
        driver.Send(2, "setBreakpoints", new
        {
            source = new { path = script },
            breakpoints = new[] { new { line = 2 } }
        });
        driver.Send(3, "launch", new { program = script });
        driver.Send(4, "configurationDone", new { });

        var stopped = driver.ReadUntil(m => m.IsEvent("stopped"));
        var reason = stopped.RootElement.GetProperty("body").GetProperty("reason").GetString();
        await Assert.That(reason).IsEqualTo("breakpoint");

        driver.Send(5, "continue", new { threadId = 1 });
        driver.ReadUntil(m => m.IsEvent("terminated"));
    }

    [Test]
    public async Task Next_StopsOnNextTopLevelStatement()
    {
        var script = WriteTempScript("let a = 1\nlet b = 2\nlet c = 3\n");
        using var driver = StartServer();

        driver.Send(1, "initialize", new { clientID = "test" });
        driver.Send(2, "launch", new { program = script });
        driver.Send(3, "setBreakpoints", new
        {
            source = new { path = script },
            breakpoints = new[] { new { line = 1 } }
        });
        driver.Send(4, "configurationDone", new { });
        driver.ReadUntil(m => m.IsEvent("stopped"));

        driver.Send(5, "next", new { threadId = 1 });
        var stoppedAgain = driver.ReadUntil(m => m.IsEvent("stopped"));
        var reason = stoppedAgain.RootElement.GetProperty("body").GetProperty("reason").GetString();
        await Assert.That(reason).IsEqualTo("step");

        driver.Send(6, "stackTrace", new { threadId = 1 });
        var stack = driver.ReadUntil(m => m.IsResponseTo("stackTrace"));
        var topLine = stack.RootElement.GetProperty("body")
            .GetProperty("stackFrames")[0].GetProperty("line").GetInt32();
        await Assert.That(topLine).IsEqualTo(2);

        driver.Send(7, "continue", new { threadId = 1 });
        driver.ReadUntil(m => m.IsEvent("terminated"));
    }

    [Test]
    public async Task StepIn_DescendsIntoUserFunctionBody()
    {
        // `inc` is defined on line 1; call on line 2; result printed on line 3.
        var script = WriteTempScript("let inc = (x) => x + 1\nlet y = inc(5)\nprintln(y)\n");
        using var driver = StartServer();

        driver.Send(1, "initialize", new { clientID = "test" });
        driver.Send(2, "launch", new { program = script });
        // Break on line 2 — the call site for inc.
        driver.Send(3, "setBreakpoints", new
        {
            source = new { path = script },
            breakpoints = new[] { new { line = 2 } }
        });
        driver.Send(4, "configurationDone", new { });
        driver.ReadUntil(m => m.IsEvent("stopped"));

        driver.Send(5, "stepIn", new { threadId = 1 });
        driver.ReadUntil(m => m.IsEvent("stopped"));

        driver.Send(6, "stackTrace", new { threadId = 1 });
        var stack = driver.ReadUntil(m => m.IsResponseTo("stackTrace"));
        var frames = stack.RootElement.GetProperty("body").GetProperty("stackFrames");
        // After stepping in, the stack should include the inc call frame on
        // top of the script frame.
        await Assert.That(frames.GetArrayLength()).IsGreaterThanOrEqualTo(2);
        var topName = frames[0].GetProperty("name").GetString();
        await Assert.That(topName).IsEqualTo("inc");

        driver.Send(7, "continue", new { threadId = 1 });
        driver.ReadUntil(m => m.IsEvent("terminated"));
    }

    [Test]
    public async Task StepOut_ReturnsToCallerFrame()
    {
        var script = WriteTempScript("let inc = (x) => x + 1\nlet y = inc(5)\nprintln(y)\n");
        using var driver = StartServer();

        driver.Send(1, "initialize", new { clientID = "test" });
        driver.Send(2, "launch", new { program = script });
        driver.Send(3, "setBreakpoints", new
        {
            source = new { path = script },
            breakpoints = new[] { new { line = 2 } }
        });
        driver.Send(4, "configurationDone", new { });
        driver.ReadUntil(m => m.IsEvent("stopped"));

        // Step into the function call, then step out — should land back in
        // the script frame on line 3.
        driver.Send(5, "stepIn", new { threadId = 1 });
        driver.ReadUntil(m => m.IsEvent("stopped"));

        driver.Send(6, "stepOut", new { threadId = 1 });
        driver.ReadUntil(m => m.IsEvent("stopped"));

        driver.Send(7, "stackTrace", new { threadId = 1 });
        var stack = driver.ReadUntil(m => m.IsResponseTo("stackTrace"));
        var frames = stack.RootElement.GetProperty("body").GetProperty("stackFrames");
        // Back at the script frame.
        await Assert.That(frames.GetArrayLength()).IsEqualTo(1);
        var topName = frames[0].GetProperty("name").GetString();
        await Assert.That(topName).IsEqualTo("(script)");

        driver.Send(8, "continue", new { threadId = 1 });
        driver.ReadUntil(m => m.IsEvent("terminated"));
    }

    // ─── helpers ────────────────────────────────────────────────────────────

    private static string WriteTempScript(string body)
    {
        var path = Path.Combine(Path.GetTempPath(), $"dap-test-{Guid.NewGuid():N}.ninja");
        File.WriteAllText(path, body);
        return path;
    }

    private static DapDriver StartServer()
    {
        var input = new BlockingPipeStream();
        var output = new BlockingPipeStream();
        var thread = new Thread(() => new DapServer().Run(input, output))
        {
            IsBackground = true,
            Name = "dap-server-test"
        };
        thread.Start();
        return new DapDriver(input, output, thread);
    }

    private sealed class DapDriver : IDisposable
    {
        private readonly BlockingPipeStream _input;
        private readonly BlockingPipeStream _output;
        private readonly Thread _thread;
        private readonly JsonRpcReader _reader;
        private readonly JsonRpcWriter _writer;

        public DapDriver(BlockingPipeStream input, BlockingPipeStream output, Thread thread)
        {
            _input = input;
            _output = output;
            _thread = thread;
            _reader = new JsonRpcReader(output);
            _writer = new JsonRpcWriter(input);
        }

        public void Send(int seq, string command, object args)
        {
            var json = JsonSerializer.Serialize(new
            {
                seq,
                type = "request",
                command,
                arguments = args
            });
            _writer.WriteMessage(Encoding.UTF8.GetBytes(json));
        }

        /// <summary>Read messages from the server until <paramref name="match"/> returns true. Times out after 10 s.</summary>
        public DapMessage ReadUntil(Func<DapMessage, bool> match)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                var doc = _reader.ReadMessage();
                if (doc is null) throw new EndOfStreamException("server closed output stream");
                var msg = new DapMessage(doc);
                if (match(msg)) return msg;
                msg.Dispose();
            }
            throw new TimeoutException("timed out waiting for matching DAP message");
        }

        public void Dispose()
        {
            _input.Complete();
            _thread.Join(TimeSpan.FromSeconds(5));
        }
    }

    private readonly record struct DapMessage(JsonDocument Document) : IDisposable
    {
        public JsonElement RootElement => Document.RootElement;

        public bool IsResponseTo(string command) =>
            RootElement.TryGetProperty("type", out var t) && t.GetString() == "response"
            && RootElement.TryGetProperty("command", out var c) && c.GetString() == command;

        public bool IsEvent(string name) =>
            RootElement.TryGetProperty("type", out var t) && t.GetString() == "event"
            && RootElement.TryGetProperty("event", out var e) && e.GetString() == name;

        public void Dispose() => Document.Dispose();
    }

    /// <summary>
    /// Stream-shaped queue: writers append byte chunks; readers consume in
    /// order, blocking when the queue is empty. Half-duplex — one end writes,
    /// the other reads. Used to pipe a real server thread through synchronous
    /// test-thread send/receive without spawning a child process.
    /// </summary>
    private sealed class BlockingPipeStream : Stream
    {
        private readonly BlockingCollection<byte[]> _chunks = new();
        private byte[]? _current;
        private int _pos;

        public void Complete() => _chunks.CompleteAdding();

        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_current is null || _pos >= _current.Length)
            {
                try { _current = _chunks.Take(); _pos = 0; }
                catch (InvalidOperationException) { return 0; }
            }
            int n = Math.Min(count, _current.Length - _pos);
            Array.Copy(_current, _pos, buffer, offset, n);
            _pos += n;
            return n;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var copy = new byte[count];
            Array.Copy(buffer, offset, copy, 0, count);
            _chunks.Add(copy);
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
