using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using TerminalNinja.Shell.Ast;
using TerminalNinja.Shell.Builtins;
using TerminalNinja.Shell.Parser;
using TerminalNinja.Shell.PowerShell;
using TerminalNinja.Shell.Repl;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Runtime.Debug;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Debug;

/// <summary>
/// One running debug session — owns the parsed script, the active breakpoint
/// set, the synthetic frame stack, and the rendezvous primitive the evaluator
/// blocks on when stopped. Implements <see cref="IDebugSink"/> so the
/// <see cref="NinjaEvaluator"/> can notify it at every AST node and every
/// user-defined call. Single-threaded runtime: one evaluator thread, one I/O
/// thread (<see cref="DapServer"/>), state guarded by <see cref="_stateLock"/>.
/// </summary>
internal sealed class DapSession : IDebugSink
{
    private readonly DapWriter _writer;
    private readonly object _stateLock = new();
    private readonly ManualResetEventSlim _resumeEvent = new(initialState: false);

    // Tracks whether the worker is currently blocked at a breakpoint or step.
    // Guards against the race where a continue/step request arrives before
    // the worker has reached its first stop — without this, the level-
    // triggered MRE would get reset on entry to OnEnter and the spurious
    // signal would be lost.
    private bool _isStopped;

    private string _program = "";
    private string _source = "";
    private ImmutableArray<Expr> _forms;

    // Per-source breakpoint table. Keyed by Path.GetFullPath of the source.
    // VS Code sends setBreakpoints *before* launch, so we accept the request
    // up front and only filter at OnEnter time against the loaded program.
    private readonly Dictionary<string, HashSet<int>> _breakpointsByPath =
        new(StringComparer.OrdinalIgnoreCase);
    private HashSet<int> _activeBreakpoints = new();

    // Synthetic call stack. _frames[0] is the bottom (script entry); _frames[^1]
    // is the currently executing frame. Each frame's CurrentExpr is refreshed
    // on every OnEnter so stackTrace can report the live position.
    private readonly List<FrameState> _frames = new();

    // Live call-depth, updated by OnEnterCall / OnLeaveCall. Used by step-over
    // and step-out to decide whether the next line transition is "shallow
    // enough" to stop on. step-in ignores depth entirely.
    private int _callDepth;
    private StepMode _stepMode = StepMode.None;
    private int _stepDepthCap;

    private int _lastStoppedLine = -1;
    private Thread? _worker;
    private bool _terminated;

    public DapSession(DapWriter writer) => _writer = writer;

    /// <summary>True once a script has been loaded via <see cref="Load"/>.</summary>
    public bool IsLoaded => _forms.Length > 0 || _source.Length > 0;

    /// <summary>Path to the launched script (DAP <c>source.path</c>).</summary>
    public string Program => _program;

    /// <summary>Read the script from disk and parse it. Returns <c>null</c> on success or an error message.</summary>
    public string? Load(string program)
    {
        if (!File.Exists(program)) return $"program file not found: {program}";
        _program = program;
        try
        {
            _source = File.ReadAllText(program);
        }
        catch (Exception ex)
        {
            return $"could not read '{program}': {ex.Message}";
        }
        try
        {
            _forms = NinjaParser.ParseScript(_source);
        }
        catch (Exception ex)
        {
            return $"parse error in '{program}': {ex.Message}";
        }
        // Now that we know which file we're running, project the breakpoints
        // that were set against it. Breakpoints for other files (currently
        // unsupported — no source() debugging) stay parked in the table but
        // never trigger.
        RebuildActiveBreakpoints();
        return null;
    }

    private void RebuildActiveBreakpoints()
    {
        lock (_stateLock)
        {
            _activeBreakpoints = new HashSet<int>();
            if (_program.Length == 0) return;
            var normalized = Path.GetFullPath(_program);
            if (_breakpointsByPath.TryGetValue(normalized, out var lines))
            {
                foreach (var l in lines) _activeBreakpoints.Add(l);
            }
        }
    }

