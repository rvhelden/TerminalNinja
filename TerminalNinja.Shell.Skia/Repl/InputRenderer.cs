using TerminalNinja.Buffers;
using TerminalNinja.Highlighting;
using TerminalNinja.Primitives;
using TerminalNinja.Shell.Language.Services;

namespace TerminalNinja.Shell.Skia;

/// <summary>
/// Renders the multi-line input block: prompt on row 0, continuation prompts on rows 1..n,
/// syntax-highlighted token colours, diagnostic underlines, selection inversion, and the
/// cursor cell.
/// </summary>
internal sealed class InputRenderer
{
    private static readonly Color DimContinuationFg = new(0x6C, 0x70, 0x86);
    private static readonly Color GhostFg = new(0x58, 0x5B, 0x70);
    private static readonly Color ErrorFg = new(0xF3, 0x8B, 0xA8);

    private readonly InputBuffer _input;
    private readonly SelectionModel _selection;

    public InputRenderer(InputBuffer input, SelectionModel selection)
    {
        _input = input;
        _selection = selection;
    }

    public void Render(
        CellBuffer buffer,
        in ReplLayout layout,
        string prompt,
        string continuationPrompt,
        string? highlightLanguage,
        SyntaxTheme theme,
        IReadOnlyList<Diagnostic> diagnostics,
        Color fg,
        Color bg,
        string ghostSuffix = "")
    {
        var allTokens = TokenizeOrNull(highlightLanguage);
        var text = _input.Text;
        var (cursorLine, cursorCol) = _input.CursorToLineCol(_input.CursorCol);

        var promptWidth = layout.PromptWidth;
        var inputX = layout.InputX;
        var inputWidth = layout.InputWidth;
        var topY = layout.InputTopY;
        var rowCount = layout.InputLines;
        var width = layout.Bounds.Width;
        var x = layout.Bounds.X;

        // Walk the buffer line by line in lockstep with the row we render on. Each line's
        // start offset is its byte index in `text`; render rowCount rows even if the
        // logical input has more lines (clamped — overflow lines are dropped from view).
        var offset = 0;
        for (var r = 0; r < rowCount; r++)
        {
            var y = topY + r;
            if ((uint)y >= (uint)buffer.Height) break;

            var lineEnd = offset;
            while (lineEnd < text.Length && text[lineEnd] != '\n') lineEnd++;

            var prefix = r == 0 ? prompt : continuationPrompt;
            var prefixFg = r == 0 ? fg : DimContinuationFg;
            CellPaint.DrawText(buffer, x, y, prefix.PadRight(promptWidth), width, prefixFg, bg);

            var lineText = text.Substring(offset, lineEnd - offset);
            DrawHighlightedLine(buffer, inputX, y, lineText, offset, allTokens, inputWidth, theme, fg, bg);

            // Error underlines go on after highlighting and before selection
            // inversion — selection visually wins over the underline (you can
            // still see the squiggle, just inverted with the rest of the run).
            ApplyDiagnosticUnderlines(buffer, inputX, y, lineText.Length, r, inputWidth, diagnostics);

            if (_selection.Region == SelectionRegion.Input
                && _selection.TryGetSelectedColsForRow(r, lineText.Length, out var selStart, out var selEnd))
            {
                CellPaint.InvertCells(buffer, inputX + selStart, y, Math.Min(selEnd, inputWidth) - selStart);
            }

            // Ghost text (history autosuggestion). Painted on the cursor row right after the
            // line text, before cursor inversion, so the cursor lands on the first ghost cell
            // and inverts it naturally. Caller is responsible for only passing a non-empty
            // ghostSuffix when the cursor is at end-of-buffer and the buffer is single-line.
            if (ghostSuffix.Length > 0 && cursorLine == r)
            {
                var ghostX = inputX + lineText.Length;
                var availableWidth = inputX + inputWidth - ghostX;
                if (availableWidth > 0)
                {
                    CellPaint.DrawText(buffer, ghostX, y, ghostSuffix, availableWidth, GhostFg, bg);
                }
            }

            if (cursorLine == r)
            {
                var cursorX = inputX + Math.Min(cursorCol, Math.Max(0, inputWidth - 1));
                if (cursorX >= inputX && cursorX < inputX + inputWidth && (uint)cursorX < (uint)buffer.Width)
                {
                    var cell = buffer.GetCell(cursorX, y);
                    buffer.SetCell(cursorX, y, new Cell(cell.Codepoint, cell.Background, cell.Foreground, cell.Decorations, cell.Flags));
                }
            }

            offset = lineEnd < text.Length ? lineEnd + 1 : lineEnd;
        }
    }

