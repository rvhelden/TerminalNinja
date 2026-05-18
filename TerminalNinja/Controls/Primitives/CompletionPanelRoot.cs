using TerminalNinja.Buffers;
using TerminalNinja.Highlighting;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls.Primitives;

/// <summary>
/// Internal root element for a <see cref="CompletionPanel"/>. Renders a two-pane
/// IntelliSense overlay: an icon+label list on the left and a details pane on
/// the right (signature + documentation for the selected item). Positions the
/// composite via the same placement math as <see cref="HoverPanelRoot"/>.
/// </summary>
/// <remarks>
/// Pane widths are caps, not fixed: <see cref="CalculateBounds"/> shrinks the
/// details pane to fit when the viewport is narrow, and drops it entirely when
/// there isn't room for both panes. The maximum panel footprint is roughly
/// 22 + 1 + 32 = 55 cells — chosen so the popup never dominates a narrow REPL
/// pane (the previous 28+1+42 = 71 layout could span half a 1280px window).
/// </remarks>
internal sealed class CompletionPanelRoot : FrameworkElement
{
    /// <summary>Width cap for the list pane in cells (label + glyph + padding).</summary>
    private const int ListPaneWidth = 22;

    /// <summary>Width cap for the details pane in cells.</summary>
    private const int DetailsPaneWidth = 32;

    /// <summary>Maximum visible rows before scrolling kicks in.</summary>
    private const int MaxVisibleRows = 8;

    /// <summary>Highlighter used to colorize the Detail signature line.</summary>
    private static readonly NinjaSyntaxHighlighter Highlighter = new();

    /// <summary>Theme used for syntax-highlighted Detail/Documentation. Public so callers can swap.</summary>
    public SyntaxTheme Theme { get; set; } = SyntaxTheme.Dark;

    /// <summary>Background colour for the panel — Catppuccin surface0.</summary>
    private static readonly Color PanelBg = new(0x31, 0x32, 0x44);

    /// <summary>Background colour for the selected row — Catppuccin surface1.</summary>
    private static readonly Color SelectedBg = new(0x45, 0x47, 0x5A);

    /// <summary>Foreground for the main labels — Catppuccin text.</summary>
    private static readonly Color LabelFg = new(0xCD, 0xD6, 0xF4);

    /// <summary>Foreground for dim text (details, separator) — Catppuccin overlay0.</summary>
    private static readonly Color DimFg = new(0x6C, 0x70, 0x86);

    /// <summary>Foreground for the signature line in the details pane — Catppuccin subtext1.</summary>
    private static readonly Color SignatureFg = new(0xBA, 0xC2, 0xDE);

    /// <summary>Anchor coordinates in viewport space.</summary>
    public int AnchorX { get; set; }
    public int AnchorY { get; set; }

    /// <summary>Placement relative to anchor.</summary>
    public PlacementMode Placement { get; set; } = PlacementMode.Bottom;

    /// <summary>Offsets applied after placement.</summary>
    public int HorizontalOffset { get; set; }
    public int VerticalOffset { get; set; }

    /// <summary>The completion entries to show in the list pane.</summary>
    public IReadOnlyList<CompletionEntry> Items { get; set; } = Array.Empty<CompletionEntry>();

    /// <summary>The currently focused row in <see cref="Items"/>. Renders inverted.</summary>
    public int SelectedIndex { get; set; }

    /// <inheritdoc />
    public override Size2D GetPreferredSize(Rect parent)
    {
        int width = ComputeWidth();
        int height = ComputeHeight();
        if (height <= 0) return new Size2D(0, 0);
        return new Size2D(width, height);
    }

    /// <inheritdoc />
    public override Rect CalculateBounds(Rect viewport)
    {
        if (Items.Count == 0) return new Rect(0, 0, 0, 0);

        int w = ComputeWidth();
        int h = ComputeHeight();

        var targetRect = new Rect(AnchorX, AnchorY, 1, 1);
        int x, y;
        switch (Placement)
        {
            case PlacementMode.Top:
                x = targetRect.X;
                y = targetRect.Y - h;
                break;
            case PlacementMode.Right:
                x = targetRect.Right;
                y = targetRect.Y;
                break;
            case PlacementMode.Left:
                x = targetRect.X - w;
                y = targetRect.Y;
                break;
            case PlacementMode.Center:
                x = targetRect.X - w / 2;
                y = targetRect.Y - h / 2;
                break;
            case PlacementMode.Absolute:
                x = HorizontalOffset;
                y = VerticalOffset;
                break;
            default:
                x = targetRect.X;
                y = targetRect.Bottom;
                break;
        }

        if (Placement != PlacementMode.Absolute)
        {
            x += HorizontalOffset;
            y += VerticalOffset;
        }

        // Flip above the anchor when Bottom placement would overflow — IntelliSense
        // popups always prefer "above the cursor" when they don't fit below.
        if (y + h > viewport.Bottom && Placement == PlacementMode.Bottom)
            y = targetRect.Y - h + VerticalOffset;

        if (x + w > viewport.Right) x = viewport.Right - w;
        if (y + h > viewport.Bottom) y = viewport.Bottom - h;
        if (x < viewport.X) x = viewport.X;
        if (y < viewport.Y) y = viewport.Y;

        return new Rect(x, y, w, h);
    }

