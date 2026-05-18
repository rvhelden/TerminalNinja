using System.Text;
using TerminalNinja.Buffers;
using TerminalNinja.Primitives;
using TerminalNinja.Shell.Language.Services;

namespace NinjaShellUi;

/// <summary>
/// Renders the two single-row decorations between the output area and the input
/// block: the hover line (cursor-target identifier info) and the diagnostic line
/// (first compile error, with a caret under the offending column).
/// </summary>
internal static class StatusLineRenderer
{
    private static readonly Color HoverFg = new(0x89, 0xDC, 0xEB);
    private static readonly Color ErrorFg = new(0xF3, 0x8B, 0xA8);

    public static void RenderHover(CellBuffer buffer, int x, int y, int width, Hover hover, Color bg)
    {
        // Hover content can be multi-line — collapse to one row, separating blocks with " · ".
        var text = hover.Contents.Replace("\n\n", " · ").Replace('\n', ' ');
        CellPaint.DrawText(buffer, x, y, text, width, HoverFg, bg);
    }

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
