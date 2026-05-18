using TerminalNinja.Buffers;
using TerminalNinja.Controls;
using TerminalNinja.Highlighting;
using TerminalNinja.Input;
using TerminalNinja.Primitives;
using TerminalNinja.Shell.Language.Services;
using TerminalNinja.Shell.Values;

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
/// pure-function surface the standalone LSP server consumes. Each keystroke kicks
/// <see cref="LanguageAnalysis.Recompute"/> which refreshes diagnostics + hover, and
/// <see cref="SignatureHelpController.Refresh"/> which (re)opens the signature tooltip.
/// </para>
/// <para>
/// Tab triggers a completion popup overlaid above the prompt; up/down cycles through the
/// items, Enter (or Tab) accepts and replaces the partial token, Esc dismisses.
/// </para>
/// <para>
/// <c>Enter</c> submits the entire buffer (which may span multiple lines).
/// <c>Shift+Enter</c> inserts a newline so the user can compose <c>let … in …</c> blocks,
/// switch expressions, or pasted multi-statement scripts in place. Continuation rows
/// show a dimmed <c>"... "</c> prefix instead of the primary <c>Prompt</c>.
/// </para>
/// <para>
/// This class is intentionally thin: state lives in the collaborators
/// (<see cref="InputBuffer"/>, <see cref="InputHistory"/>, <see cref="OutputLog"/>,
/// <see cref="SelectionModel"/>, <see cref="LanguageAnalysis"/>) and rendering /
/// overlay lifecycle lives in the renderers + controllers under <c>Repl/</c>.
/// </para>
/// </remarks>
public sealed class ReplView : Control
{
    private readonly InputBuffer _input = new();
    private readonly InputHistory _history = new();
    private readonly OutputLog _output = new();
    private readonly SelectionModel _selection = new();
    private readonly LanguageAnalysis _analysis = new();

    private readonly OutputRenderer _outputRenderer;
    private readonly InputRenderer _inputRenderer;
    private readonly CompletionController _completion;
    private readonly SignatureHelpController _signatureHelp = new();
    private readonly MouseHoverController _mouseHover = new();

    private ReplLayout _layout;

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

    /// <summary>
    /// Live scope used to enrich hover info with shape + data for user-defined bindings.
    /// The host (e.g. <see cref="ShellViewModel"/>) sets this from the evaluator's
    /// <c>Env.Bindings</c> snapshot after each evaluation.
    /// </summary>
    public IReadOnlyDictionary<string, NValue>? Scope { get; set; }

    /// <summary>Raised when the user presses Enter on a non-empty input line.</summary>
    public event Action<string>? CommandEntered;

    /// <summary>Creates a focusable REPL view.</summary>
    public ReplView()
    {
        Focusable = true;
        _outputRenderer = new OutputRenderer(_output, _selection);
        _inputRenderer = new InputRenderer(_input, _selection);
        _completion = new CompletionController(_input, Invalidate);
    }

    /// <inheritdoc />
    public override Size2D GetPreferredSize(Rect availableSpace) => new(availableSpace.Width, availableSpace.Height);

    /// <inheritdoc />
    public override Rect CalculateBounds(Rect parentBounds) => parentBounds;

    /// <summary>Appends <paramref name="text"/> to the output buffer, splitting on newlines.</summary>
    public void AppendOutput(string? text)
    {
        if (_output.Append(text) < 0) return;
        _outputRenderer.ScrollToBottom();
        Invalidate();
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
        if (_output.Append(text, value) < 0) return;
        _outputRenderer.ScrollToBottom();
        Invalidate();
    }

