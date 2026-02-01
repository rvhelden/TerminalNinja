namespace TerminalNinja.Core.Input;

/// <summary>
/// Event arguments for key events that can be handled by the application.
/// </summary>
public sealed class KeyEventArgs
{
    /// <summary>
    /// Gets or sets whether the event has been handled.
    /// If true, default handling will be skipped.
    /// </summary>
    public bool Handled { get; set; }
}
