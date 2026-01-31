using TerminalNinja.Core.Ansi;

namespace TerminalNinja.Core.Console;

/// <summary>
/// RAII-style guard that saves terminal state on creation and restores it on disposal.
/// Use with 'using' statement to ensure proper cleanup.
/// </summary>
public sealed class TerminalGuard : IDisposable
{
    private readonly AnsiWriter _writer;
    private bool _disposed;
    
    private TerminalGuard(AnsiWriter writer)
    {
        _writer = writer;
    }
    
    /// <summary>
    /// Enters terminal mode: enables ANSI, hides cursor, and clears screen.
    /// </summary>
    /// <param name="writer">The ANSI writer to use for setup commands.</param>
    /// <returns>A guard that will restore terminal state when disposed.</returns>
    public static TerminalGuard Enter(AnsiWriter writer)
    {
        Terminal.EnableAnsiMode();
        writer.HideCursor();
        writer.ClearScreen();
        writer.Flush();
        return new TerminalGuard(writer);
    }
    
    /// <summary>
    /// Restores terminal state: shows cursor, resets attributes, and disables ANSI mode.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _writer.Reset();
        _writer.ShowCursor();
        _writer.Flush();
        Terminal.DisableAnsiMode();
    }
}