    /// <summary>Removes every line from the output buffer.</summary>
    public void ClearOutput()
    {
        _output.Clear();
        _outputRenderer.ScrollTo(0, _layout.OutputHeight);
        _mouseHover.Hide();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnRender(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            _layout = default;
            return;
        }

        var promptWidth = Math.Max(Prompt.Length, ContinuationPrompt.Length);
        _layout = ReplLayout.Compute(
            bounds,
            _input.CountLines(),
            _analysis.HasDiagnostic,
            _analysis.Hover is not null,
            promptWidth);

        _outputRenderer.Clamp(_layout.OutputHeight);

        var fg = Foreground;
        var bg = Background;

        CellPaint.ClearRegion(buffer, bounds, bg);
        _outputRenderer.Render(buffer, _layout, fg, bg);

        if (_layout.HoverY > -1 && _analysis.Hover is not null)
            StatusLineRenderer.RenderHover(buffer, bounds.X, _layout.HoverY, bounds.Width, _analysis.Hover, bg);
        if (_layout.DiagY > -1 && _analysis.HasDiagnostic)
            StatusLineRenderer.RenderDiagnostic(buffer, bounds.X, _layout.DiagY, bounds.Width, _analysis.Diagnostics[0], Prompt.Length, bg);

        _inputRenderer.Render(buffer, _layout, Prompt, ContinuationPrompt, HighlightLanguage, Theme, _analysis.Diagnostics, fg, bg);

        // Completion + signature popups render on the overlay stack via their controllers.
    }

    // ─── Key handling ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public override void OnKeyEvent(KeyEvent e)
    {
        // Hover panel scroll: while the mouse-hover overlay is open, PgUp/PgDn
        // and Ctrl+up/down scroll its contents. Keys the hover doesn't consume
        // fall through to normal input handling.
        if (_mouseHover.ForwardKey(e))
        {
            Invalidate();
            return;
        }

        // Completion popup eats Up/Down/Enter/Esc/Tab while open. The popup is
        // dismissed by Esc or by any keystroke that changes the buffer.
        var compResult = _completion.HandleKey(e);
        if (compResult == CompletionKeyResult.Consumed)
        {
            // Accept may have mutated the buffer — refresh analysis after.
            if (e.Key is ConsoleKey.Tab or ConsoleKey.Enter) RecomputeAnalysis();
            return;
        }
        // ClosedFallthrough: popup closed itself, keep going with the same key.

        switch (e.Key)
        {
            case ConsoleKey.C when e.Ctrl:
                if (_selection.HasSelection) { CopyToClipboard(); return; }
                break;
            case ConsoleKey.Escape when _selection.HasSelection:
                ClearSelection();
                return;
            case ConsoleKey.Enter when e.Shift:
                // Shift+Enter — insert a newline rather than submit.
                ClearInputSelection();
                _input.Insert(_input.CursorCol, '\n');
                _input.CursorCol++;
                RecomputeAnalysis();
                Invalidate();
                return;
            case ConsoleKey.Enter:
                ClearInputSelection();
                Submit();
                return;
            case ConsoleKey.Tab:
                OpenCompletion();
                // Always swallow Tab here — the host intercepts Tab via KeyDown
                // and routes through TryHandleCompletionTab for focus-nav fallback.
                return;
            case ConsoleKey.Backspace:
                if (_input.CursorCol > 0)
                {
                    ClearInputSelection();
                    _input.Remove(_input.CursorCol - 1, 1);
                    _input.CursorCol--;
                    RecomputeAnalysis();
                    Invalidate();
                }
                return;
            case ConsoleKey.Delete:
                if (_input.CursorCol < _input.Length)
                {
                    ClearInputSelection();
                    _input.Remove(_input.CursorCol, 1);
                    RecomputeAnalysis();
                    Invalidate();
                }
                return;
            case ConsoleKey.LeftArrow:
                if (_input.CursorCol > 0) { _input.CursorCol--; RecomputeAnalysis(); Invalidate(); }
                return;
            case ConsoleKey.RightArrow:
                if (_input.CursorCol < _input.Length) { _input.CursorCol++; RecomputeAnalysis(); Invalidate(); }
                return;
            case ConsoleKey.Home when e.Ctrl:
                _outputRenderer.ScrollTo(int.MaxValue, _layout.OutputHeight);
                Invalidate();
                return;
            case ConsoleKey.End when e.Ctrl:
                _outputRenderer.ScrollTo(0, _layout.OutputHeight);
                Invalidate();
                return;
            case ConsoleKey.Home:
                _input.CursorCol = _input.LineColToIndex(_input.CursorToLineCol(_input.CursorCol).Line, 0);
                RecomputeAnalysis();
                Invalidate();
                return;
            case ConsoleKey.End:
                _input.CursorCol = _input.LineColToIndex(_input.CursorToLineCol(_input.CursorCol).Line, int.MaxValue);
                RecomputeAnalysis();
                Invalidate();
                return;
            case ConsoleKey.UpArrow:
                MoveUpOrHistoryBack();
                return;
            case ConsoleKey.DownArrow:
                MoveDownOrHistoryForward();
                return;
            case ConsoleKey.PageUp:
                _outputRenderer.ScrollBy(Math.Max(1, _layout.OutputHeight - 1), _layout.OutputHeight);
                Invalidate();
                return;
            case ConsoleKey.PageDown:
                _outputRenderer.ScrollBy(-Math.Max(1, _layout.OutputHeight - 1), _layout.OutputHeight);
                Invalidate();
                return;
        }

        // Printable text input: SDL3 TEXT_INPUT delivers shifted symbols here as KeyChar.
        if (e.KeyChar >= 0x20 && e.KeyChar < 0x7F && !e.Ctrl && !e.Alt)
        {
            ClearInputSelection();
            _input.Insert(_input.CursorCol, e.KeyChar);
            _input.CursorCol++;
            RecomputeAnalysis();
            Invalidate();
        }
    }