    /// <summary>
    /// Replace the breakpoint set for <paramref name="sourcePath"/>. May be called
    /// before <see cref="Load"/>: VS Code sends setBreakpoints between the
    /// <c>initialized</c> event and <c>launch</c>. All requested lines are reported
    /// as verified; whether they actually trigger depends on whether the
    /// launched program matches this source path.
    /// </summary>
    public IReadOnlyList<BreakpointResult> SetBreakpoints(string sourcePath, IReadOnlyList<int> lines)
    {
        var normalized = sourcePath.Length > 0 ? Path.GetFullPath(sourcePath) : "";
        lock (_stateLock)
        {
            if (normalized.Length == 0)
            {
                // No source path → can't associate; ignore.
            }
            else if (lines.Count == 0)
            {
                _breakpointsByPath.Remove(normalized);
            }
            else
            {
                _breakpointsByPath[normalized] = new HashSet<int>(lines);
            }
        }
        RebuildActiveBreakpoints();

        var results = new BreakpointResult[lines.Count];
        for (int i = 0; i < lines.Count; i++)
        {
            results[i] = new BreakpointResult(true, lines[i], null);
        }
        return results;
    }

    /// <summary>Start the evaluator on a worker thread. Must be called after <see cref="Load"/>.</summary>
    public void Start()
    {
        _frames.Add(new FrameState { Name = "(script)", CallSite = Span.None });
        _worker = new Thread(Run) { IsBackground = true, Name = "ninja-dap-worker" };
        _worker.Start();
    }

    /// <summary>Wait for the worker to finish (bounded), so the final terminated event makes it out.</summary>
    public void Join(TimeSpan timeout) => _worker?.Join(timeout);

    /// <summary>Resume execution after a stop. No-op if not currently stopped.</summary>
    public void Continue()
    {
        lock (_stateLock)
        {
            if (!_isStopped) return;
            _stepMode = StepMode.None;
        }
        _resumeEvent.Set();
    }

    /// <summary>
    /// Resume execution under a step-mode constraint. Arms the predicate
    /// <see cref="IDebugSink.OnEnter"/> uses to decide whether the *next* line
    /// transition should trigger a stop. No-op if not currently stopped.
    /// </summary>
    public void Step(StepMode mode)
    {
        if (mode == StepMode.None) { Continue(); return; }
        lock (_stateLock)
        {
            if (!_isStopped) return;
            _stepMode = mode;
            // Cap is captured at the moment of the step request so step-over
            // and step-out can be compared against the *current* call depth
            // as the script progresses.
            _stepDepthCap = _callDepth;
        }
        _resumeEvent.Set();
    }

    /// <summary>Snapshot the current frame stack for a stackTrace response.</summary>
    public IReadOnlyList<FrameView> SnapshotStack()
    {
        lock (_stateLock)
        {
            var list = new List<FrameView>(_frames.Count);
            // DAP frame 0 is the topmost (innermost), which is _frames[^1].
            for (int i = _frames.Count - 1; i >= 0; i--)
            {
                var f = _frames[i];
                var span = f.CurrentExpr?.Span ?? f.CallSite;
                list.Add(new FrameView(
                    Id: i + 1,
                    Name: f.Name,
                    Line: span.StartLine,
                    Column: span.StartColumn));
            }
            return list;
        }
    }

    /// <summary>Look up a frame's locals snapshot for a variables response.</summary>
    public IReadOnlyList<(string Name, NValue Value)>? GetLocals(int frameId)
    {
        lock (_stateLock)
        {
            // FrameId is 1-based, matching SnapshotStack's emission.
            int index = frameId - 1;
            if (index < 0 || index >= _frames.Count) return null;
            var env = _frames[index].CurrentEnv;
            if (env is null) return Array.Empty<(string, NValue)>();
            var list = new List<(string, NValue)>();
            foreach (var kv in env.Bindings) list.Add((kv.Key, kv.Value));
            return list;
        }
    }

    /// <summary>Tear down the session. Releases any thread blocked on a stop.</summary>
    public void Dispose()
    {
        _terminated = true;
        _resumeEvent.Set();
    }

    // ----- IDebugSink ----------------------------------------------------

