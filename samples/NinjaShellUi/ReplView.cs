using System.Text;
using TerminalNinja.Buffers;
using TerminalNinja.Controls;
using TerminalNinja.Highlighting;
using TerminalNinja.Input;
using TerminalNinja.Primitives;
using TerminalNinja.Shell.Language.Services;
using TerminalNinja.Shell.Values;
using TerminalNinja.Styling;

namespace NinjaShellUi;

/// <summary>
/// A minimal terminal-style REPL surface: a scrolling output buffer at the top and a
/// multi-line input region at the bottom. Owns its own input state (no <c>TextBox</c>),
/// so <c>Enter</c>, history navigation, and command execution all funnel through one
/// keyboard handler.
/// </summary>
/// <remarks>
/// <para>
/// LSP-shaped affordances are wired in through <see cref="LanguageService"/> — the same
/// pure-function surface that the standalone LSP server consumes. Each keystroke that
/// changes the input buffer kicks <see cref="RecomputeAnalysis"/> which refreshes:
/// </para>
/// <list type="bullet">
/// <item><description>Diagnostics, drawn as a single-line error message above the prompt
/// with a caret under the offending column.</description></item>
/// <item><description>Hover info for the identifier under the cursor, drawn one line
/// above the diagnostic.</description></item>
/// </list>
/// <para>
/// Tab triggers a completion popup overlaid above the prompt; ↑/↓ cycles through the
/// items, Enter (or Tab) accepts and replaces the partial token, Esc dismisses.
/// </para>
/// <para>
/// <c>Enter</c> submits the entire buffer (which may span multiple lines).
/// <c>Shift+Enter</c> inserts a newline so the user can compose <c>let … in …</c> blocks,
/// switch expressions, or pasted multi-statement scripts in place. Continuation rows
/// show a dimmed <c>"... "</c> prefix instead of the primary <c>Prompt</c>.
/// </para>
/// </remarks>
public sealed class ReplView : Control
{
    private readonly List<string> _outputLines = new(capacity: 256);
    private readonly StringBuilder _input = new();
    private readonly List<string> _history = new();
    private int _historyIndex = -1;
    // Linear UTF-16 offset into _input — NOT a column index. Multi-line input means this
    // index spans `\n`s; the row/column on screen is computed via CursorToLineCol(...).
    private int _cursorCol;
    private int _scrollOffset;

    // LSP-shaped derived state, recomputed on every input change.
    private IReadOnlyList<Diagnostic> _diagnostics = Array.Empty<Diagnostic>();
    private Hover? _hover;

    // Completion popup state. Built fresh on each Tab press from the current cursor;
    // surviving keystrokes (Up / Down / Enter / Esc) navigate or dismiss it.
    // The visual is rendered via the core CompletionPanel overlay (icon + label
    // list on the left, signature + documentation on the right).
    private IReadOnlyList<CompletionItem>? _completions;
    private int _completionIndex;
    private int _completionAnchorCol;
    private readonly CompletionPanel _completionPanel = new();

    // Signature-help popup. Re-evaluated on every keystroke; opens when the
    // cursor sits inside an open paren that resolves to a known callable.
    // Uses HoverPanel (not CompletionPanel) — signature help is just a styled
    // single-block tooltip, not a navigable list.
    private readonly HoverPanel _signaturePanel = new();
    private SignatureHelp? _activeSignature;

    // Mouse-hover state. The HoverPanel itself lives in TerminalNinja core and
    // pushes its content onto the Application overlay stack on Show. We track
    // the last-shown cell so identical mouse positions don't churn the overlay.
    private readonly HoverPanel _hoverPanel = new();
    private readonly HoverBox _hoverBox = new();
    private readonly Dictionary<int, NValue> _outputResults = new();
    private Rect _lastBounds;
    private (int X, int Y)? _lastMouseHoverCell;

    // Cached on every OnRender so OnMouseEvent and the OnKeyEvent scroll path
    // can use "one page" (= output rows visible) for PageUp / PageDown without
    // duplicating the layout math.
    private int _lastOutputHeight;
    private int _lastInputTopY;
    private int _lastInputLines;

    // Selection model. Coordinates are stored in *region* space (line index within
    // the region + column within that line), NOT screen space — so scrolling the
    // output area doesn't move the selection. Region cells live in their own
    // coordinate system; selections never cross from input to output or vice
    // versa.
    private enum SelectionRegion { None, Input, Output }
    private SelectionRegion _selectionRegion = SelectionRegion.None;
    private (int Row, int Col) _selectionAnchor;
    private (int Row, int Col) _selectionHead;
    private bool _selectionRectangular;
    private bool _isMouseDragging;

    /// <summary>The prompt rendered in front of the first input row. Defaults to <c>"ninja&gt; "</c>.</summary>
    public string Prompt { get; set; } = "ninja> ";

    /// <summary>
    /// Continuation prompt rendered in front of input rows 2..n when the user has entered
    /// a multi-line buffer via Shift+Enter. Must be the same width as <see cref="Prompt"/>
    /// so cursor columns line up across rows; padded automatically if it's shorter.
    /// </summary>
    public string ContinuationPrompt { get; set; } = "... > ";

    /// <summary>
    /// Language identifier handed to <see cref="SyntaxHighlighterRegistry"/> when rendering
    /// the input line. <c>null</c> disables highlighting (the input is drawn with the plain
    /// <see cref="Control.Foreground"/> color). Default is <c>"ninja"</c>.
    /// </summary>
    public string? HighlightLanguage { get; set; } = "ninja";

    /// <summary>The theme colour palette applied to highlighted tokens.</summary>
    public SyntaxTheme Theme { get; set; } = SyntaxTheme.Dark;

    /// <summary>The current contents of the input buffer (without the prompt).</summary>
    public string InputBuffer => _input.ToString();

    /// <summary>
    /// Live scope used to enrich hover info with shape + data for user-defined
    /// bindings. The host (e.g. <see cref="ShellViewModel"/>) sets this from
    /// the evaluator's <c>Env.Bindings</c> snapshot after each evaluation.
    /// </summary>
    public IReadOnlyDictionary<string, NValue>? Scope { get; set; }

    /// <summary>Raised when the user presses Enter on a non-empty input line.</summary>
    public event Action<string>? CommandEntered;

    /// <summary>Creates a focusable REPL view.</summary>
    public ReplView()
    {
        Focusable = true;
    }

    /// <inheritdoc />
    public override Size2D GetPreferredSize(Rect availableSpace) => new(availableSpace.Width, availableSpace.Height);

    /// <inheritdoc />
    public override Rect CalculateBounds(Rect parentBounds) => parentBounds;

    /// <summary>Appends <paramref name="text"/> to the output buffer, splitting on newlines.</summary>
    public void AppendOutput(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        foreach (var line in text.Split('\n'))
        {
            _outputLines.Add(line.TrimEnd('\r'));
        }
        ScrollToBottom();
        InvalidationCallback?.Invoke();
    }

    /// <summary>
    /// Append the rendered output of an evaluation and remember the produced
    /// <paramref name="value"/> alongside it. Every appended line becomes a
    /// mouse-hover target — moving the mouse onto any of them shows the value's
    /// shape and data in a <see cref="HoverPanel"/>. Calls with a null value
    /// (errors, banners, status lines) skip the registry and behave like
    /// <see cref="AppendOutput(string?)"/>.
    /// </summary>
    public void AppendResult(string? text, NValue? value)
    {
        if (string.IsNullOrEmpty(text))
        {
            AppendOutput(text);
            return;
        }

        int firstLineIndex = _outputLines.Count;
        foreach (var line in text.Split('\n'))
        {
            _outputLines.Add(line.TrimEnd('\r'));
        }

        if (value.HasValue)
        {
            // Every line of this multi-line block resolves back to the same value.
            var resolved = value.Value;
            for (int i = firstLineIndex; i < _outputLines.Count; i++)
                _outputResults[i] = resolved;
        }

        ScrollToBottom();
        InvalidationCallback?.Invoke();
    }

