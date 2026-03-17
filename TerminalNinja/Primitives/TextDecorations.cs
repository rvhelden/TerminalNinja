using System.ComponentModel;
using TerminalNinja.Xaml.TypeConverters;

namespace TerminalNinja.Primitives;

/// <summary>
/// Flags enum for terminal text decorations (bold, italic, underline, etc.).
/// Maps to ANSI SGR (Select Graphic Rendition) escape sequences.
/// </summary>
[Flags]
[TypeConverter(typeof(TextDecorationsTypeConverter))]
public enum TextDecorations : byte
{
    /// <summary>No decorations.</summary>
    None = 0,

    /// <summary>Bold text (\e[1m).</summary>
    Bold = 1,

    /// <summary>Dim/faint text (\e[2m).</summary>
    Dim = 2,

    /// <summary>Italic text (\e[3m).</summary>
    Italic = 4,

    /// <summary>Underlined text (\e[4m).</summary>
    Underline = 8,

    /// <summary>Blinking text (\e[5m).</summary>
    Blink = 16,

    /// <summary>Inverse/reverse video (\e[7m).</summary>
    Inverse = 32,

    /// <summary>Strikethrough text (\e[9m).</summary>
    Strikethrough = 64,
}