    void IDebugSink.OnEnter(Expr expr, Env env)
    {
        bool shouldStop;
        string stopReason = "";
        int stopLine;
        lock (_stateLock)
        {
            // Refresh the top frame's live position so stackTrace can read it.
            if (_frames.Count > 0)
            {
                var top = _frames[^1];
                top.CurrentExpr = expr;
                top.CurrentEnv = env;
                _frames[^1] = top;
            }

            stopLine = expr.Span.StartLine;
            bool lineTransition = stopLine > 0 && stopLine != _lastStoppedLine;
            bool bpHit = lineTransition && _activeBreakpoints.Contains(stopLine);
            bool stepHit = lineTransition && _stepMode switch
            {
                StepMode.In => true,
                StepMode.Over => _callDepth <= _stepDepthCap,
                StepMode.Out => _callDepth < _stepDepthCap,
                _ => false,
            };
            shouldStop = !_terminated && (bpHit || stepHit);
            if (shouldStop)
            {
                stopReason = bpHit ? "breakpoint" : "step";
                // Consume the step so the next OnEnter doesn't re-trigger on
                // every visited node — a fresh step request will re-arm.
                _stepMode = StepMode.None;
            }
            _lastStoppedLine = stopLine;
        }
        if (!shouldStop) return;

        lock (_stateLock)
        {
            _isStopped = true;
            _resumeEvent.Reset();
        }
        _writer.WriteEvent("stopped", w =>
        {
            w.WriteString("reason", stopReason);
            w.WriteNumber("threadId", 1);
            w.WriteBoolean("allThreadsStopped", true);
        });
        _resumeEvent.Wait();
        lock (_stateLock)
        {
            _isStopped = false;
        }
    }

    void IDebugSink.OnEnterCall(string name, Span callSite)
    {
        lock (_stateLock)
        {
            _frames.Add(new FrameState { Name = name, CallSite = callSite });
            _callDepth++;
        }
    }

    void IDebugSink.OnLeaveCall()
    {
        lock (_stateLock)
        {
            if (_frames.Count > 1) _frames.RemoveAt(_frames.Count - 1);
            if (_callDepth > 0) _callDepth--;
        }
    }

    // ----- Worker --------------------------------------------------------

    private void Run()
    {
        var stdoutCapture = new DapTextWriter(_writer, "stdout");
        var stderrCapture = new DapTextWriter(_writer, "stderr");
        var origOut = Console.Out;
        var origErr = Console.Error;
        Console.SetOut(stdoutCapture);
        Console.SetError(stderrCapture);

        int exitCode;
        try
        {
            var env = BuiltinRegistry.CreateDefaultEnv();
            if (PwshBridge.IsAvailable) env = PwshBridge.Install(env);

            using (new DebugScope(this))
            {
                NValue last = NUnit.Instance;
                foreach (var form in _forms)
                {
                    var r = NinjaEvaluator.EvalTop(form, env);
                    env = r.Env;
                    last = r.Value;
                }
                var rendered = Printer.Format(last);
                if (!string.IsNullOrEmpty(rendered)) Console.Out.WriteLine(rendered);
            }
            exitCode = 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            exitCode = 1;
        }
        finally
        {
            stdoutCapture.Flush();
            stderrCapture.Flush();
            Console.SetOut(origOut);
            Console.SetError(origErr);
        }

        _writer.WriteEvent("exited", w => w.WriteNumber("exitCode", exitCode));
        _writer.WriteEvent("terminated", static _ => { });
    }

    private struct FrameState
    {
        public string Name;
        public Span CallSite;
        public Expr? CurrentExpr;
        public Env? CurrentEnv;
    }
}

internal readonly record struct BreakpointResult(bool Verified, int Line, string? Message);

internal readonly record struct FrameView(int Id, string Name, int Line, int Column);

internal enum StepMode
{
    /// <summary>Running freely — only breakpoints can stop execution.</summary>
    None,
    /// <summary>step-in: stop on the next AST node, any call depth.</summary>
    In,
    /// <summary>step-over: stop on the next line transition at depth ≤ depth-at-step.</summary>
    Over,
    /// <summary>step-out: stop on the next line transition at depth &lt; depth-at-step.</summary>
    Out,
}