    /// <summary>Removes every line from the output buffer.</summary>
    public void ClearOutput()
    {
        _outputLines.Clear();
        _outputResults.Clear();
        _scrollOffset = 0;
        HideMouseHover();
        InvalidationCallback?.Invoke();
    }

    /// <inheritdoc />
    protected override void OnRender(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        _lastBounds = bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        // The input region grows downward from a baseline near the bottom: it always
        // occupies `inputLines` rows (≥ 1, the user-entered '\n' count plus one), and
        // the optional hover / diagnostic rows sit above it. We clamp inputLines to
        // half the panel height so a runaway multi-line buffer can't swallow the entire
        // output area.
        var inputLines = CountInputLines();
        inputLines = Math.Min(inputLines, Math.Max(1, bounds.Height / 2));

        var inputBottomY = bounds.Y + bounds.Height - 1;
        var inputTopY = inputBottomY - (inputLines - 1);
        var diagY = HasDiagnostic() ? inputTopY - 1 : -1;
        var hoverY = (_hover is not null && diagY > 0) ? diagY - 1
                   : _hover is not null ? inputTopY - 1
                   : -1;

        var topReserved = inputLines + (diagY > -1 ? 1 : 0) + (hoverY > -1 ? 1 : 0);
        var outputHeight = Math.Max(0, bounds.Height - topReserved);
        _lastOutputHeight = outputHeight;
        _lastInputTopY = inputTopY;
        _lastInputLines = inputLines;
        ClampScrollOffset();

        var fg = Foreground;
        var bg = Background;
        ClearRegion(buffer, bounds, bg);
        RenderOutput(buffer, bounds.X, bounds.Y, bounds.Width, outputHeight, fg, bg);
        RenderScrollIndicator(buffer, bounds.Right - 1, bounds.Y, outputHeight, bg);

        if (hoverY > -1) RenderHoverLine(buffer, bounds.X, hoverY, bounds.Width, bg);
        if (diagY > -1) RenderDiagnosticLine(buffer, bounds.X, diagY, bounds.Width, bg);

        RenderInputBlock(buffer, bounds.X, inputTopY, bounds.Width, inputLines, fg, bg);

        // Completion + signature popups render on the overlay stack via
        // _completionPanel / _signaturePanel — nothing to draw inline.
    }

    /// <summary>Number of input rows the current buffer needs (1 + count of '\n').</summary>
    private int CountInputLines()
    {
        var n = 1;
        for (var i = 0; i < _input.Length; i++)
        {
            if (_input[i] == '\n') n++;
        }
        return n;
    }

    private bool HasDiagnostic() => _diagnostics.Count > 0;

    private void RenderOutput(CellBuffer buffer, int x, int y, int width, int height, Color fg, Color bg)
    {
        if (height <= 0) return;
        var firstLine = Math.Max(0, _outputLines.Count - height - _scrollOffset);
        var lastLine = Math.Min(_outputLines.Count, firstLine + height);

        for (var i = firstLine; i < lastLine; i++)
        {
            var row = y + (i - firstLine);
            DrawText(buffer, x, row, _outputLines[i], width, fg, bg);
            ApplyOutputSelectionToRow(buffer, x, row, width, i);
        }
    }

    /// <summary>If the output line at <paramref name="lineIndex"/> intersects the active
    /// selection, invert fg/bg on the selected cells of <paramref name="row"/>.</summary>
    private void ApplyOutputSelectionToRow(CellBuffer buffer, int x, int row, int width, int lineIndex)
    {
        if (_selectionRegion != SelectionRegion.Output) return;
        if (!TryGetSelectedColsForRow(lineIndex, _outputLines[lineIndex].Length, out var startCol, out var endCol)) return;
        InvertCells(buffer, x + startCol, row, Math.Min(endCol, width) - startCol);
    }

    /// <summary>
    /// Draws a one-cell-wide scroll-position indicator on the right edge of the
    /// output area. The thumb's height is proportional to "visible / total" and
    /// its top position is proportional to the scroll offset. Track cells stay
    /// blank (no fill character) so the indicator looks like a discrete block
    /// rather than a continuous line.
    /// </summary>
    private void RenderScrollIndicator(CellBuffer buffer, int x, int y, int outputHeight, Color bg)
    {
        if (outputHeight <= 1) return;
        if (_outputLines.Count <= outputHeight) return;        // everything visible — no indicator

        int total = _outputLines.Count;
        // Thumb height: outputHeight^2 / total, min 1, max outputHeight.
        int thumbHeight = Math.Max(1, Math.Min(outputHeight, outputHeight * outputHeight / Math.Max(1, total)));
        int trackLength = outputHeight - thumbHeight;
        // Thumb top: trackLength * (1 - scrollOffset / maxOffset). scrollOffset=0 → bottom; max → top.
        int maxOffset = Math.Max(1, total - outputHeight);
        int offsetFromTop = trackLength - (trackLength * Math.Min(_scrollOffset, maxOffset) / maxOffset);
        int thumbTopY = y + offsetFromTop;

        var thumbColor = new Color(0x6C, 0x70, 0x86);
        for (int i = 0; i < thumbHeight; i++)
        {
            int row = thumbTopY + i;
            if ((uint)row >= (uint)buffer.Height || (uint)x >= (uint)buffer.Width) continue;
            buffer.SetChar(x, row, '█', thumbColor, bg);   // █ full block
        }
    }

    /// <summary>
    /// Scroll by <paramref name="lines"/> rows; positive moves the view back in time
    /// (older content), negative moves forward (newer). Clamping happens in
    /// <see cref="ClampScrollOffset"/> on the next render pass.
    /// </summary>
    private void ScrollBy(int lines)
    {
        _scrollOffset += lines;
        ClampScrollOffset();
        InvalidationCallback?.Invoke();
    }

    /// <summary>Scroll to an absolute offset (clamped). <c>0</c> = bottom (newest), large = top.</summary>
    private void ScrollTo(int offset)
    {
        _scrollOffset = offset;
        ClampScrollOffset();
        InvalidationCallback?.Invoke();
    }

    private void ClampScrollOffset()
    {
        int maxOffset = Math.Max(0, _outputLines.Count - _lastOutputHeight);
        if (_scrollOffset > maxOffset) _scrollOffset = maxOffset;
        if (_scrollOffset < 0) _scrollOffset = 0;
    }

    // ─── Selection ──────────────────────────────────────────────────────────

    /// <summary>True when the current selection covers at least one cell.</summary>
    private bool HasSelection => _selectionRegion != SelectionRegion.None
        && (_selectionAnchor.Row != _selectionHead.Row || _selectionAnchor.Col != _selectionHead.Col);

