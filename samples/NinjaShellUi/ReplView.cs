using System.Text;
using TerminalNinja.Buffers;
using TerminalNinja.Controls;
using TerminalNinja.Highlighting;
using TerminalNinja.Input;
using TerminalNinja.Primitives;
using TerminalNinja.Shell.Language.Services;

namespace NinjaShellUi;

/// <summary>
/// A minimal terminal-style REPL surface: a scrolling output buffer at the top and a
/// single-line input prompt at the bottom. Owns its own input state (no <c>TextBox</c>),
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
/// </remarks>
public sealed class ReplView : Control
{
    private readonly List<string> _outputLines = new(capacity: 256);
    private readonly StringBuilder _input = new();
    private readonly List<string> _history = new();
    private int _historyIndex = -1;
    private int _cursorCol;
    private int _scrollOffset;

    // LSP-shaped derived state, recomputed on every input change.
    private IReadOnlyList<Diagnostic> _diagnostics = Array.Empty<Diagnostic>();
    private Hover? _hover;

    // Completion popup state. Built fresh on each Tab press from the current cursor;
    // surviving keystrokes (Up / Down / Enter / Esc) navigate or dismiss it.
    private IReadOnlyList<CompletionItem>? _completions;
    private int _completionIndex;
    private int _completionAnchorCol;

    /// <summary>The prompt rendered in front of the input line. Defaults to <c>"ninja&gt; "</c>.</summary>
    public string Prompt { get; set; } = "ninja> ";

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

    /// <summary>Removes every line from the output buffer.</summary>
    public void ClearOutput()
    {
        _outputLines.Clear();
        _scrollOffset = 0;
        InvalidationCallback?.Invoke();
    }

