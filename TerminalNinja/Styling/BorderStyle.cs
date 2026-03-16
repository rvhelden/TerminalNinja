using System.ComponentModel;
using TerminalNinja.Primitives;
using TerminalNinja.Xaml.TypeConverters;

namespace TerminalNinja.Styling;

/// <summary>
/// Defines a border with width, color, and style.
/// </summary>
[TypeConverter(typeof(BorderTypeConverter))]
public readonly record struct BorderStyle
{
    /// <summary>Gets the border width (0 = no border, 1 = single line).</summary>
    public readonly byte Width;
    
    /// <summary>Gets the border color.</summary>
    public readonly Color Color;
    
    /// <summary>Gets the characters used to draw the border.</summary>
    public readonly BorderChars Chars;
    
    /// <summary>
    /// Creates a new border.
    /// </summary>
    public BorderStyle(byte width, Color color, BorderChars chars)
    {
        Width = width;
        Color = color;
        Chars = chars;
    }
    
    /// <summary>Gets a border with no width (invisible).</summary>
    public static readonly BorderStyle None = new(0, Color.White, BorderChars.None);
    
    /// <summary>
    /// Creates a single-line border with the specified color.
    /// </summary>
    public static BorderStyle Single(Color color) => new(1, color, BorderChars.Single);
    
    /// <summary>
    /// Creates a double-line border with the specified color.
    /// </summary>
    public static BorderStyle Double(Color color) => new(1, color, BorderChars.Double);
    
    /// <summary>
    /// Creates a rounded corner border with the specified color.
    /// </summary>
    public static BorderStyle Rounded(Color color) => new(1, color, BorderChars.Rounded);
    
    /// <summary>
    /// Creates an ASCII-compatible border with the specified color.
    /// </summary>
    public static BorderStyle Ascii(Color color) => new(1, color, BorderChars.Ascii);
    
    /// <summary>Gets whether this border should be drawn.</summary>
    public bool HasBorder => Width > 0 && !Chars.IsEmpty;
}