    /// <summary>
    /// Compute the [startCol, endCol) range of selected columns on
    /// <paramref name="row"/>, within a line of <paramref name="lineLength"/>.
    /// Returns false when this row falls outside the selection. Handles both
    /// line-flow and rectangular selection modes.
    /// </summary>
    private bool TryGetSelectedColsForRow(int row, int lineLength, out int startCol, out int endCol)
    {
        startCol = endCol = 0;
        if (_selectionRegion == SelectionRegion.None) return false;

        int rowLo = Math.Min(_selectionAnchor.Row, _selectionHead.Row);
        int rowHi = Math.Max(_selectionAnchor.Row, _selectionHead.Row);
        if (row < rowLo || row > rowHi) return false;

        if (_selectionRectangular)
        {
            int colLo = Math.Min(_selectionAnchor.Col, _selectionHead.Col);
            int colHi = Math.Max(_selectionAnchor.Col, _selectionHead.Col);
            startCol = Math.Min(colLo, lineLength);
            endCol = Math.Min(colHi, lineLength);
            return endCol > startCol;
        }

        // Line-flow: the first selected row goes from the anchor's column to EOL,
        // intermediate rows are fully selected, the last row goes from BOL to head.
        var (firstRow, firstCol, lastRow, lastCol) = OrderedEndpoints();
        if (row == firstRow && row == lastRow) { startCol = firstCol; endCol = Math.Min(lastCol, lineLength); }
        else if (row == firstRow)              { startCol = firstCol; endCol = lineLength; }
        else if (row == lastRow)               { startCol = 0;        endCol = Math.Min(lastCol, lineLength); }
        else                                   { startCol = 0;        endCol = lineLength; }
        return endCol > startCol;
    }

    private (int FirstRow, int FirstCol, int LastRow, int LastCol) OrderedEndpoints()
    {
        if (_selectionAnchor.Row < _selectionHead.Row
            || (_selectionAnchor.Row == _selectionHead.Row && _selectionAnchor.Col <= _selectionHead.Col))
        {
            return (_selectionAnchor.Row, _selectionAnchor.Col, _selectionHead.Row, _selectionHead.Col);
        }
        return (_selectionHead.Row, _selectionHead.Col, _selectionAnchor.Row, _selectionAnchor.Col);
    }

    /// <summary>
    /// Walks the active diagnostic list and underlines any cells on the given
    /// input row that intersect a diagnostic range. The decoration also pulls
    /// the foreground to soft-red so the squiggle reads regardless of whatever
    /// the syntax highlighter painted underneath.
    /// </summary>
    private void ApplyDiagnosticUnderlinesToRow(CellBuffer buffer, int inputX, int y, int lineLength, int row, int inputWidth)
    {
        if (_diagnostics.Count == 0 || lineLength == 0) return;
        var errFg = new Color(0xF3, 0x8B, 0xA8);
        foreach (var d in _diagnostics)
        {
            if (d.Range.Start.Line != row && d.Range.End.Line != row
                && (row < d.Range.Start.Line || row > d.Range.End.Line)) continue;

            // Map the diagnostic range onto this row's column span. A range
            // that starts on an earlier line begins at column 0 here; one that
            // ends on a later line runs to EOL.
            int s = d.Range.Start.Line == row ? d.Range.Start.Character : 0;
            int e = d.Range.End.Line == row ? d.Range.End.Character : lineLength;
            s = Math.Clamp(s, 0, lineLength);
            e = Math.Clamp(e, s, lineLength);
            if (e <= s) continue;
            UnderlineCells(buffer, inputX + s, y, Math.Min(e - s, inputWidth - s), errFg);
        }
    }

    /// <summary>Add <see cref="TextDecorations.Underline"/> and force red fg on <paramref name="length"/> cells starting at (x, y).</summary>
    private static void UnderlineCells(CellBuffer buffer, int x, int y, int length, Color errFg)
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

    /// <summary>Invert fg/bg of <paramref name="length"/> cells starting at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    private static void InvertCells(CellBuffer buffer, int x, int y, int length)
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

