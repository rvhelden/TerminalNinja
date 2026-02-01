namespace TerminalNinja.Input;

/// <summary>
/// Platform-specific backend for reading console input events.
/// </summary>
public interface IInputBackend : IDisposable
{
    /// <summary>
    /// Attempts to read input events without blocking.
    /// Returns null if no events are available.
    /// </summary>
    IReadOnlyList<InputEvent>? TryRead();

    /// <summary>
    /// Reads input events, blocking until at least one event is available.
    /// </summary>
    IReadOnlyList<InputEvent> Read();

    /// <summary>
    /// Enables mouse event tracking.
    /// </summary>
    void EnableMouseTracking();

    /// <summary>
    /// Disables mouse event tracking.
    /// </summary>
    void DisableMouseTracking();
}
