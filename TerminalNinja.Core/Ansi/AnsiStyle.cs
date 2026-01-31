using TerminalNinja.Core.Primitives;

namespace TerminalNinja.Core.Ansi;

/// <summary>
/// Tracks the current ANSI style state to minimize escape sequences.
/// </summary>
public struct AnsiStyle
{
    /// <summary>The current foreground color.</summary>
    public Color Foreground;
    
    /// <summary>The current background color.</summary>
    public Color Background;
    
    /// <summary>Whether any style has been set.</summary>
    public bool IsSet;
    
    /// <summary>
    /// Checks if the foreground color needs to be updated.
    /// </summary>
    public readonly bool NeedsForeground(Color color) => !IsSet || Foreground != color;
    
    /// <summary>
    /// Checks if the background color needs to be updated.
    /// </summary>
    public readonly bool NeedsBackground(Color color) => !IsSet || Background != color;
    
    /// <summary>
    /// Updates the tracked style.
    /// </summary>
    public void Update(Color fg, Color bg)
    {
        Foreground = fg;
        Background = bg;
        IsSet = true;
    }
    
    /// <summary>
    /// Resets the tracked style (after a reset escape sequence).
    /// </summary>
    public void Reset() => IsSet = false;
}