    /// <summary>
    /// Build the text payload for the current selection, ready to ship to the
    /// clipboard. Lines are joined with <c>\n</c>. Rectangular selections pad /
    /// truncate each row to the column band.
    /// </summary>
    private string BuildSelectionText()
    {
        if (_selectionRegion == SelectionRegion.None) return string.Empty;

        var source = _selectionRegion == SelectionRegion.Output
            ? (IReadOnlyList<string>)_outputLines
            : InputLines();

        int rowLo = Math.Max(0, Math.Min(_selectionAnchor.Row, _selectionHead.Row));
        int rowHi = Math.Min(source.Count - 1, Math.Max(_selectionAnchor.Row, _selectionHead.Row));
        if (rowHi < rowLo) return string.Empty;

        var sb = new StringBuilder();
        for (int r = rowLo; r <= rowHi; r++)
        {
            if (!TryGetSelectedColsForRow(r, source[r].Length, out var s, out var e))
            {
                if (r != rowHi) sb.Append('\n');
                continue;
            }
            sb.Append(source[r], s, e - s);
            if (r != rowHi) sb.Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>Lazy view of <see cref="_input"/> split on <c>\n</c>, indexed by input row.</summary>
    private IReadOnlyList<string> InputLines() => _input.ToString().Split('\n');

    private void ClearSelection()
    {
        if (_selectionRegion == SelectionRegion.None) return;
        _selectionRegion = SelectionRegion.None;
        _selectionRectangular = false;
        _isMouseDragging = false;
        InvalidationCallback?.Invoke();
    }

    /// <summary>
    /// Copy the current selection to the OS clipboard via
    /// <see cref="TerminalNinja.App.Application.Clipboard"/>. On success the
    /// selection is cleared (so the next keystroke / click doesn't see stale
    /// highlight). Failures surface as a single output line so the user knows
    /// why nothing landed on the clipboard.
    /// </summary>
    /// <returns><c>true</c> when the clipboard accepted the text.</returns>
    private bool CopyToClipboard()
    {
        if (!HasSelection) return false;
        var text = BuildSelectionText();
        if (text.Length == 0) return false;

        var clipboard = TerminalNinja.App.Application.Current?.Clipboard;
        if (clipboard is null)
        {
            AppendOutput("copy: clipboard is unavailable (no Application context)");
            return false;
        }

        try
        {
            clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            AppendOutput($"copy: failed — {ex.Message}");
            return false;
        }

        // Most editors leave the selection visible after Ctrl+C, but in this
        // REPL the next keystroke is usually a buffer edit (or Esc) — keeping
        // the highlight around after a successful copy just gets in the way.
        ClearSelection();
        return true;
    }

    /// <summary>
    /// Mouse-down — start a fresh selection at the clicked cell, unless Shift
    /// is held in which case extend the existing selection. Alt switches the
    /// selection mode to rectangular (block).
    /// </summary>
    private void BeginSelection(MouseEvent e)
    {
        var hit = HitTest(e.X, e.Y);
        if (hit.Region == SelectionRegion.None)
        {
            ClearSelection();
            return;
        }

        // Clicking dismisses any open completion popup and hides the
        // mouse-hover panel so they don't compete with the drag.
        if (_completions is { Count: > 0 }) CloseCompletion();
        HideMouseHover();

        if (e.Shift && _selectionRegion == hit.Region)
        {
            // Shift-click — extend by moving only the head; rectangular mode
            // is inherited from the existing selection.
            _selectionHead = (hit.Row, hit.Col);
        }
        else
        {
            _selectionRegion = hit.Region;
            _selectionRectangular = e.Alt;
            _selectionAnchor = (hit.Row, hit.Col);
            _selectionHead = (hit.Row, hit.Col);
        }

        _isMouseDragging = true;
        InvalidationCallback?.Invoke();
    }

    /// <summary>Drag-update — move only the head; clamp to the same region as the anchor.</summary>
    private void ExtendSelection(int mouseX, int mouseY)
    {
        if (_selectionRegion == SelectionRegion.None) return;
        var hit = HitTest(mouseX, mouseY);
        if (hit.Region != _selectionRegion) return;   // dragging out of the region is a no-op
        if (_selectionHead.Row == hit.Row && _selectionHead.Col == hit.Col) return;
        _selectionHead = (hit.Row, hit.Col);
        InvalidationCallback?.Invoke();
    }

    /// <summary>
    /// Map a screen (mouseX, mouseY) cell to a (region, row, col) tuple in the
    /// region's own coordinate system. Returns None when the click landed
    /// outside both regions.
    /// </summary>
    private (SelectionRegion Region, int Row, int Col) HitTest(int mouseX, int mouseY)
    {
        if (_lastBounds.Width <= 0) return (SelectionRegion.None, 0, 0);

        // Input region: the bottom inputLines rows of the panel.
        int inputBottomY = _lastBounds.Y + _lastBounds.Height - 1;
        int inputTopY = _lastInputTopY;
        if (mouseY >= inputTopY && mouseY <= inputBottomY)
        {
            int row = mouseY - inputTopY;
            int promptWidth = Math.Max(Prompt.Length, ContinuationPrompt.Length);
            int col = Math.Max(0, mouseX - _lastBounds.X - promptWidth);
            // Clamp col to the row's actual length so selection can't run past EOL.
            var lines = InputLines();
            if (row >= 0 && row < lines.Count) col = Math.Min(col, lines[row].Length);
            return (SelectionRegion.Input, row, col);
        }

        // Output region: everything above the input + decoration rows.
        if (mouseY >= _lastBounds.Y && mouseY < _lastBounds.Y + _lastOutputHeight)
        {
            int rowInPanel = mouseY - _lastBounds.Y;
            int firstVisible = Math.Max(0, _outputLines.Count - _lastOutputHeight - _scrollOffset);
            int lineIndex = firstVisible + rowInPanel;
            if (lineIndex < 0 || lineIndex >= _outputLines.Count)
                return (SelectionRegion.None, 0, 0);
            int col = Math.Max(0, mouseX - _lastBounds.X);
            col = Math.Min(col, _outputLines[lineIndex].Length);
            return (SelectionRegion.Output, lineIndex, col);
        }

        return (SelectionRegion.None, 0, 0);
    }

    /// <summary>
    /// Renders one or more input rows starting at <paramref name="topY"/>. The first row
    /// uses <see cref="Prompt"/>; subsequent rows use <see cref="ContinuationPrompt"/>
    /// padded to the same width so cursor columns align across rows. Highlighter runs
    /// once over the full buffer; the tokens are sliced per row at render time.
    /// </summary>
    private void RenderInputBlock(CellBuffer buffer, int x, int topY, int width, int rowCount, Color fg, Color bg)
    {
        var promptWidth = Math.Max(Prompt.Length, ContinuationPrompt.Length);
        var inputX = x + promptWidth;
        var inputWidth = Math.Max(0, width - promptWidth);

        var allTokens = TokenizeOrNull();
        var text = _input.ToString();

        var (cursorLine, cursorCol) = CursorToLineCol(_cursorCol);

        // Walk the buffer line by line in lockstep with the row we render on. Each line's
        // start offset is its byte index in `text`; render `rowCount` rows even if the
        // logical input has more lines (clamped — overflow lines are dropped from view).
        var offset = 0;
        for (var r = 0; r < rowCount; r++)
        {
            var y = topY + r;
            if ((uint)y >= (uint)buffer.Height) break;

            // Find the end of this logical line: next '\n' or end of buffer.
            var lineEnd = offset;
            while (lineEnd < text.Length && text[lineEnd] != '\n') lineEnd++;

            // Prompt prefix for this row.
            var prefix = r == 0 ? Prompt : ContinuationPrompt;
            var prefixFg = r == 0 ? fg : new Color(0x6C, 0x70, 0x86); // dim continuation
            DrawText(buffer, x, y, prefix.PadRight(promptWidth), width, prefixFg, bg);

            // Slice the line's text and highlighter tokens onto this row.
            var lineText = text.Substring(offset, lineEnd - offset);
            DrawHighlightedInputLine(buffer, inputX, y, lineText, offset, allTokens, inputWidth, fg, bg);

            // Error underlines go on after highlighting and before selection
            // inversion — selection visually wins over the underline (you can
            // still see the squiggle, just inverted with the rest of the run).
            ApplyDiagnosticUnderlinesToRow(buffer, inputX, y, lineText.Length, r, inputWidth);

            // Selection inversion lives on top of the highlight pass so it
            // works regardless of the per-token colour we drew underneath.
            if (_selectionRegion == SelectionRegion.Input
                && TryGetSelectedColsForRow(r, lineText.Length, out var selStart, out var selEnd))
            {
                InvertCells(buffer, inputX + selStart, y, Math.Min(selEnd, inputWidth) - selStart);
            }

            // Render the cursor cell on this row if the cursor sits on this line.
            if (cursorLine == r)
            {
                var cursorX = inputX + Math.Min(cursorCol, Math.Max(0, inputWidth - 1));
                if (cursorX >= inputX && cursorX < inputX + inputWidth && (uint)cursorX < (uint)buffer.Width)
                {
                    var cell = buffer.GetCell(cursorX, y);
                    buffer.SetCell(cursorX, y, new Cell(cell.Codepoint, cell.Background, cell.Foreground, cell.Decorations, cell.Flags));
                }
            }

            // Advance past this line plus the trailing '\n' (if any).
            offset = lineEnd < text.Length ? lineEnd + 1 : lineEnd;
        }
    }

    private IReadOnlyList<SyntaxToken>? TokenizeOrNull()
    {
        if (HighlightLanguage is null) return null;
        if (_input.Length == 0) return null;
        if (!SyntaxHighlighterRegistry.TryGet(HighlightLanguage, out var hl)) return null;
        return hl.Tokenize(_input.ToString());
    }

    /// <summary>
    /// Renders one row of the input — the substring <paramref name="lineText"/> that lives
    /// at <paramref name="lineOffset"/> within the full buffer — with highlighted token
    /// colours. Tokens were produced over the whole buffer (so multi-line constructs like
    /// strings highlight correctly across rows); per-row rendering filters tokens to those
    /// that overlap this line's offset range.
    /// </summary>
    private void DrawHighlightedInputLine(
        CellBuffer buffer, int x, int y,
        string lineText, int lineOffset,
        IReadOnlyList<SyntaxToken>? tokens,
        int maxWidth, Color fallbackFg, Color bg)
    {
        if (lineText.Length == 0 || maxWidth <= 0 || (uint)y >= (uint)buffer.Height) return;

        if (tokens is null)
        {
            DrawText(buffer, x, y, lineText, maxWidth, fallbackFg, bg);
            return;
        }

        var tokenIdx = 0;
        for (var i = 0; i < lineText.Length && i < maxWidth; i++)
        {
            var absoluteOffset = lineOffset + i;

            // Advance past tokens that ended before this absolute offset.
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
                    fg = Theme.GetColor(t.Kind);
                }
            }

            var cx = x + i;
            if ((uint)cx >= (uint)buffer.Width) break;
            buffer.SetChar(cx, y, lineText[i], fg, bg);
        }
    }

