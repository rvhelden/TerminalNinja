namespace TerminalNinja.Core.Primitives;

/// <summary>
/// Specifies how text is aligned within its container.
/// </summary>
public enum TextAlignment : byte
{
    /// <summary>Text is aligned to the left (or top for vertical alignment).</summary>
    Start,
    
    /// <summary>Text is centered horizontally (or middle for vertical alignment).</summary>
    Center,
    
    /// <summary>Text is aligned to the right (or bottom for vertical alignment).</summary>
    End
}
