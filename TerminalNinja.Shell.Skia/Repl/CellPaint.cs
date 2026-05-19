using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Shell.Skia;

/// <summary>
/// Low-level <see cref="CellBuffer"/> mutators shared by the REPL renderers:
/// text drawing with embedded SGR support, inversion / underline overlays,
/// and rectangular fills.
/// </summary>
internal static class CellPaint
{
    /// <summary>
    /// Draw <paramref name="text"/> at (<paramref name="x"/>, <paramref name="y"/>),
    /// capped to <paramref name="maxWidth"/> columns. Embedded <c>\e[...m</c> SGR
    /// escapes mutate the current fg/bg/decorations without advancing a column.
    /// </summary>
    public static void DrawText(CellBuffer buffer, int x, int y, string text, int maxWidth, Color fg, Color bg)
    {
        if ((uint)y >= (uint)buffer.Height) return;
        var currentFg = fg;
        var currentBg = bg;
        var deco = TextDecorations.None;
        int col = 0;
        int i = 0;
        while (i < text.Length && col < maxWidth)
        {
            if (text[i] == 0x1B && i + 1 < text.Length && text[i + 1] == '[')
            {
                int end = text.IndexOf('m', i + 2);
                if (end < 0) break;
                AnsiSgr.Apply(text.AsSpan(i + 2, end - (i + 2)), ref currentFg, ref currentBg, ref deco, fg, bg);
                i = end + 1;
                continue;
            }
            var cx = x + col;
            if ((uint)cx >= (uint)buffer.Width) break;
            buffer.SetChar(cx, y, text[i], currentFg, currentBg, deco);
            col++;
            i++;
        }
    }

    /// <summary>Invert fg/bg of <paramref name="length"/> cells starting at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public static void InvertCells(CellBuffer buffer, int x, int y, int length)
    {
        if ((uint)y >= (uint)buffer.Height) return;
        for (int i = 0; i < length; i++)
        {
            int cx = x + i;
            if ((uint)cx >= (uint)buffer.Width) break;
            var c = buffer.GetCell(cx, y);
            buffer.SetCell(cx, y, new Cell(c.Codepoint, c.Background, c.Foreground, c.Decorations, c.Flags));
        }
    }

    /// <summary>Add <see cref="TextDecorations.Underline"/> and force <paramref name="errFg"/> on <paramref name="length"/> cells starting at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public static void UnderlineCells(CellBuffer buffer, int x, int y, int length, Color errFg)
    {
        if ((uint)y >= (uint)buffer.Height) return;
        for (int i = 0; i < length; i++)
        {
            int cx = x + i;
            if ((uint)cx >= (uint)buffer.Width) break;
            var c = buffer.GetCell(cx, y);
            buffer.SetCell(cx, y, new Cell(c.Codepoint, errFg, c.Background, c.Decorations | TextDecorations.Underline, c.Flags));
        }
    }

    public static void FillRow(CellBuffer buffer, int x, int y, int width, Color bg)
    {
        if ((uint)y >= (uint)buffer.Height) return;
        for (var i = 0; i < width; i++)
        {
            var cx = x + i;
            if ((uint)cx >= (uint)buffer.Width) break;
            buffer.SetCell(cx, y, new Cell(' ', Color.White, bg));
        }
    }

    public static void ClearRegion(CellBuffer buffer, Rect bounds, Color bg)
    {
        for (var row = 0; row < bounds.Height; row++)
        {
            var y = bounds.Y + row;
            if ((uint)y >= (uint)buffer.Height) continue;
            for (var col = 0; col < bounds.Width; col++)
            {
                var bx = bounds.X + col;
                if ((uint)bx >= (uint)buffer.Width) continue;
                buffer.SetCell(bx, y, new Cell(' ', Color.White, bg));
            }
        }
    }
}
