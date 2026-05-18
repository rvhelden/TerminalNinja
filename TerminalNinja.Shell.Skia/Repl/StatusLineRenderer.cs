using System.Text;
using TerminalNinja.Buffers;
using TerminalNinja.Primitives;
using TerminalNinja.Shell.Language.Services;

namespace TerminalNinja.Shell.Skia;

/// <summary>
/// Renders the single-row diagnostic decoration that sits between the output
/// area and the input block: the first compile error for the current input,
/// with a caret under the offending column. (Cursor-target hover info now
/// lives in the on-demand HoverPanel — see <c>ShowCursorHover</c>.)
/// </summary>
internal static class StatusLineRenderer
{
    private static readonly Color ErrorFg = new(0xF3, 0x8B, 0xA8);

    public static void RenderDiagnostic(CellBuffer buffer, int x, int y, int width, Diagnostic diagnostic, int promptWidth, Color bg)
    {
        // Format: "  ^ <message>" with the caret roughly under the offending column.
        // Diagnostic ranges are 0-based into the input buffer; add promptWidth for screen X.
        var caretCol = promptWidth + diagnostic.Range.Start.Character;
        var line = new StringBuilder(width);
        for (var c = 0; c < caretCol && c < width; c++) line.Append(' ');
        if (caretCol < width) line.Append('^');
        line.Append(' ').Append(diagnostic.Message);
        CellPaint.DrawText(buffer, x, y, line.ToString(), width, ErrorFg, bg);
    }
}