    private void RenderHoverLine(CellBuffer buffer, int x, int y, int width, Color bg)
    {
        if (_hover is null) return;
        // Hover info is informational — dim cyan-ish on the same background.
        var hoverFg = new Color(0x89, 0xDC, 0xEB);
        // Collapse newlines: hover content can be multi-line (module summary lists members
        // on a second line). One row only, so we replace newlines with " · ".
        var text = _hover.Contents.Replace("\n\n", " · ").Replace('\n', ' ');
        DrawText(buffer, x, y, text, width, hoverFg, bg);
    }

    private void RenderDiagnosticLine(CellBuffer buffer, int x, int y, int width, Color bg)
    {
        var d = _diagnostics[0];
        var errFg = new Color(0xF3, 0x8B, 0xA8); // soft red
        // Format: "  ^ <message>" with the caret roughly under the offending column —
        // remember diagnostic ranges are 0-based and pointing into the input buffer, but
        // we want screen coordinates so we add Prompt.Length.
        var caretCol = Prompt.Length + d.Range.Start.Character;
        var line = new StringBuilder(width);
        for (var c = 0; c < caretCol && c < width; c++) line.Append(' ');
        if (caretCol < width) line.Append('^');
        line.Append(' ').Append(d.Message);
        DrawText(buffer, x, y, line.ToString(), width, errFg, bg);
    }

    /// <summary>
    /// Map an LSP-shaped <see cref="CompletionItem"/> to a renderer-friendly
    /// <see cref="CompletionEntry"/>: pick a glyph and colour per kind so the
    /// list reads at a glance, and pass Detail / Documentation straight through.
    /// </summary>
    private static CompletionEntry ToEntry(CompletionItem item)
    {
        var (glyph, color) = item.Kind switch
        {
            CompletionKind.Function    => ("ƒ", new Color(0x89, 0xB4, 0xFA)), // blue
            CompletionKind.Method      => ("ƒ", new Color(0x89, 0xB4, 0xFA)),
            CompletionKind.Constructor => ("ƒ", new Color(0x89, 0xB4, 0xFA)),
            CompletionKind.Variable    => ("α", new Color(0xA6, 0xE3, 0xA1)), // green
            CompletionKind.Field       => ("▪", new Color(0x94, 0xE2, 0xD5)), // teal
            CompletionKind.Property    => ("▪", new Color(0x94, 0xE2, 0xD5)),
            CompletionKind.Module      => ("■", new Color(0xF9, 0xE2, 0xAF)), // yellow
            CompletionKind.Class       => ("C", new Color(0xF9, 0xE2, 0xAF)),
            CompletionKind.Interface   => ("I", new Color(0xF9, 0xE2, 0xAF)),
            CompletionKind.Keyword     => ("★", new Color(0xCB, 0xA6, 0xF7)), // mauve
            CompletionKind.Enum        => ("E", new Color(0xFA, 0xB3, 0x87)), // peach
            CompletionKind.Snippet     => ("◇", new Color(0x9C, 0xA0, 0xB0)),
            _                          => ("·", new Color(0x9C, 0xA0, 0xB0)),
        };
        return new CompletionEntry(item.Label, glyph, color, item.Detail, item.Documentation);
    }

    /// <summary>
    /// Place the <see cref="CompletionPanel"/> at the row above the input line
    /// that holds the partial token, anchored to the column of the partial token.
    /// </summary>
    private (int X, int Y) GetCompletionAnchor()
    {
        var (anchorLine, _) = CursorToLineCol(_completionAnchorCol);
        var promptWidth = Math.Max(Prompt.Length, ContinuationPrompt.Length);
        var anchorLineStart = LineColToIndex(anchorLine, 0);
        var anchorColOnLine = _completionAnchorCol - anchorLineStart;
        return (_lastBounds.X + promptWidth + anchorColOnLine, _lastInputTopY + anchorLine);
    }

    private static void FillRow(CellBuffer buffer, int x, int y, int width, Color bg)
    {
        if ((uint)y >= (uint)buffer.Height) return;
        for (var i = 0; i < width; i++)
        {
            var cx = x + i;
            if ((uint)cx >= (uint)buffer.Width) break;
            buffer.SetCell(cx, y, new Cell(' ', Color.White, bg));
        }
    }

    private static void ClearRegion(CellBuffer buffer, Rect bounds, Color bg)
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