    /// <summary>
    /// Panel height = the larger of (list rows) and (details rows). With a
    /// single completion entry the list wants 1 row, but the details pane may
    /// need 3–8 rows to surface the signature + documentation — sizing by the
    /// max keeps both panes visible. Capped at MaxVisibleRows; overflow shows
    /// a "↓ +N more" indicator on the last details row.
    /// </summary>
    private int ComputeHeight()
    {
        if (Items.Count == 0) return 0;
        int listRows = Math.Min(Items.Count, MaxVisibleRows);
        int detailsRows = ComputeDetailsRowCount();
        return Math.Min(MaxVisibleRows, Math.Max(listRows, detailsRows));
    }

    /// <summary>Rows the selected entry's Detail + Documentation need when wrapped.</summary>
    private int ComputeDetailsRowCount()
    {
        if (SelectedIndex < 0 || SelectedIndex >= Items.Count) return 0;
        var entry = Items[SelectedIndex];
        if (string.IsNullOrEmpty(entry.Detail) && string.IsNullOrEmpty(entry.Documentation)) return 0;
        int innerWidth = Math.Max(1, DetailsPaneWidth - 2);
        int rows = 0;
        if (!string.IsNullOrEmpty(entry.Detail))
        {
            foreach (var _ in WrapLines(entry.Detail!, innerWidth)) rows++;
            if (!string.IsNullOrEmpty(entry.Documentation)) rows++; // spacer
        }
        if (!string.IsNullOrEmpty(entry.Documentation))
        {
            foreach (var hardLine in entry.Documentation!.Split('\n'))
                foreach (var _ in WrapLines(hardLine, innerWidth)) rows++;
        }
        return rows;
    }

    /// <summary>Total panel width = list pane + 1-cell separator + details pane.</summary>
    private int ComputeWidth()
    {
        // Skip the details pane when the focused entry has no extra content.
        bool hasDetails = SelectedIndex >= 0 && SelectedIndex < Items.Count
            && (!string.IsNullOrEmpty(Items[SelectedIndex].Detail)
                || !string.IsNullOrEmpty(Items[SelectedIndex].Documentation));
        return hasDetails ? ListPaneWidth + 1 + DetailsPaneWidth : ListPaneWidth;
    }

    /// <inheritdoc />
    protected override void OnRender(CellBuffer buffer, Rect parentBounds)
    {
        if (Items.Count == 0) return;
        var bounds = CalculateBounds(parentBounds);
        FillRect(buffer, bounds, PanelBg);

        int listWidth = Math.Min(ListPaneWidth, bounds.Width);
        var listRect = new Rect(bounds.X, bounds.Y, listWidth, bounds.Height);
        RenderList(buffer, listRect);

        if (bounds.Width <= ListPaneWidth + 1) return;

        // Separator column between the two panes.
        for (int row = 0; row < bounds.Height; row++)
        {
            int sx = bounds.X + ListPaneWidth;
            int sy = bounds.Y + row;
            if ((uint)sy >= (uint)buffer.Height || (uint)sx >= (uint)buffer.Width) continue;
            buffer.SetChar(sx, sy, '│', DimFg, PanelBg);
        }

        var detailsRect = new Rect(
            bounds.X + ListPaneWidth + 1, bounds.Y,
            bounds.Width - ListPaneWidth - 1, bounds.Height);
        RenderDetails(buffer, detailsRect);
    }

    /// <summary>
    /// Render the icon+label list pane. Selected row's background flips so the
    /// glyph and label still read on top. The visible window scrolls to keep
    /// <see cref="SelectedIndex"/> in view.
    /// </summary>
    private void RenderList(CellBuffer buffer, Rect rect)
    {
        int visible = Math.Min(rect.Height, Items.Count);
        int firstVisible = Math.Max(0, Math.Min(Items.Count - visible, SelectedIndex - visible / 2));
        for (int r = 0; r < visible; r++)
        {
            int itemIdx = firstVisible + r;
            if (itemIdx >= Items.Count) break;
            var entry = Items[itemIdx];
            int y = rect.Y + r;
            var rowBg = itemIdx == SelectedIndex ? SelectedBg : PanelBg;
            FillRow(buffer, rect.X, y, rect.Width, rowBg);

            // Glyph cell.
            DrawText(buffer, rect.X + 1, y, entry.Glyph, 1, entry.GlyphColor, rowBg);
            // Label, clipped to the remaining width.
            int labelX = rect.X + 3;
            int labelWidth = Math.Max(0, rect.Width - 4);
            DrawText(buffer, labelX, y, entry.Label, labelWidth, LabelFg, rowBg);
        }
    }

