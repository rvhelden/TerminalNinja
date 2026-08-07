using System.Text;
using TerminalNinja.Buffers;
using TerminalNinja.Controls;
using TerminalNinja.Primitives;

namespace TerminalNinja.Rendering;

/// <summary>
/// Renders a control tree to plain text, for headless verification of layouts from
/// scripts and tests.
/// <para>
/// Draws into a private <see cref="CellBuffer"/> and reads the cells back, rather than
/// capturing the renderer's ANSI output and stripping it. The renderer positions the cursor
/// with escape sequences instead of emitting newlines, so stripped ANSI arrives as one long
/// line with the layout destroyed — which is exactly the thing a frame capture is supposed
/// to show.
/// </para>
/// </summary>
public static class FrameCapture
{
    /// <summary>
    /// Renders <paramref name="root"/> into a <paramref name="width"/> × <paramref name="height"/>
    /// buffer and returns the frame as text, one line per row, with trailing blanks trimmed.
    /// </summary>
    public static string ToText(UIElement root, int width, int height)
    {
        var buffer = RenderToBuffer(root, width, height);
        var text = new StringBuilder(width * height + height);

        for (var y = 0; y < height; y++)
        {
            var line = new StringBuilder(width);

            for (var x = 0; x < width; x++)
            {
                var codepoint = buffer[x, y].Codepoint;

                // A zero codepoint is an untouched cell, and the right-hand half of a wide
                // glyph is recorded as zero too; both read as a space.
                line.Append(codepoint == 0 ? ' ' : char.ConvertFromUtf32((int)codepoint));
            }

            text.AppendLine(line.ToString().TrimEnd());
        }

        return TrimTrailingBlankLines(text.ToString());
    }

    /// <summary>
    /// Renders <paramref name="root"/> and returns the frame with 24-bit foreground colour
    /// escape sequences, for diagnosing colour bugs from a script — the plain capture is
    /// codepoints only, so a colour problem is invisible in it.
    /// </summary>
    public static string ToAnsi(UIElement root, int width, int height)
    {
        var buffer = RenderToBuffer(root, width, height);
        var text = new StringBuilder();

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var cell = buffer[x, y];
                var fg = cell.Foreground;
                var glyph = cell.Codepoint == 0 ? " " : char.ConvertFromUtf32((int)cell.Codepoint);
                text.Append($"\e[38;2;{fg.R};{fg.G};{fg.B}m{glyph}");
            }

            text.Append("\e[0m\n");
        }

        return text.ToString();
    }

    private static CellBuffer RenderToBuffer(UIElement root, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(width, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(height, 0);

        var buffer = new CellBuffer(width, height);
        root.Render(buffer, new Rect(0, 0, width, height));
        return buffer;
    }

    private static string TrimTrailingBlankLines(string text)
    {
        var lines = text.Split(Environment.NewLine);
        var last = Array.FindLastIndex(lines, line => line.Length > 0);

        return string.Join(Environment.NewLine, lines[..(last + 1)]);
    }
}
