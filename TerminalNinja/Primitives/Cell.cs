namespace TerminalNinja.Primitives;

/// <summary>
/// Represents a single terminal cell: a Unicode codepoint plus its colors,
/// text decorations, and rendering flags.
/// </summary>
/// <remarks>
/// <para>
/// Storing the character as a <see cref="uint"/> codepoint (rather than a UTF-16
/// <see cref="char"/>) lets the cell carry any Unicode scalar value — including
/// supplementary-plane content like emoji. Multi-codepoint grapheme clusters
/// are stored in a row-side table on <see cref="Buffers.CellBuffer"/>; the
/// cell's <see cref="Flags"/> indicate participation via <see cref="CellFlags.HasGrapheme"/>.
/// </para>
/// <para>
/// Natural alignment: 4 (Codepoint) + 4 (Foreground) + 4 (Background) + 1 + 1 + 2 padding = 16 bytes.
/// </para>
/// </remarks>
public readonly record struct Cell
{
    /// <summary>The Unicode scalar value (0 – U+10FFFF) displayed in this cell. Zero for trailing wide cells.</summary>
    public readonly uint Codepoint;

    /// <summary>Foreground (text) color.</summary>
    public readonly Color Foreground;

    /// <summary>Background color.</summary>
    public readonly Color Background;

    /// <summary>Text decorations (bold, italic, underline, etc.).</summary>
    public readonly TextDecorations Decorations;

    /// <summary>Rendering flags: wide-character lead/trail, grapheme cluster, etc.</summary>
    public readonly CellFlags Flags;

    /// <summary>Creates a cell with the specified codepoint and colors.</summary>
    public Cell(uint codepoint, Color foreground, Color background)
        : this(codepoint, foreground, background, TextDecorations.None, CellFlags.None) { }

    /// <summary>Creates a cell with the specified codepoint, colors, and decorations.</summary>
    public Cell(uint codepoint, Color foreground, Color background, TextDecorations decorations)
        : this(codepoint, foreground, background, decorations, CellFlags.None) { }

    /// <summary>Creates a cell with the specified codepoint, colors, decorations, and flags.</summary>
    public Cell(uint codepoint, Color foreground, Color background, TextDecorations decorations, CellFlags flags)
    {
        Codepoint = codepoint;
        Foreground = foreground;
        Background = background;
        Decorations = decorations;
        Flags = flags;
    }

    /// <summary>An empty cell (space, white-on-black).</summary>
    public static readonly Cell Empty = new(' ', Color.White, Color.Black);
}