    private IReadOnlyList<SyntaxToken>? TokenizeOrNull(string? highlightLanguage)
    {
        if (highlightLanguage is null) return null;
        if (_input.IsEmpty) return null;
        if (!SyntaxHighlighterRegistry.TryGet(highlightLanguage, out var hl)) return null;
        return hl.Tokenize(_input.Text);
    }

    /// <summary>
    /// Render one row of the input — the substring <paramref name="lineText"/> that lives
    /// at <paramref name="lineOffset"/> within the full buffer — with highlighted token
    /// colours. Tokens were produced over the whole buffer (so multi-line constructs like
    /// strings highlight correctly across rows); per-row rendering filters tokens to those
    /// that overlap this line's offset range.
    /// </summary>
    private static void DrawHighlightedLine(
        CellBuffer buffer, int x, int y,
        string lineText, int lineOffset,
        IReadOnlyList<SyntaxToken>? tokens,
        int maxWidth, SyntaxTheme theme,
        Color fallbackFg, Color bg)
    {
        if (lineText.Length == 0 || maxWidth <= 0 || (uint)y >= (uint)buffer.Height) return;

        if (tokens is null)
        {
            CellPaint.DrawText(buffer, x, y, lineText, maxWidth, fallbackFg, bg);
            return;
        }

        var tokenIdx = 0;
        for (var i = 0; i < lineText.Length && i < maxWidth; i++)
        {
            var absoluteOffset = lineOffset + i;

            while (tokenIdx < tokens.Count && tokens[tokenIdx].Start + tokens[tokenIdx].Length <= absoluteOffset)
            {
                tokenIdx++;
            }

            var fg = fallbackFg;
            if (tokenIdx < tokens.Count)
            {
                var t = tokens[tokenIdx];
                if (absoluteOffset >= t.Start && absoluteOffset < t.Start + t.Length)
                {
                    fg = theme.GetColor(t.Kind);
                }
            }

            var cx = x + i;
            if ((uint)cx >= (uint)buffer.Width) break;
            buffer.SetChar(cx, y, lineText[i], fg, bg);
        }
    }

    /// <summary>
    /// Underline any cells on the given input row that intersect a diagnostic range.
    /// The decoration also pulls the foreground to soft-red so the squiggle reads
    /// regardless of whatever the syntax highlighter painted underneath.
    /// </summary>
    private static void ApplyDiagnosticUnderlines(
        CellBuffer buffer, int inputX, int y, int lineLength, int row, int inputWidth,
        IReadOnlyList<Diagnostic> diagnostics)
    {
        if (diagnostics.Count == 0 || lineLength == 0) return;
        foreach (var d in diagnostics)
        {
            if (d.Range.Start.Line != row && d.Range.End.Line != row
                && (row < d.Range.Start.Line || row > d.Range.End.Line)) continue;

            int s = d.Range.Start.Line == row ? d.Range.Start.Character : 0;
            int e = d.Range.End.Line == row ? d.Range.End.Character : lineLength;
            s = Math.Clamp(s, 0, lineLength);
            e = Math.Clamp(e, s, lineLength);
            if (e <= s) continue;
            CellPaint.UnderlineCells(buffer, inputX + s, y, Math.Min(e - s, inputWidth - s), ErrorFg);
        }
    }
}
