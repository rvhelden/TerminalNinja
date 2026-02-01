namespace TerminalNinja.Console;

/// <summary>
/// System terminal implementation that delegates to the static Terminal class.
/// </summary>
public sealed class SystemTerminal : ITerminal
{
    /// <summary>Singleton instance for production use.</summary>
    public static readonly SystemTerminal Instance = new();
    
    private SystemTerminal() { }
    
    /// <inheritdoc/>
    public int Width => Terminal.Width;
    
    /// <inheritdoc/>
    public int Height => Terminal.Height;
    
    /// <inheritdoc/>
    public Stream OpenOutput() => Terminal.OpenStdout();
    
    /// <inheritdoc/>
    public bool EnableAnsiMode() => Terminal.EnableAnsiMode();
    
    /// <inheritdoc/>
    public void DisableAnsiMode() => Terminal.DisableAnsiMode();
}
