using TerminalNinja.Primitives;

namespace TerminalNinja.Ansi;

/// <summary>
/// Tracks the current ANSI style state to minimize escape sequences.
/// </summary>
public struct AnsiStyle
{
    /// <summary>The current foreground color.</summary>
    public Color Foreground;
    
    /// <summary>The current background color.</summary>
    public Color Background;
    
    /// <summary>The current text decorations.</summary>
    public TextDecorations Decorations;
    
    /// <summary>Whether foreground has been set.</summary>
    public bool ForegroundSet;
    
    /// <summary>Whether background has been set.</summary>
    public bool BackgroundSet;
    
    /// <summary>Whether decorations have been set.</summary>
    public bool DecorationsSet;
    
    /// <summary>
    /// Checks if the foreground color needs to be updated.
    /// </summary>
    public readonly bool NeedsForeground(Color color) => !ForegroundSet || Foreground != color;
    
    /// <summary>
    /// Checks if the background color needs to be updated.
    /// </summary>
    public readonly bool NeedsBackground(Color color) => !BackgroundSet || Background != color;
    
    /// <summary>
    /// Checks if the decorations need to be updated.
    /// </summary>
    public readonly bool NeedsDecorations(TextDecorations decorations) => !DecorationsSet || Decorations != decorations;
    
    /// <summary>
    /// Updates the tracked style.
    /// </summary>
    public void Update(Color fg, Color bg)
    {
        Foreground = fg;
        Background = bg;
        ForegroundSet = true;
        BackgroundSet = true;
    }
    
    /// <summary>
    /// Resets the tracked style (after a reset escape sequence).
    /// </summary>
    public void Reset()
    {
        ForegroundSet = false;
        BackgroundSet = false;
        DecorationsSet = false;
        Decorations = TextDecorations.None;
    }
}
