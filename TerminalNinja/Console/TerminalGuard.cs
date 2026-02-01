using TerminalNinja.Ansi;

namespace TerminalNinja.Console;

/// <summary>
/// RAII-style guard that saves terminal state on creation and restores it on disposal.
/// Use with 'using' statement to ensure proper cleanup.
/// </summary>
public sealed class TerminalGuard : IDisposable
{
    private readonly AnsiWriter _writer;
    private readonly ITerminal? _terminal;
    private bool _disposed;
    
    private TerminalGuard(AnsiWriter writer, ITerminal? terminal)
    {
        _writer = writer;
        _terminal = terminal;
    }
    
    /// <summary>
    /// Enters terminal mode: enables ANSI, hides cursor, and clears screen.
    /// </summary>
    /// <param name="writer">The ANSI writer to use for setup commands.</param>
    /// <param name="terminal">The terminal abstraction to use.</param>
    /// <returns>A guard that will restore terminal state when disposed.</returns>
    public static TerminalGuard Enter(AnsiWriter writer, ITerminal terminal)
    {
        terminal.EnableAnsiMode();
        writer.HideCursor();
        writer.ClearScreen();
        writer.Flush();
        return new TerminalGuard(writer, terminal);
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
        _terminal?.DisableAnsiMode();
    }
}
