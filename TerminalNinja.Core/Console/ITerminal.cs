namespace TerminalNinja.Core.Console;

/// <summary>
/// Abstraction for terminal interaction, enabling dependency injection and testing.
/// </summary>
public interface ITerminal
{
    /// <summary>Gets the current terminal width in columns.</summary>
    int Width { get; }
    
    /// <summary>Gets the current terminal height in rows.</summary>
    int Height { get; }
    
    /// <summary>Opens the standard output stream for direct writing.</summary>
    Stream OpenOutput();
    
    /// <summary>Enables ANSI escape sequence processing.</summary>
    /// <returns>True if successful, false otherwise.</returns>
    bool EnableAnsiMode();
    
    /// <summary>Disables ANSI escape sequence processing and restores original mode.</summary>
    void DisableAnsiMode();
}