    /// <summary>
    /// Render the details pane for <see cref="SelectedIndex"/>: signature line
    /// (Detail) at the top syntax-highlighted in ninja colors, then a blank
    /// line, then the Documentation body wrapped to the pane width. If
    /// Documentation overflows the visible rows, the last row shows a "↓ +N"
    /// indicator so the user knows there's more (full body is reachable via
    /// hover or by accepting the completion and inspecting via obj.dump).
    /// </summary>
    private void RenderDetails(CellBuffer buffer, Rect rect)
    {
        if (SelectedIndex < 0 || SelectedIndex >= Items.Count) return;
        var entry = Items[SelectedIndex];
        if (string.IsNullOrEmpty(entry.Detail) && string.IsNullOrEmpty(entry.Documentation)) return;

        // Collect all lines (detail + spacer + wrapped doc) upfront so we can
        // count total vs visible and render an overflow indicator on the last row.
        var lines = new List<(string Text, bool Highlight)>();
        if (!string.IsNullOrEmpty(entry.Detail))
        {
            foreach (var line in WrapLines(entry.Detail!, rect.Width - 2))
                lines.Add((line, true));
            if (!string.IsNullOrEmpty(entry.Documentation))
                lines.Add((string.Empty, false));
        }
        if (!string.IsNullOrEmpty(entry.Documentation))
        {
            foreach (var hardLine in entry.Documentation!.Split('\n'))
                foreach (var soft in WrapLines(hardLine, rect.Width - 2))
                    lines.Add((soft, false));
        }

        int max = rect.Height;
        bool overflow = lines.Count > max;
        int rowsForText = overflow ? max - 1 : lines.Count;
        for (int r = 0; r < rowsForText; r++)
        {
            var (text, highlight) = lines[r];
            if (highlight) DrawHighlighted(buffer, rect.X + 1, rect.Y + r, text, rect.Width - 2);
            else DrawText(buffer, rect.X + 1, rect.Y + r, text, rect.Width - 2, LabelFg, PanelBg);
        }
        if (overflow)
        {
            int hidden = lines.Count - rowsForText;
            DrawText(buffer, rect.X + 1, rect.Y + rowsForText,
                     $"↓ +{hidden} more", rect.Width - 2, DimFg, PanelBg);
        }
    }

    /// <summary>
    /// Draw <paramref name="text"/> with NinjaSyntaxHighlighter token colors.
    /// Used for the Detail signature line so callables, keywords, literals etc.
    /// get the same colors users see while typing.
    /// </summary>
    private void DrawHighlighted(CellBuffer buffer, int x, int y, string text, int maxWidth)
    {
        if (text.Length == 0 || maxWidth <= 0 || (uint)y >= (uint)buffer.Height) return;
        IReadOnlyList<SyntaxToken> tokens;
        try { tokens = Highlighter.Tokenize(text); }
        catch { tokens = Array.Empty<SyntaxToken>(); }
        int tokenIdx = 0;
        for (int i = 0; i < text.Length && i < maxWidth; i++)
        {
            var fg = SignatureFg;
            while (tokenIdx < tokens.Count && tokens[tokenIdx].Start + tokens[tokenIdx].Length <= i)
                tokenIdx++;
            if (tokenIdx < tokens.Count)
            {
                var t = tokens[tokenIdx];
                if (i >= t.Start && i < t.Start + t.Length) fg = Theme.GetColor(t.Kind);
            }
            int cx = x + i;
            if ((uint)cx >= (uint)buffer.Width) break;
            buffer.SetChar(cx, y, text[i], fg, PanelBg);
        }
    }

    /// <summary>Wrap <paramref name="text"/> at word boundaries to fit <paramref name="width"/>.</summary>
    private static IEnumerable<string> WrapLines(string text, int width)
    {
        if (width <= 0 || text.Length == 0) { yield return string.Empty; yield break; }
        int i = 0;
        while (i < text.Length)
        {
            int len = Math.Min(width, text.Length - i);
            // Try to break on the last space within the window so words stay intact.
            if (i + len < text.Length)
            {
                int breakAt = text.LastIndexOf(' ', i + len - 1, len);
                if (breakAt > i) len = breakAt - i;
            }
            yield return text.Substring(i, len);
            i += len;
            while (i < text.Length && text[i] == ' ') i++;
        }
    }

    private static void FillRect(CellBuffer buffer, Rect rect, Color bg)
    {
        for (int r = 0; r < rect.Height; r++) FillRow(buffer, rect.X, rect.Y + r, rect.Width, bg);
    }

    private static void FillRow(CellBuffer buffer, int x, int y, int width, Color bg)
    {
        if ((uint)y >= (uint)buffer.Height) return;
        for (int i = 0; i < width; i++)
        {
            int cx = x + i;
            if ((uint)cx >= (uint)buffer.Width) break;
            buffer.SetCell(cx, y, new Cell(' ', Color.White, bg));
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
