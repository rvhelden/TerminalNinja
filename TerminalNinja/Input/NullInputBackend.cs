namespace TerminalNinja.Input;

/// <summary>
/// A no-op input backend for headless/test mode.
/// All read operations return no events; mouse tracking is ignored.
/// </summary>
internal sealed class NullInputBackend : IInputBackend
{
    public IReadOnlyList<InputEvent>? TryRead() => null;

    public IReadOnlyList<InputEvent> Read() => [];

    public void EnableMouseTracking() { }

    public void DisableMouseTracking() { }

    public void Dispose() { }
}