    /// <summary>
    /// Tries to handle a Tab keypress as a completion trigger. Returns true if the popup
    /// opened or advanced; false if there's nothing to complete (the host should then let
    /// Tab perform its default behaviour, typically focus navigation).
    /// </summary>
    public bool TryHandleCompletionTab()
    {
        if (_completion.IsOpen)
        {
            _completion.Accept();
            RecomputeAnalysis();
            return true;
        }
        return OpenCompletion();
    }

    private bool OpenCompletion()
    {
        var opened = _completion.Open(Scope, _layout);
        if (opened) Invalidate();
        return opened;
    }

    // ─── Mouse handling ─────────────────────────────────────────────────────

    /// <inheritdoc />
    public override void OnMouseEvent(MouseEvent e)
    {
        // Wheel events scroll the output history. We handle them before the
        // Move-only filter below so the wheel works whether or not the mouse
        // is over the panel's bounds (the hit-test that routed the event here
        // already confirmed the pointer is over the REPL).
        if (e.Action == MouseAction.ScrollUp)
        {
            _outputRenderer.ScrollBy(3, _layout.OutputHeight);
            Invalidate();
            return;
        }
        if (e.Action == MouseAction.ScrollDown)
        {
            _outputRenderer.ScrollBy(-3, _layout.OutputHeight);
            Invalidate();
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
        // Right-click — copy the active selection (if any) and clear it.
        if (e.Button == MouseButton.Right && e.Action == MouseAction.Press)
        {
            if (_selection.HasSelection) CopyToClipboard();
            return;
        }
        if (_selection.IsMouseDragging && e.Action == MouseAction.Move)
        {
            ExtendSelection(e.X, e.Y);
            return;
        }
        if (e.Button == MouseButton.Left && e.Action == MouseAction.Release)
        {
            _selection.IsMouseDragging = false;
            return;
        }

        if (e.Action != MouseAction.Move) return;
        if (_layout.IsEmpty) return;

        var bounds = _layout.Bounds;
        if (e.X < bounds.X || e.X >= bounds.Right
         || e.Y < bounds.Y || e.Y >= bounds.Bottom)
        {
            HideMouseHover();
            return;
        }

        if (!_mouseHover.TryMoveCursor(e.X, e.Y)) return;

        // Multi-line input occupies the bottom N rows.
        var inputLines = Math.Min(_input.CountLines(), Math.Max(1, bounds.Height / 2));
        var inputBottomY = bounds.Y + bounds.Height - 1;
        var inputTopY = inputBottomY - (inputLines - 1);

        if (e.Y >= inputTopY && e.Y <= inputBottomY)
        {
            ShowInputHover(e.X, e.Y - inputTopY);
            return;
        }

        if (TryGetOutputValueAtRow(e.Y, out var value))
        {
            _mouseHover.Theme = Theme;
            _mouseHover.ShowValueHover(value, e.X, e.Y);
            return;
        }

        HideMouseHover();
    }

    private void ShowInputHover(int mouseX, int inputRow)
    {
        var promptWidth = Math.Max(Prompt.Length, ContinuationPrompt.Length);
        int colInLine = mouseX - _layout.Bounds.X - promptWidth;
        if (colInLine < 0)
        {
            HideMouseHover();
            return;
        }

        var totalLines = _input.CountLines();
        if (inputRow < 0 || inputRow >= totalLines)
        {
            HideMouseHover();
            return;
        }

        var inputBottomY = _layout.Bounds.Y + _layout.Bounds.Height - 1;
        var inputTopY = inputBottomY - (totalLines - 1);
        int anchorY = inputTopY + inputRow;

        _mouseHover.Theme = Theme;
        _mouseHover.ShowInputHover(mouseX, anchorY, _input.Text, new Position(inputRow, colInLine), Scope);
    }

    private bool TryGetOutputValueAtRow(int row, out NValue value)
    {
        value = NUnit.Instance;
        var inputLines = Math.Min(_input.CountLines(), Math.Max(1, _layout.Bounds.Height / 2));
        int outputHeight = _layout.Bounds.Height
            - inputLines
            - (_analysis.HasDiagnostic ? 1 : 0)
            - (_analysis.Hover is not null ? 1 : 0);
        if (outputHeight <= 0) return false;

        int rowInPanel = row - _layout.Bounds.Y;
        if (rowInPanel < 0 || rowInPanel >= outputHeight) return false;

        int lineIndex = _outputRenderer.LineIndexForRow(rowInPanel, outputHeight);
        if (lineIndex < 0) return false;

        return _output.TryGetValueAt(lineIndex, out value);
    }

    private void HideMouseHover() => _mouseHover.Hide();

    // ─── Selection ──────────────────────────────────────────────────────────

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
        if (_completion.IsOpen) _completion.Close();
        HideMouseHover();

        if (e.Shift && _selection.Region == hit.Region)
        {
            _selection.ExtendHead(hit.Row, hit.Col);
        }
        else
        {
            _selection.Begin(hit.Region, hit.Row, hit.Col, e.Alt);
        }

        _selection.IsMouseDragging = true;
        Invalidate();
    }

