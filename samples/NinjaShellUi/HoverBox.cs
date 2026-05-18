using TerminalNinja.Buffers;
using TerminalNinja.Controls;
using TerminalNinja.Highlighting;
using TerminalNinja.Input;
using TerminalNinja.Primitives;

namespace NinjaShellUi;

/// <summary>
/// Bounded, syntax-highlighted hover content. The previous BuildHoverContent
/// used a plain TextBlock inside a Border, which let the panel grow to fit
/// whatever Hover.Contents happened to be — a long `obj.dump(value)` payload
/// could fill most of the screen. HoverBox caps the visible window at
/// <see cref="MaxWidth"/> x <see cref="MaxVisibleRows"/>, word-wraps overflowing
/// lines, and shows a "+N more" indicator on the last visible row when content
/// is truncated. Each visible row is highlighted via
/// <see cref="NinjaSyntaxHighlighter"/> using the active <see cref="SyntaxTheme"/>.
/// </summary>
/// <remarks>
/// <para>
/// Vertical scroll is supported via <see cref="ScrollOffset"/> + Page Up / Page
/// Down / Up / Down handled when this element has logical focus. The
/// owning hover panel exposes <see cref="HandleKey"/> so the REPL can forward
/// keystrokes while the hover is open without making the hover steal focus.
/// </para>
/// </remarks>
internal sealed class HoverBox : FrameworkElement
{
    private const int DefaultMaxWidth = 60;
    private const int DefaultMaxVisibleRows = 12;

    private static readonly Color BorderColor = new(0x89, 0xDC, 0xEB);
    private static readonly Color FallbackFg = new(0xCD, 0xD6, 0xF4);
    private static readonly Color HintFg = new(0x6C, 0x70, 0x86);
    private static readonly Color PanelBg = new(0x31, 0x32, 0x44);

    private string _text = string.Empty;
    private string[] _wrapped = Array.Empty<string>();
    private int _scrollOffset;

    /// <summary>The text payload. Wrapped on every set.</summary>
    public string Text
    {
        get => _text;
        set
        {
            _text = value ?? string.Empty;
            _wrapped = WrapToLines(_text, ContentWidth);
            _scrollOffset = 0;
        }
    }

    public int MaxWidth { get; set; } = DefaultMaxWidth;
    public int MaxVisibleRows { get; set; } = DefaultMaxVisibleRows;
    public SyntaxTheme Theme { get; set; } = SyntaxTheme.Dark;

    /// <summary>
    /// Language id to drive the syntax highlighter for visible lines. Resolved
    /// against <see cref="SyntaxHighlighterRegistry"/> on every render so swaps
    /// are live. Defaults to <c>"ninja"</c>; the REPL flips it to <c>"record"</c>
    /// when showing a value-shape hover so dump output reads cleanly.
    /// </summary>
    public string Language { get; set; } = "ninja";

    private int ContentWidth => Math.Max(1, MaxWidth - 4);  // borders + side padding

    /// <summary>
    /// Scroll-aware Page Up / Page Down / Up / Down handler. The hover panel
    /// itself never gets keyboard focus — the REPL forwards keys here while the
    /// hover is open. Returns true if the key was consumed.
    /// </summary>
    public bool HandleKey(KeyEvent e)
    {
        if (_wrapped.Length <= MaxVisibleRows) return false;
        int max = Math.Max(0, _wrapped.Length - MaxVisibleRows);
        switch (e.Key)
        {
            case ConsoleKey.PageDown:
                _scrollOffset = Math.Min(max, _scrollOffset + MaxVisibleRows);
                return true;
            case ConsoleKey.PageUp:
                _scrollOffset = Math.Max(0, _scrollOffset - MaxVisibleRows);
                return true;
            case ConsoleKey.DownArrow when e.Ctrl:
                _scrollOffset = Math.Min(max, _scrollOffset + 1);
                return true;
            case ConsoleKey.UpArrow when e.Ctrl:
                _scrollOffset = Math.Max(0, _scrollOffset - 1);
                return true;
        }
        return false;
    }

    /// <inheritdoc />
    public override Size2D GetPreferredSize(Rect parent)
    {
        if (_wrapped.Length == 0) return new Size2D(0, 0);
        int contentW = 0;
        foreach (var l in _wrapped) if (l.Length > contentW) contentW = l.Length;
        // +4 for borders + 1 cell padding each side.
        int w = Math.Min(MaxWidth, contentW + 4);
        // +2 for top + bottom borders. Reserve one row for the overflow
        // indicator when the content can't fit inside MaxVisibleRows.
        int contentRows = _wrapped.Length;
        bool indicatorRow = contentRows > MaxVisibleRows;
        int innerH = indicatorRow ? MaxVisibleRows : contentRows;
        int h = innerH + 2;
        return new Size2D(Math.Max(w, 4), Math.Max(h, 3));
    }

    /// <inheritdoc />
    public override Rect CalculateBounds(Rect parentBounds)
    {
        var size = GetPreferredSize(parentBounds);
        // HoverBox sits inside HoverPanelRoot, which has already placed +
        // clamped the parentBounds to the viewport. We just paint within those
        // bounds, capped to our preferred size so we never overflow.
        int w = Math.Min(size.Width, parentBounds.Width);
        int h = Math.Min(size.Height, parentBounds.Height);
        return new Rect(parentBounds.X, parentBounds.Y, w, h);
    }