    /// <inheritdoc />
    protected override void OnRender(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        // Bottom row = input prompt. Two optional info rows above it: hover + diagnostic.
        // Each is only allocated when there's something to show, so a clean input keeps
        // the full output area visible.
        var promptY = bounds.Y + bounds.Height - 1;
        var diagY = HasDiagnostic() ? promptY - 1 : -1;
        var hoverY = (_hover is not null && diagY > 0) ? diagY - 1
                   : _hover is not null ? promptY - 1
                   : -1;

        var topReserved = (promptY > -1 ? 1 : 0) + (diagY > -1 ? 1 : 0) + (hoverY > -1 ? 1 : 0);
        var outputHeight = Math.Max(0, bounds.Height - topReserved);

        var fg = Foreground;
        var bg = Background;
        ClearRegion(buffer, bounds, bg);
        RenderOutput(buffer, bounds.X, bounds.Y, bounds.Width, outputHeight, fg, bg);

        if (hoverY > -1) RenderHoverLine(buffer, bounds.X, hoverY, bounds.Width, bg);
        if (diagY > -1) RenderDiagnosticLine(buffer, bounds.X, diagY, bounds.Width, bg);

        RenderPromptLine(buffer, bounds.X, promptY, bounds.Width, fg, bg);

        // The completion popup sits on top of everything else — render last.
        if (_completions is { Count: > 0 })
        {
            RenderCompletionPopup(buffer, bounds, promptY, fg, bg);
        }
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
        }
    }

    private void RenderPromptLine(CellBuffer buffer, int x, int y, int width, Color fg, Color bg)
    {
        DrawText(buffer, x, y, Prompt, width, fg, bg);
        var inputX = x + Prompt.Length;
        var inputWidth = Math.Max(0, width - Prompt.Length);
        DrawHighlightedInput(buffer, inputX, y, _input.ToString(), inputWidth, fg, bg);

        // Cursor: invert fg/bg on the cell at the cursor position.
        var cursorX = inputX + Math.Min(_cursorCol, inputWidth - 1);
        if (cursorX >= inputX && cursorX < inputX + inputWidth && (uint)y < (uint)buffer.Height)
        {
            var cell = buffer.GetCell(cursorX, y);
            buffer.SetCell(cursorX, y, new Cell(cell.Codepoint, cell.Background, cell.Foreground, cell.Decorations, cell.Flags));
        }
    }

    /// <summary>
    /// Renders the input buffer with per-token colours. Resolves the configured
    /// <see cref="HighlightLanguage"/> through <see cref="SyntaxHighlighterRegistry"/>;
    /// if no highlighter is registered, falls back to drawing the text plain.
    /// </summary>
    private void DrawHighlightedInput(CellBuffer buffer, int x, int y, string text, int maxWidth, Color fallbackFg, Color bg)
    {
        if (text.Length == 0 || maxWidth <= 0 || (uint)y >= (uint)buffer.Height) return;

        ISyntaxHighlighter? highlighter = null;
        if (HighlightLanguage is not null)
        {
            SyntaxHighlighterRegistry.TryGet(HighlightLanguage, out highlighter);
        }

        if (highlighter is null)
        {
            DrawText(buffer, x, y, text, maxWidth, fallbackFg, bg);
            return;
        }

        var tokens = highlighter.Tokenize(text);
        // Walk char-by-char, advancing through the token list in lock-step. Characters that
        // don't fall inside any token use the fallback foreground (whitespace, gaps).
        var tokenIdx = 0;
        for (var i = 0; i < text.Length && i < maxWidth; i++)
        {
            // Advance past tokens that ended before this offset.
            while (tokenIdx < tokens.Count && tokens[tokenIdx].Start + tokens[tokenIdx].Length <= i)
            {
                tokenIdx++;
            }

            var fg = fallbackFg;
            if (tokenIdx < tokens.Count)
            {
                var t = tokens[tokenIdx];
                if (i >= t.Start && i < t.Start + t.Length)
                {
                    fg = Theme.GetColor(t.Kind);
                }
            }

            var cx = x + i;
            if ((uint)cx >= (uint)buffer.Width) break;
            buffer.SetChar(cx, y, text[i], fg, bg);
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

    private void RenderCompletionPopup(CellBuffer buffer, Rect bounds, int promptY, Color fg, Color bg)
    {
        var items = _completions!;
        var popupHeight = Math.Min(items.Count, 8);
        if (popupHeight <= 0) return;

        // Width = longest item label + " — detail" tail, capped by the panel width.
        var maxLabel = 0;
        var maxDetail = 0;
        for (var i = 0; i < items.Count; i++)
        {
            maxLabel = Math.Max(maxLabel, items[i].Label.Length);
            maxDetail = Math.Max(maxDetail, items[i].Detail?.Length ?? 0);
        }
        var popupWidth = Math.Min(bounds.Width - 2, maxLabel + 3 + maxDetail);
        if (popupWidth < 10) popupWidth = Math.Min(bounds.Width - 2, 30);

        // Anchor: the popup floats just above the prompt, starting at the column where
        // the completion was triggered (the start of the partial token).
        var popupX = bounds.X + Math.Min(bounds.Width - popupWidth - 1, Math.Max(0, Prompt.Length + _completionAnchorCol));
        var popupY = Math.Max(bounds.Y, promptY - popupHeight);

        var popupBg = new Color(0x31, 0x32, 0x44);
        var popupSelBg = new Color(0x45, 0x47, 0x5A);

        // Window the visible items so the selection stays in view.
        var firstVisible = Math.Max(0, Math.Min(items.Count - popupHeight, _completionIndex - popupHeight / 2));
        for (var r = 0; r < popupHeight; r++)
        {
            var itemIndex = firstVisible + r;
            if (itemIndex >= items.Count) break;
            var rowBg = itemIndex == _completionIndex ? popupSelBg : popupBg;
            var y = popupY + r;
            FillRow(buffer, popupX, y, popupWidth, rowBg);

            var item = items[itemIndex];
            DrawText(buffer, popupX + 1, y, item.Label, popupWidth - 2, fg, rowBg);
            if (item.Detail is not null)
            {
                var detailX = popupX + 1 + item.Label.Length + 2;
                if (detailX < popupX + popupWidth - 1)
                {
                    var dimFg = new Color(0x9C, 0xA0, 0xB0);
                    DrawText(buffer, detailX, y, item.Detail, popupX + popupWidth - 1 - detailX, dimFg, rowBg);
                }
            }
        }
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
        for (var i = 0; i < text.Length && i < maxWidth; i++)
        {
            var cx = x + i;
            if ((uint)cx >= (uint)buffer.Width) break;
            buffer.SetChar(cx, y, text[i], fg, bg);
        }
    }

    private void ScrollToBottom() => _scrollOffset = 0;

    /// <inheritdoc />
    public override void OnKeyEvent(KeyEvent e)
    {
        // Completion popup eats Up / Down / Enter / Esc / Tab while it's open. The popup is
        // dismissed by Esc or by any keystroke that changes the input buffer in a way that
        // would invalidate the items (handled below as "any non-popup key while open").
        if (_completions is { Count: > 0 })
        {
            switch (e.Key)
            {
                case ConsoleKey.UpArrow:
                    _completionIndex = (_completionIndex - 1 + _completions.Count) % _completions.Count;
                    InvalidationCallback?.Invoke();
                    return;
                case ConsoleKey.DownArrow:
                    _completionIndex = (_completionIndex + 1) % _completions.Count;
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
            case ConsoleKey.Enter:
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
                    _input.Remove(_cursorCol - 1, 1);
                    _cursorCol--;
                    RecomputeAnalysis();
                    InvalidationCallback?.Invoke();
                }
                return;
            case ConsoleKey.Delete:
                if (_cursorCol < _input.Length)
                {
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
            case ConsoleKey.Home:
                _cursorCol = 0; RecomputeAnalysis(); InvalidationCallback?.Invoke();
                return;
            case ConsoleKey.End:
                _cursorCol = _input.Length; RecomputeAnalysis(); InvalidationCallback?.Invoke();
                return;
            case ConsoleKey.UpArrow:
                NavigateHistory(-1);
                return;
            case ConsoleKey.DownArrow:
                NavigateHistory(+1);
                return;
            case ConsoleKey.PageUp:
                _scrollOffset = Math.Min(_scrollOffset + 5, Math.Max(0, _outputLines.Count - 1));
                InvalidationCallback?.Invoke();
                return;
            case ConsoleKey.PageDown:
                _scrollOffset = Math.Max(0, _scrollOffset - 5);
                InvalidationCallback?.Invoke();
                return;
        }

        // Printable text input: SDL3 TEXT_INPUT delivers shifted symbols here as KeyChar.
        if (e.KeyChar >= 0x20 && e.KeyChar < 0x7F && !e.Ctrl && !e.Alt)
        {
            _input.Insert(_cursorCol, e.KeyChar);
            _cursorCol++;
            RecomputeAnalysis();
            InvalidationCallback?.Invoke();
        }
    }

    private bool OpenCompletion()
    {
        var items = LanguageService.GetCompletions(_input.ToString(), new Position(0, _cursorCol));
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
        InvalidationCallback?.Invoke();
    }

    private static int FindWordStart(string text, int cursor)
    {
        var s = cursor;
        while (s > 0 && IsIdentifierChar(text[s - 1])) s--;
        return s;
    }

    private static bool IsIdentifierChar(char c)
        => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';

    private void RecomputeAnalysis()
    {
        var text = _input.ToString();
        _diagnostics = text.Length == 0
            ? Array.Empty<Diagnostic>()
            : LanguageService.GetDiagnostics(text);
        _hover = text.Length == 0
            ? null
            : LanguageService.GetHover(text, new Position(0, _cursorCol));
    }

    private void Submit()
    {
        var line = _input.ToString();
        _input.Clear();
        _cursorCol = 0;
        _historyIndex = -1;
        _diagnostics = Array.Empty<Diagnostic>();
        _hover = null;

        AppendOutput(Prompt + line);

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
}