    /// <summary>Drag-update — move only the head; clamp to the same region as the anchor.</summary>
    private void ExtendSelection(int mouseX, int mouseY)
    {
        if (_selection.Region == SelectionRegion.None) return;
        var hit = HitTest(mouseX, mouseY);
        if (hit.Region != _selection.Region) return;
        if (_selection.Head.Row == hit.Row && _selection.Head.Col == hit.Col) return;
        _selection.ExtendHead(hit.Row, hit.Col);
        Invalidate();
    }

    /// <summary>
    /// Map a screen (mouseX, mouseY) cell to a (region, row, col) tuple in the
    /// region's own coordinate system. Returns None when the click landed
    /// outside both regions.
    /// </summary>
    private (SelectionRegion Region, int Row, int Col) HitTest(int mouseX, int mouseY)
    {
        if (_layout.IsEmpty) return (SelectionRegion.None, 0, 0);

        var bounds = _layout.Bounds;
        int inputBottomY = bounds.Y + bounds.Height - 1;
        int inputTopY = _layout.InputTopY;
        if (mouseY >= inputTopY && mouseY <= inputBottomY)
        {
            int row = mouseY - inputTopY;
            int promptWidth = Math.Max(Prompt.Length, ContinuationPrompt.Length);
            int col = Math.Max(0, mouseX - bounds.X - promptWidth);
            var lines = _input.Lines();
            if (row >= 0 && row < lines.Count) col = Math.Min(col, lines[row].Length);
            return (SelectionRegion.Input, row, col);
        }

        if (mouseY >= bounds.Y && mouseY < bounds.Y + _layout.OutputHeight)
        {
            int rowInPanel = mouseY - bounds.Y;
            int lineIndex = _outputRenderer.LineIndexForRow(rowInPanel, _layout.OutputHeight);
            if (lineIndex < 0) return (SelectionRegion.None, 0, 0);
            int col = Math.Max(0, mouseX - bounds.X);
            col = Math.Min(col, _output.Lines[lineIndex].Length);
            return (SelectionRegion.Output, lineIndex, col);
        }

        return (SelectionRegion.None, 0, 0);
    }