    private static void DrawText(CellBuffer buffer, int x, int y, string text, int maxWidth, Color fg, Color bg)
    {
        if ((uint)y >= (uint)buffer.Height) return;
        var currentFg = fg;
        var currentBg = bg;
        var deco = TextDecorations.None;
        int col = 0;
        int i = 0;
        while (i < text.Length && col < maxWidth)
        {
            // SGR escape: \e[ params m → mutate state, don't advance a column.
            if (text[i] == 0x1B && i + 1 < text.Length && text[i + 1] == '[')
            {
                int end = text.IndexOf('m', i + 2);
                if (end < 0) break;     // malformed — abort the rest of the line
                ApplySgr(text.AsSpan(i + 2, end - (i + 2)), ref currentFg, ref currentBg, ref deco, fg, bg);
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

    /// <summary>
    /// Apply a single ANSI SGR (Select Graphic Rendition) payload — the
    /// semicolon-separated numeric codes between <c>\e[</c> and <c>m</c> — to
    /// the current rendering state. Supports the subset the REPL actually emits:
    /// reset, bold/dim toggle, basic + bright 8-color fg, 256-color fg, truecolor
    /// fg, and the matching default-fg / clear-bold-or-dim reset codes.
    /// </summary>
    private static void ApplySgr(ReadOnlySpan<char> payload, ref Color fg, ref Color bg,
                                 ref TextDecorations deco, Color defaultFg, Color defaultBg)
    {
        Span<int> codes = stackalloc int[16];
        int n = 0;
        int cur = 0;
        bool any = false;
        foreach (var ch in payload)
        {
            if (ch == ';')
            {
                if (n < codes.Length) codes[n++] = any ? cur : 0;
                cur = 0; any = false;
            }
            else if (ch is >= '0' and <= '9')
            {
                cur = cur * 10 + (ch - '0');
                any = true;
            }
        }
        if (n < codes.Length) codes[n++] = any ? cur : 0;

        int k = 0;
        while (k < n)
        {
            int code = codes[k];
            switch (code)
            {
                case 0: fg = defaultFg; bg = defaultBg; deco = TextDecorations.None; k++; break;
                case 1: deco |= TextDecorations.Bold; k++; break;
                case 2: deco |= TextDecorations.Dim; k++; break;
                case 22: deco &= ~(TextDecorations.Bold | TextDecorations.Dim); k++; break;
                case 30: case 31: case 32: case 33: case 34: case 35: case 36: case 37:
                    fg = AnsiBasicColor(code - 30); k++; break;
                case 38:
                    if (k + 4 < n && codes[k + 1] == 2)
                    { fg = new Color((byte)codes[k + 2], (byte)codes[k + 3], (byte)codes[k + 4]); k += 5; }
                    else if (k + 2 < n && codes[k + 1] == 5)
                    { fg = AnsiBasicColor(codes[k + 2] & 0xF); k += 3; }
                    else k++;
                    break;
                case 39: fg = defaultFg; k++; break;
                case 90: case 91: case 92: case 93: case 94: case 95: case 96: case 97:
                    fg = AnsiBrightColor(code - 90); k++; break;
                default: k++; break;
            }
        }
    }

    private static Color AnsiBasicColor(int idx) => idx switch
    {
        0 => new Color(0x00, 0x00, 0x00),
        1 => new Color(0xF3, 0x8B, 0xA8),
        2 => new Color(0xA6, 0xE3, 0xA1),
        3 => new Color(0xF9, 0xE2, 0xAF),
        4 => new Color(0x89, 0xB4, 0xFA),
        5 => new Color(0xCB, 0xA6, 0xF7),
        6 => new Color(0x94, 0xE2, 0xD5),
        7 => new Color(0xCD, 0xD6, 0xF4),
        _ => new Color(0xCD, 0xD6, 0xF4),
    };

    private static Color AnsiBrightColor(int idx) => idx switch
    {
        0 => new Color(0x58, 0x5B, 0x70),
        1 => new Color(0xF3, 0x8B, 0xA8),
        2 => new Color(0xA6, 0xE3, 0xA1),
        3 => new Color(0xF9, 0xE2, 0xAF),
        4 => new Color(0x89, 0xB4, 0xFA),
        5 => new Color(0xCB, 0xA6, 0xF7),
        6 => new Color(0x94, 0xE2, 0xD5),
        7 => new Color(0xCD, 0xD6, 0xF4),
        _ => new Color(0xCD, 0xD6, 0xF4),
    };

    private void ScrollToBottom() => _scrollOffset = 0;

    /// <inheritdoc />
    public override void OnKeyEvent(KeyEvent e)
    {
        // Hover panel scroll: while the mouse-hover overlay is open, PgUp/PgDn
        // and Ctrl+↑/↓ scroll its contents (the box itself never gets focus, so
        // the REPL forwards keys to it explicitly). Keys the hover doesn't
        // consume fall through to the normal input handling below.
        if (_hoverPanel.IsOpen && _hoverBox.HandleKey(e))
        {
            InvalidationCallback?.Invoke();
            return;
        }

        // Completion popup eats Up / Down / Enter / Esc / Tab while it's open. The popup is
        // dismissed by Esc or by any keystroke that changes the input buffer in a way that
        // would invalidate the items (handled below as "any non-popup key while open").
        if (_completions is { Count: > 0 })
        {
            switch (e.Key)
            {
                case ConsoleKey.UpArrow:
                    _completionIndex = (_completionIndex - 1 + _completions.Count) % _completions.Count;
                    _completionPanel.SelectedIndex = _completionIndex;
                    InvalidationCallback?.Invoke();
                    return;
                case ConsoleKey.DownArrow:
                    _completionIndex = (_completionIndex + 1) % _completions.Count;
                    _completionPanel.SelectedIndex = _completionIndex;
                    InvalidationCallback?.Invoke();
                    return;
                case ConsoleKey.Escape:
                    CloseCompletion();
                    return;
                case ConsoleKey.Tab:
                case ConsoleKey.Enter:
                    AcceptCompletion();
                    return;
                default:
                    // Any other key drops the popup. Fall through so the keystroke also
                    // mutates the buffer normally (the user kept typing past the prefix).
                    CloseCompletion();
                    break;
            }
        }

        switch (e.Key)
        {
            case ConsoleKey.C when e.Ctrl:
                // Copy — only handle when there's an active selection so plain
                // Ctrl+C with no selection falls through to whatever the host
                // wants (e.g. cancel-current-command in a future revision).
                // CopyToClipboard clears the selection on success.
                if (HasSelection) { CopyToClipboard(); return; }
                break;
            case ConsoleKey.Escape when HasSelection:
                ClearSelection();
                return;
            case ConsoleKey.Enter when e.Shift:
                // Shift+Enter — insert a newline rather than submit. Lets the user compose
                // multi-line expressions (let … in …, switch arms, pasted scripts).
                ClearInputSelection();
                _input.Insert(_cursorCol, '\n');
                _cursorCol++;
                RecomputeAnalysis();
                InvalidationCallback?.Invoke();
                return;
            case ConsoleKey.Enter:
                ClearInputSelection();
                Submit();
                return;
            case ConsoleKey.Tab:
                OpenCompletion();
                // Always swallow Tab when it lands here directly — the host intercepts
                // Tab via KeyDown and routes through TryHandleCompletionTab when it wants
                // to fall back to focus navigation.
                return;
            case ConsoleKey.Backspace:
                if (_cursorCol > 0)
                {
                    ClearInputSelection();
                    _input.Remove(_cursorCol - 1, 1);
                    _cursorCol--;
                    RecomputeAnalysis();
                    InvalidationCallback?.Invoke();
                }
                return;
            case ConsoleKey.Delete:
                if (_cursorCol < _input.Length)
                {
                    ClearInputSelection();
                    _input.Remove(_cursorCol, 1);
                    RecomputeAnalysis();
                    InvalidationCallback?.Invoke();
                }
                return;
            case ConsoleKey.LeftArrow:
                if (_cursorCol > 0) { _cursorCol--; RecomputeAnalysis(); InvalidationCallback?.Invoke(); }
                return;
            case ConsoleKey.RightArrow:
                if (_cursorCol < _input.Length) { _cursorCol++; RecomputeAnalysis(); InvalidationCallback?.Invoke(); }
                return;
            case ConsoleKey.Home when e.Ctrl:
                ScrollTo(int.MaxValue);
                return;
            case ConsoleKey.End when e.Ctrl:
                ScrollTo(0);
                return;
            case ConsoleKey.Home:
                // Home goes to the start of the current line (matches editor convention).
                _cursorCol = LineColToIndex(CursorToLineCol(_cursorCol).Line, 0);
                RecomputeAnalysis(); InvalidationCallback?.Invoke();
                return;
            case ConsoleKey.End:
                _cursorCol = LineColToIndex(CursorToLineCol(_cursorCol).Line, int.MaxValue);
                RecomputeAnalysis(); InvalidationCallback?.Invoke();
                return;
            case ConsoleKey.UpArrow:
                MoveUpOrHistoryBack();
                return;
            case ConsoleKey.DownArrow:
                MoveDownOrHistoryForward();
                return;
            case ConsoleKey.PageUp:
                ScrollBy(Math.Max(1, _lastOutputHeight - 1));
                return;
            case ConsoleKey.PageDown:
                ScrollBy(-Math.Max(1, _lastOutputHeight - 1));
                return;
        }

        // Printable text input: SDL3 TEXT_INPUT delivers shifted symbols here as KeyChar.
        if (e.KeyChar >= 0x20 && e.KeyChar < 0x7F && !e.Ctrl && !e.Alt)
        {
            ClearInputSelection();
            _input.Insert(_cursorCol, e.KeyChar);
            _cursorCol++;
            RecomputeAnalysis();
            InvalidationCallback?.Invoke();
        }
    }

    /// <summary>Drop a selection only if it lives in the input region; output selections survive editing.</summary>
    private void ClearInputSelection()
    {
        if (_selectionRegion == SelectionRegion.Input) ClearSelection();
    }

    private bool OpenCompletion()
    {
        // Pass Scope through so user-defined `let` bindings show up alongside builtins.
        // Scope is null on first open (before any evaluation has produced bindings) —
        // GetCompletions handles that as "no extras", same as the parameterless overload.
        var (cursorLine, cursorCol) = CursorToLineCol(_cursorCol);
        var items = LanguageService.GetCompletions(_input.ToString(), new Position(cursorLine, cursorCol), Scope);
        if (items.Count == 0)
        {
            // No completions — let the caller (Tab handler) decide what to do; typically
            // fall through to focus navigation so empty-input Tab leaves the REPL.
            return false;
        }

        // Anchor at the start of the partial token to the left of the cursor.
        _completionAnchorCol = FindWordStart(_input.ToString(), _cursorCol);
        _completions = items;
        _completionIndex = 0;

        // Translate to renderer-friendly entries and show the overlay panel
        // anchored at the partial-token position.
        var entries = new CompletionEntry[items.Count];
        for (int i = 0; i < items.Count; i++) entries[i] = ToEntry(items[i]);
        var (anchorX, anchorY) = GetCompletionAnchor();
        _completionPanel.Placement = PlacementMode.Top;
        _completionPanel.ShowAt(anchorX, anchorY, entries, 0);
        InvalidationCallback?.Invoke();
        return true;
    }

    /// <summary>
    /// Tries to handle a Tab keypress as a completion trigger. Returns true if the popup
    /// opened or advanced; false if there's nothing to complete (the host should then let
    /// Tab perform its default behaviour, typically focus navigation).
    /// </summary>
    public bool TryHandleCompletionTab()
    {
        if (_completions is { Count: > 0 })
        {
            AcceptCompletion();
            return true;
        }
        return OpenCompletion();
    }

    private void AcceptCompletion()
    {
        if (_completions is null || _completions.Count == 0) return;
        var item = _completions[_completionIndex];

        // Replace the partial token [_completionAnchorCol, _cursorCol) with item.Label.
        var removeLen = _cursorCol - _completionAnchorCol;
        if (removeLen > 0) _input.Remove(_completionAnchorCol, removeLen);
        _input.Insert(_completionAnchorCol, item.Label);
        _cursorCol = _completionAnchorCol + item.Label.Length;
        CloseCompletion();
        RecomputeAnalysis();
        InvalidationCallback?.Invoke();
    }

    private void CloseCompletion()
    {
        _completions = null;
        _completionIndex = 0;
        if (_completionPanel.IsOpen) _completionPanel.Hide();
        InvalidationCallback?.Invoke();
    }

    private static int FindWordStart(string text, int cursor)
    {
        var s = cursor;
        while (s > 0 && IsIdentifierChar(text[s - 1])) s--;
        return s;
    }

    /// <summary>
    /// Maps the linear cursor index into (line, column) over the current input buffer.
    /// Counts '\n' characters; the column resets to 0 after each. Indices past end-of-input
    /// clamp to the last line's last column.
    /// </summary>
    private (int Line, int Col) CursorToLineCol(int index)
    {
        var line = 0;
        var lineStart = 0;
        var clamped = Math.Clamp(index, 0, _input.Length);
        for (var i = 0; i < clamped; i++)
        {
            if (_input[i] == '\n')
            {
                line++;
                lineStart = i + 1;
            }
        }
        return (line, clamped - lineStart);
    }

    /// <summary>
    /// Reverse of <see cref="CursorToLineCol"/>: convert a (line, col) target to a linear
    /// index, clamping the column to the actual length of <paramref name="line"/>.
    /// </summary>
    private int LineColToIndex(int line, int col)
    {
        if (line < 0) return 0;
        var i = 0;
        var currentLine = 0;
        while (currentLine < line && i < _input.Length)
        {
            if (_input[i] == '\n') currentLine++;
            i++;
        }
        if (currentLine < line)
        {
            return _input.Length;
        }
        var lineStart = i;
        var lineEnd = lineStart;
        while (lineEnd < _input.Length && _input[lineEnd] != '\n') lineEnd++;
        return lineStart + Math.Min(col, lineEnd - lineStart);
    }

    private static bool IsIdentifierChar(char c)
        => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';

    private void RecomputeAnalysis()
    {
        var text = _input.ToString();
        _diagnostics = text.Length == 0
            ? Array.Empty<Diagnostic>()
            : LanguageService.GetDiagnostics(text);
        // Hover needs (line, character) — convert from the linear cursor offset so the
        // service sees the right token even when the buffer spans multiple lines.
        var (cursorLine, cursorCol) = CursorToLineCol(_cursorCol);
        _hover = text.Length == 0
            ? null
            : LanguageService.GetHover(text, new Position(cursorLine, cursorCol), Scope);

        RecomputeSignatureHelp(text, cursorLine, cursorCol);
    }

    /// <summary>
    /// Refresh the signature-help overlay based on the current cursor. When
    /// the cursor sits inside a known callable's argument list, show a
    /// <see cref="HoverPanel"/> just above the input row with the signature
    /// (active parameter underlined) and the documentation below it. When it
    /// doesn't, hide the panel.
    /// </summary>
    private void RecomputeSignatureHelp(string text, int cursorLine, int cursorCol)
    {
        var sig = text.Length == 0
            ? null
            : LanguageService.GetSignatureHelp(text, new Position(cursorLine, cursorCol), Scope);
        _activeSignature = sig;
        if (sig is null)
        {
            if (_signaturePanel.IsOpen) _signaturePanel.Hide();
            return;
        }

        // Build the panel content: signature line on top with the active param
        // emphasised, documentation underneath in a dimmer color.
        var sb = new StringBuilder();
        sb.Append(sig.Label);
        if (sig.ActiveParameter >= 0 && sig.ActiveParameter < sig.Parameters.Length)
        {
            var p = sig.Parameters[sig.ActiveParameter];
            sb.Append("\n").Append(new string(' ', p.LabelStart)).Append(new string('▔', p.LabelLength));
        }
        if (sig.Documentation is not null)
        {
            sb.Append("\n\n").Append(sig.Documentation);
        }

        var content = new Border
        {
            Child = new TextBlock { Text = sb.ToString(), Padding = new Thickness(1, 0) },
            BorderStyle = BorderStyle.Rounded(new Color(0x89, 0xB4, 0xFA)),
        };

        // Anchor on the input row that holds the cursor (multi-line buffers
        // place the cursor on a row != input top).
        var anchorY = _lastInputTopY + cursorLine;
        var promptWidth = Math.Max(Prompt.Length, ContinuationPrompt.Length);
        var anchorX = _lastBounds.X + promptWidth + cursorCol;
        _signaturePanel.Placement = PlacementMode.Top;
        _signaturePanel.ShowAt(anchorX, anchorY, content);
    }

    private void Submit()
    {
        var line = _input.ToString();
        _input.Clear();
        _cursorCol = 0;
        _historyIndex = -1;
        _diagnostics = Array.Empty<Diagnostic>();
        _hover = null;
        _activeSignature = null;
        if (_signaturePanel.IsOpen) _signaturePanel.Hide();

        // Echo the buffer back into the output with prompts in front of each line so the
        // history reads like a transcript. Continuation rows use ContinuationPrompt to
        // match how the input was displayed while the user was typing it.
        var lines = line.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var prefix = i == 0 ? Prompt : ContinuationPrompt;
            AppendOutput(prefix + lines[i]);
        }

        if (!string.IsNullOrWhiteSpace(line))
        {
            _history.Add(line);
            CommandEntered?.Invoke(line);
        }
        else
        {
            InvalidationCallback?.Invoke();
        }
    }

    /// <summary>
    /// Up arrow: if the cursor is on the first line of a multi-line buffer (or the buffer
    /// is single-line), walk history backwards; otherwise move the cursor up one line,
    /// preserving the visual column when possible.
    /// </summary>
    private void MoveUpOrHistoryBack()
    {
        var (line, col) = CursorToLineCol(_cursorCol);
        if (line > 0)
        {
            _cursorCol = LineColToIndex(line - 1, col);
            RecomputeAnalysis();
            InvalidationCallback?.Invoke();
            return;
        }
        NavigateHistory(-1);
    }

    /// <summary>
    /// Down arrow: opposite of <see cref="MoveUpOrHistoryBack"/>. On the last line of a
    /// multi-line buffer, walks history forwards; otherwise drops one line.
    /// </summary>
    private void MoveDownOrHistoryForward()
    {
        var (line, col) = CursorToLineCol(_cursorCol);
        var totalLines = CountInputLines();
        if (line < totalLines - 1)
        {
            _cursorCol = LineColToIndex(line + 1, col);
            RecomputeAnalysis();
            InvalidationCallback?.Invoke();
            return;
        }
        NavigateHistory(+1);
    }

    private void NavigateHistory(int direction)
    {
        if (_history.Count == 0) return;

        if (_historyIndex == -1 && direction < 0)
        {
            _historyIndex = _history.Count - 1;
        }
        else if (_historyIndex >= 0)
        {
            _historyIndex = Math.Clamp(_historyIndex + direction, -1, _history.Count - 1);
        }

        _input.Clear();
        if (_historyIndex >= 0 && _historyIndex < _history.Count)
        {
            _input.Append(_history[_historyIndex]);
        }
        _cursorCol = _input.Length;
        RecomputeAnalysis();
        InvalidationCallback?.Invoke();
    }

    // ─── Mouse hover ────────────────────────────────────────────────────────

    /// <inheritdoc />
    public override void OnMouseEvent(MouseEvent e)
    {
        // Wheel events scroll the output history. We handle them before the
        // Move-only filter below so the wheel works whether or not the mouse
        // is over the panel's bounds (the hit-test that routed the event here
        // already confirmed the pointer is over the REPL).
        if (e.Action == MouseAction.ScrollUp)
        {
            ScrollBy(3);
            return;
        }
        if (e.Action == MouseAction.ScrollDown)
        {
            ScrollBy(-3);
            return;
        }

        // Selection — left button press starts a drag, Move while dragging
        // tracks the head, Release ends the drag (but keeps the selection so
        // Ctrl+C can copy afterwards).
        if (e.Button == MouseButton.Left && e.Action == MouseAction.Press)
        {
            BeginSelection(e);
            return;
        }
        // Right-click — copy the active selection (if any) and clear it. The
        // press alone is enough; we don't track a drag for right-button.
        if (e.Button == MouseButton.Right && e.Action == MouseAction.Press)
        {
            if (HasSelection) CopyToClipboard();
            return;
        }
        if (_isMouseDragging && e.Action == MouseAction.Move)
        {
            ExtendSelection(e.X, e.Y);
            return;
        }
        if (e.Button == MouseButton.Left && e.Action == MouseAction.Release)
        {
            _isMouseDragging = false;
            return;
        }

        if (e.Action != MouseAction.Move) return;
        if (_lastBounds.Width <= 0 || _lastBounds.Height <= 0) return;

        // Mouse left the panel entirely.
        if (e.X < _lastBounds.X || e.X >= _lastBounds.Right
         || e.Y < _lastBounds.Y || e.Y >= _lastBounds.Bottom)
        {
            HideMouseHover();
            return;
        }

        var cell = (X: e.X, Y: e.Y);
        if (_lastMouseHoverCell == cell) return;
        _lastMouseHoverCell = cell;

        // Multi-line input occupies the bottom N rows. inputTopY ≤ mouse.Y ≤ panel bottom
        // means the mouse is on one of the input lines; the line within the buffer is the
        // delta from inputTopY.
        var inputLines = Math.Min(CountInputLines(), Math.Max(1, _lastBounds.Height / 2));
        var inputBottomY = _lastBounds.Y + _lastBounds.Height - 1;
        var inputTopY = inputBottomY - (inputLines - 1);

        if (e.Y >= inputTopY && e.Y <= inputBottomY)
        {
            ShowInputHover(e.X, e.Y - inputTopY);
            return;
        }

        if (TryGetOutputValueAtRow(e.Y, out var value))
        {
            ShowValueHover(value, e.X, e.Y);
            return;
        }

        HideMouseHover();
    }

    private void ShowInputHover(int mouseX, int inputRow)
    {
        var promptWidth = Math.Max(Prompt.Length, ContinuationPrompt.Length);
        int promptStartX = _lastBounds.X;
        int colInLine = mouseX - promptStartX - promptWidth;
        var text = _input.ToString();
        if (colInLine < 0)
        {
            HideMouseHover();
            return;
        }

        var totalLines = CountInputLines();
        if (inputRow < 0 || inputRow >= totalLines)
        {
            HideMouseHover();
            return;
        }

        var hover = LanguageService.GetHover(text, new Position(inputRow, colInLine), Scope);
        if (hover is null)
        {
            HideMouseHover();
            return;
        }

        _hoverBox.Language = "ninja";
        var content = BuildHoverContent(hover.Contents);
        // Anchor on the current input row, not the panel bottom — multi-line input might
        // sit several rows above the bottom.
        var inputBottomY = _lastBounds.Y + _lastBounds.Height - 1;
        var inputTopY = inputBottomY - (totalLines - 1);
        int anchorY = inputTopY + inputRow;
        _hoverPanel.Placement = PlacementMode.Top;
        _hoverPanel.ShowAt(mouseX, anchorY, content);
    }

    private void ShowValueHover(NValue value, int mouseX, int mouseY)
    {
        var sb = new StringBuilder();
        sb.Append("result :: ").AppendLine(ValueFormatter.TypeName(value));
        sb.AppendLine();
        sb.Append("shape: ").AppendLine(ValueFormatter.Def(value));
        sb.Append("data:  ").Append(ValueFormatter.Dump(value));

        // Value hovers surface obj.dump-style payloads — drive the highlighter
        // with the record grammar so keys/values are visually distinguishable.
        _hoverBox.Language = "record";
        var content = BuildHoverContent(sb.ToString());
        _hoverPanel.Placement = PlacementMode.Bottom;
        _hoverPanel.ShowAt(mouseX, mouseY, content);
    }

    private bool TryGetOutputValueAtRow(int row, out NValue value)
    {
        value = NUnit.Instance;
        var inputLines = Math.Min(CountInputLines(), Math.Max(1, _lastBounds.Height / 2));
        int outputHeight = _lastBounds.Height
            - inputLines
            - (HasDiagnostic() ? 1 : 0)
            - (_hover is not null ? 1 : 0);
        if (outputHeight <= 0) return false;

        // Row index inside the visible output region (0-based from the top of the panel).
        int rowInPanel = row - _lastBounds.Y;
        if (rowInPanel < 0 || rowInPanel >= outputHeight) return false;

        int firstVisible = Math.Max(0, _outputLines.Count - outputHeight - _scrollOffset);
        int lineIndex = firstVisible + rowInPanel;
        if (lineIndex < 0 || lineIndex >= _outputLines.Count) return false;

        return _outputResults.TryGetValue(lineIndex, out value!);
    }

    private void HideMouseHover()
    {
        if (_hoverPanel.IsOpen) _hoverPanel.Hide();
        _lastMouseHoverCell = null;
    }

    /// <summary>
    /// Build a bounded, syntax-highlighted hover content element. Cached on the
    /// REPL so PgUp / PgDn / Ctrl+↑ / Ctrl+↓ can scroll it while it's open.
    /// </summary>
    private UIElement BuildHoverContent(string text)
    {
        _hoverBox.Text = text;
        _hoverBox.Theme = Theme;
        return _hoverBox;
    }
}
