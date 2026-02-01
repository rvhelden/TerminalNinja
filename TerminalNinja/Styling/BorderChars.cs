namespace TerminalNinja.Styling;

/// <summary>
/// Defines the characters used to draw a border (12 bytes - 6 chars × 2 bytes).
/// </summary>
public readonly record struct BorderChars
{
    /// <summary>Gets the top-left corner character.</summary>
    public readonly char TopLeft;
    
    /// <summary>Gets the top-right corner character.</summary>
    public readonly char TopRight;
    
    /// <summary>Gets the bottom-left corner character.</summary>
    public readonly char BottomLeft;
    
    /// <summary>Gets the bottom-right corner character.</summary>
    public readonly char BottomRight;
    
    /// <summary>Gets the horizontal line character.</summary>
    public readonly char Horizontal;
    
    /// <summary>Gets the vertical line character.</summary>
    public readonly char Vertical;
    
    /// <summary>
    /// Creates a new border character set.
    /// </summary>
    public BorderChars(char topLeft, char topRight, char bottomLeft, char bottomRight, char horizontal, char vertical)
    {
        TopLeft = topLeft;
        TopRight = topRight;
        BottomLeft = bottomLeft;
        BottomRight = bottomRight;
        Horizontal = horizontal;
        Vertical = vertical;
    }
    
    /// <summary>Gets a single-line box drawing border.</summary>
    public static readonly BorderChars Single = new('┌', '┐', '└', '┘', '─', '│');
    
    /// <summary>Gets a double-line box drawing border.</summary>
    public static readonly BorderChars Double = new('╔', '╗', '╚', '╝', '═', '║');
    
    /// <summary>Gets a rounded corner border.</summary>
    public static readonly BorderChars Rounded = new('╭', '╮', '╰', '╯', '─', '│');
    
    /// <summary>Gets an ASCII-compatible border using +, -, and |.</summary>
    public static readonly BorderChars Ascii = new('+', '+', '+', '+', '-', '|');
    
    /// <summary>Gets an empty border (no characters).</summary>
    public static readonly BorderChars None = new('\0', '\0', '\0', '\0', '\0', '\0');
    
    /// <summary>Gets whether this border has any characters defined.</summary>
    public bool IsEmpty => TopLeft == '\0';
}