    /// <inheritdoc />
    protected override void OnRender(CellBuffer buffer, Rect bounds)
    {
        if (bounds.Width < 4 || bounds.Height < 3) return;
        FillRect(buffer, bounds, PanelBg);
        DrawBorder(buffer, bounds, BorderColor, PanelBg);

        int innerW = bounds.Width - 4;
        int innerH = bounds.Height - 2;
        if (innerW <= 0 || innerH <= 0) return;

        // Re-wrap lazily if the available width changed since Text was set.
        if (_wrapped.Length > 0 && _wrapped[0].Length > innerW)
        {
            _wrapped = WrapToLines(_text, innerW);
            int max = Math.Max(0, _wrapped.Length - innerH);
            if (_scrollOffset > max) _scrollOffset = max;
        }

        int visibleCount = Math.Min(innerH, _wrapped.Length - _scrollOffset);
        // If we'd show a truncation indicator, reserve the last row for it.
        bool needsIndicator = _scrollOffset + visibleCount < _wrapped.Length || _scrollOffset > 0;
        int rowsForText = needsIndicator ? Math.Max(0, visibleCount - 1) : visibleCount;

        for (int r = 0; r < rowsForText; r++)
        {
            var line = _wrapped[_scrollOffset + r];
            DrawHighlightedLine(buffer, bounds.X + 2, bounds.Y + 1 + r, line, innerW);
        }

        if (needsIndicator && rowsForText < innerH)
        {
            int hidden = _wrapped.Length - (_scrollOffset + rowsForText);
            var hint = hidden > 0
                ? $"  ↓ +{hidden} more   (PgDn / Ctrl+↓)"
                : $"  ↑ at top         (PgUp / Ctrl+↑)";
            DrawText(buffer, bounds.X + 2, bounds.Y + 1 + rowsForText, hint, innerW, HintFg, PanelBg);
        }
    }

    private void DrawHighlightedLine(CellBuffer buffer, int x, int y, string line, int maxWidth)
    {
        if (line.Length == 0) return;
        // Tokenize each soft line independently — tokens never cross wrapped
        // line boundaries, so per-line analysis stays correct even though the
        // semantics may be lost when a word wraps mid-identifier.
        IReadOnlyList<SyntaxToken> tokens;
        try
        {
            tokens = SyntaxHighlighterRegistry.TryGet(Language, out var hl)
                ? hl.Tokenize(line)
                : Array.Empty<SyntaxToken>();
        }
        catch { tokens = Array.Empty<SyntaxToken>(); }

        int tokenIdx = 0;
        for (int i = 0; i < line.Length && i < maxWidth; i++)
        {
            var fg = FallbackFg;
            while (tokenIdx < tokens.Count && tokens[tokenIdx].Start + tokens[tokenIdx].Length <= i)
                tokenIdx++;
            if (tokenIdx < tokens.Count)
            {
                var t = tokens[tokenIdx];
                if (i >= t.Start && i < t.Start + t.Length) fg = Theme.GetColor(t.Kind);
            }
            int cx = x + i;
            if ((uint)cx >= (uint)buffer.Width) break;
            buffer.SetChar(cx, y, line[i], fg, PanelBg);
        }
    }

    /// <summary>
    /// Split <paramref name="text"/> on hard newlines, then word-wrap each soft
    /// line to <paramref name="width"/>. Tabs and CRs are stripped before wrap.
    /// </summary>
    private static string[] WrapToLines(string text, int width)
    {
        if (width <= 0 || string.IsNullOrEmpty(text)) return Array.Empty<string>();
        var result = new List<string>();
        var clean = text.Replace("\r", "").Replace("\t", "    ");
        foreach (var hard in clean.Split('\n'))
        {
            if (hard.Length == 0) { result.Add(string.Empty); continue; }
            int i = 0;
            while (i < hard.Length)
            {
                int len = Math.Min(width, hard.Length - i);
                if (i + len < hard.Length)
                {
                    int breakAt = hard.LastIndexOf(' ', i + len - 1, len);
                    if (breakAt > i) len = breakAt - i;
                }
                result.Add(hard.Substring(i, len));
                i += len;
                while (i < hard.Length && hard[i] == ' ') i++;
            }
        }
        return result.ToArray();
    }

    private static void FillRect(CellBuffer buffer, Rect rect, Color bg)
    {
        for (int r = 0; r < rect.Height; r++)
        {
            int y = rect.Y + r;
            if ((uint)y >= (uint)buffer.Height) continue;
            for (int c = 0; c < rect.Width; c++)
            {
                int x = rect.X + c;
                if ((uint)x >= (uint)buffer.Width) continue;
                buffer.SetCell(x, y, new Cell(' ', Color.White, bg));
            }
        }
    }

    private static void DrawBorder(CellBuffer buffer, Rect rect, Color fg, Color bg)
    {
        int x = rect.X, y = rect.Y, w = rect.Width, h = rect.Height;
        if (w < 2 || h < 2) return;
        buffer.SetChar(x, y, '╭', fg, bg);
        buffer.SetChar(x + w - 1, y, '╮', fg, bg);
        buffer.SetChar(x, y + h - 1, '╰', fg, bg);
        buffer.SetChar(x + w - 1, y + h - 1, '╯', fg, bg);
        for (int i = 1; i < w - 1; i++)
        {
            buffer.SetChar(x + i, y, '─', fg, bg);
            buffer.SetChar(x + i, y + h - 1, '─', fg, bg);
        }
        for (int i = 1; i < h - 1; i++)
        {
            buffer.SetChar(x, y + i, '│', fg, bg);
            buffer.SetChar(x + w - 1, y + i, '│', fg, bg);
        }
    }

    private static void DrawText(CellBuffer buffer, int x, int y, string text, int maxWidth, Color fg, Color bg)
    {
        if ((uint)y >= (uint)buffer.Height || maxWidth <= 0) return;
        for (int i = 0; i < text.Length && i < maxWidth; i++)
        {
            int cx = x + i;
            if ((uint)cx >= (uint)buffer.Width) break;
            buffer.SetChar(cx, y, text[i], fg, bg);
        }
    }
}
