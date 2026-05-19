namespace TerminalNinja.Shell.Runtime.Debug;

/// <summary>
/// RAII-style installer for a thread-local <see cref="IDebugSink"/>. The DAP
/// session creates its evaluator thread, opens a <see cref="DebugScope"/>, and
/// runs the script; on dispose the previous sink (usually <c>null</c>) is
/// restored. Wiring is per-thread because <see cref="NinjaEvaluator"/> stores
/// the sink in a <c>[ThreadStatic]</c> field — see <see cref="NinjaEvaluator.CurrentSink"/>.
/// </summary>
internal readonly struct DebugScope : IDisposable
{
    private readonly IDebugSink? _previous;

    public DebugScope(IDebugSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _previous = NinjaEvaluator.CurrentSink;
        NinjaEvaluator.CurrentSink = sink;
    }

    public void Dispose() => NinjaEvaluator.CurrentSink = _previous;
}