    /// <summary>Drop a selection only if it lives in the input region; output selections survive editing.</summary>
    private void ClearInputSelection()
    {
        if (_selection.Region == SelectionRegion.Input) ClearSelection();
    }

    private void ClearSelection()
    {
        if (_selection.Region == SelectionRegion.None) return;
        _selection.Clear();
        Invalidate();
    }

    /// <summary>
    /// Copy the current selection to the OS clipboard. On success the selection
    /// is cleared (so the next keystroke / click doesn't see stale highlight).
    /// Failures surface as a single output line so the user knows why nothing
    /// landed on the clipboard.
    /// </summary>
    private bool CopyToClipboard()
    {
        if (!_selection.HasSelection) return false;

        var source = _selection.Region == SelectionRegion.Output
            ? _output.Lines
            : _input.Lines();
        var text = _selection.BuildText(source);
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

        ClearSelection();
        return true;
    }

    // ─── Submit / history ──────────────────────────────────────────────────

    private void Submit()
    {
        var line = _input.Text;
        _input.Clear();
        _history.ResetCursor();
        _analysis.Reset();
        _signatureHelp.Hide();

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
            _history.Push(line);
            CommandEntered?.Invoke(line);
        }
        else
        {
            Invalidate();
        }
    }

    /// <summary>
    /// Up arrow: if the cursor is on the first line of a multi-line buffer (or the buffer
    /// is single-line), walk history backwards; otherwise move the cursor up one line,
    /// preserving the visual column when possible.
    /// </summary>
    private void MoveUpOrHistoryBack()
    {
        var (line, col) = _input.CursorToLineCol(_input.CursorCol);
        if (line > 0)
        {
            _input.CursorCol = _input.LineColToIndex(line - 1, col);
            RecomputeAnalysis();
            Invalidate();
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
        var (line, col) = _input.CursorToLineCol(_input.CursorCol);
        var totalLines = _input.CountLines();
        if (line < totalLines - 1)
        {
            _input.CursorCol = _input.LineColToIndex(line + 1, col);
            RecomputeAnalysis();
            Invalidate();
            return;
        }
        NavigateHistory(+1);
    }

    private void NavigateHistory(int direction)
    {
        if (_history.Count == 0) return;
        var entry = _history.Navigate(direction);
        _input.Replace(entry ?? string.Empty);
        RecomputeAnalysis();
        Invalidate();
    }

    // ─── Analysis ──────────────────────────────────────────────────────────

    private void RecomputeAnalysis()
    {
        var text = _input.Text;
        var (cursorLine, cursorCol) = _input.CursorToLineCol(_input.CursorCol);
        _analysis.Recompute(text, new Position(cursorLine, cursorCol), Scope);
        _signatureHelp.Refresh(text, cursorLine, cursorCol, Scope, _layout);
    }

    private void Invalidate() => InvalidationCallback?.Invoke();
}
