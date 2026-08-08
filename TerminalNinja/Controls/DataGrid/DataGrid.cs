using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Controls.Primitives;
using TerminalNinja.Input;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// A read-only multi-column data grid with column headers, sorting indicators,
/// grid lines, and row selection. Extends <see cref="Selector"/> for selection semantics.
/// <para>
/// Columns are defined via <see cref="Columns"/> using typed column classes:
/// <see cref="DataGridTextColumn"/>, <see cref="DataGridCheckBoxColumn"/>,
/// and <see cref="DataGridTemplateColumn"/>.
/// Sort indicators (&#x25B2;/&#x25BC;) are shown in headers for sortable columns.
/// </para>
/// <para>
/// Keyboard: Up/Down move one row, PageUp/PageDown move one viewport of rows — whatever is
/// currently on screen, so a grid showing thirty rows pages by thirty — and Home/End jump to the
/// first and last row. The page keys turn the page under the selection rather than scrolling the
/// minimum needed to keep it visible, so the selected row holds its position on screen.
/// </para>
/// </summary>
[ContentProperty("Items")]
[RuntimeNameProperty("Name")]
public sealed class DataGrid : Selector
{
    public DataGrid()
    {
        DefaultStyleKey = typeof(DataGrid);
        Columns = [];
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty FocusColorProperty =
        DependencyProperty.Register(nameof(FocusColor), typeof(Color), typeof(DataGrid),
            new FrameworkPropertyMetadata(Color.Cyan, affectsRender: true));

    public Color FocusColor { get => (Color)GetValue(FocusColorProperty)!; set => SetValue(FocusColorProperty, value); }

    public static readonly DependencyProperty SelectedBackgroundProperty =
        DependencyProperty.Register(nameof(SelectedBackground), typeof(Color), typeof(DataGrid),
            new FrameworkPropertyMetadata(Color.Blue, affectsRender: true));

    public static readonly DependencyProperty SelectedForegroundProperty =
        DependencyProperty.Register(nameof(SelectedForeground), typeof(Color), typeof(DataGrid),
            new FrameworkPropertyMetadata(Color.White, affectsRender: true));

    public static readonly DependencyProperty HeaderBackgroundProperty =
        DependencyProperty.Register(nameof(HeaderBackground), typeof(Color), typeof(DataGrid),
            new FrameworkPropertyMetadata(Color.DarkGray, affectsRender: true));

    public static readonly DependencyProperty HeaderForegroundProperty =
        DependencyProperty.Register(nameof(HeaderForeground), typeof(Color), typeof(DataGrid),
            new FrameworkPropertyMetadata(Color.White, affectsRender: true));

    public static readonly DependencyProperty ShowGridLinesProperty =
        DependencyProperty.Register(nameof(ShowGridLines), typeof(bool), typeof(DataGrid),
            new FrameworkPropertyMetadata(true, affectsRender: true));

    public static readonly DependencyProperty CanSortProperty =
        DependencyProperty.Register(nameof(CanSort), typeof(bool), typeof(DataGrid),
            new PropertyMetadata(true));

    public Color SelectedBackground { get => (Color)GetValue(SelectedBackgroundProperty)!; set => SetValue(SelectedBackgroundProperty, value); }
    public Color SelectedForeground { get => (Color)GetValue(SelectedForegroundProperty)!; set => SetValue(SelectedForegroundProperty, value); }
    public Color HeaderBackground { get => (Color)GetValue(HeaderBackgroundProperty)!; set => SetValue(HeaderBackgroundProperty, value); }
    public Color HeaderForeground { get => (Color)GetValue(HeaderForegroundProperty)!; set => SetValue(HeaderForegroundProperty, value); }
    public bool ShowGridLines { get => (bool)GetValue(ShowGridLinesProperty)!; set => SetValue(ShowGridLinesProperty, value); }
    public bool CanSort { get => (bool)GetValue(CanSortProperty)!; set => SetValue(CanSortProperty, value); }

    /// <summary>Gets the column definitions.</summary>
    public List<DataGridColumn> Columns { get; }

    /// <summary>Raised when a column's sort direction changes.</summary>
    public event EventHandler? SortingChanged;

    // ─── Container Generation ────────────────────────────────────────

    protected override bool IsItemItsOwnContainer(object item) => item is ListViewItem;

    protected override UIElement CreateContainerForItem(object item)
    {
        return new ListViewItem
        {
            Background = Background,
            Foreground = Foreground,
            SelectedBackground = SelectedBackground,
            SelectedForeground = SelectedForeground,
            Content = item
        };
    }

    protected override void PrepareContainerForItem(UIElement container, object item)
    {
        base.PrepareContainerForItem(container, item);
        if (container is ListViewItem lvi)
        {
            lvi.SelectedBackground = SelectedBackground;
            lvi.SelectedForeground = SelectedForeground;
            lvi.IsSelected = SelectedItem == item;
        }
    }

    // ─── Sorting ─────────────────────────────────────────────────────

    /// <summary>
    /// Sorts by the specified column, cycling through None → Ascending → Descending → None.
    /// Clears sort on all other columns.
    /// </summary>
    public void SortByColumn(int columnIndex)
    {
        if (!CanSort || columnIndex < 0 || columnIndex >= Columns.Count) return;

        var col = Columns[columnIndex];
        if (!col.CanUserSort) return;

        // Cycle direction
        col.SortDirection = col.SortDirection switch
        {
            SortDirection.None => SortDirection.Ascending,
            SortDirection.Ascending => SortDirection.Descending,
            _ => SortDirection.None
        };

        // Clear other columns
        for (var i = 0; i < Columns.Count; i++)
        {
            if (i != columnIndex) Columns[i].SortDirection = SortDirection.None;
        }

        SortingChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    // ─── Layout ──────────────────────────────────────────────────────

    public override Size2D GetPreferredSize(Rect parent) => new(parent.Width, parent.Height);
    public override Rect CalculateBounds(Rect parent) => parent;

    // ─── Column Width Resolution ─────────────────────────────────────

    internal int[] ResolveColumnWidths(int totalWidth)
    {
        if (Columns.Count == 0) return [];

        var widths = new int[Columns.Count];
        var separatorCount = ShowGridLines ? Columns.Count - 1 : 0;
        var available = totalWidth - separatorCount;
        var fixedTotal = 0;
        var starCount = 0;

        for (var i = 0; i < Columns.Count; i++)
        {
            if (Columns[i].Width > 0) { widths[i] = Columns[i].Width; fixedTotal += widths[i]; }
            else starCount++;
        }

        var remaining = Math.Max(0, available - fixedTotal);
        var starWidth = starCount > 0 ? remaining / starCount : 0;
        var starRemainder = starCount > 0 ? remaining % starCount : 0;

        for (var i = 0; i < Columns.Count; i++)
        {
            if (Columns[i].Width <= 0)
            {
                widths[i] = starWidth + (starRemainder > 0 ? 1 : 0);
                if (starRemainder > 0) starRemainder--;
            }
        }
        return widths;
    }

    // ─── Scrolling ───────────────────────────────────────────────────

    private int _scrollOffset;

    /// <summary>
    /// Data rows visible at the last render, which is the distance PageUp/PageDown move by.
    /// </summary>
    /// <remarks>
    /// Only the render pass knows this: the grid is sized against the bounds its parent hands
    /// down, and key handling never sees them. Zero until the first render, where the page keys
    /// fall back to a single row — a grid that has not been drawn has no page to move by, and
    /// guessing one would scroll to somewhere the user cannot predict.
    /// </remarks>
    private int _viewportHeight;

    /// <summary>
    /// Adjusts the scroll offset so the selected row stays inside the viewport,
    /// matching the ListBox's internal scrolling behaviour.
    /// </summary>
    private void EnsureSelectedVisible(int viewportHeight)
    {
        if (SelectedIndex < 0 || viewportHeight <= 0) return;

        if (SelectedIndex < _scrollOffset)
            _scrollOffset = SelectedIndex;
        else if (SelectedIndex >= _scrollOffset + viewportHeight)
            _scrollOffset = SelectedIndex - viewportHeight + 1;
    }

    // ─── Rendering ───────────────────────────────────────────────────

    private const int HeaderRowCount = 2;

    protected override void OnRender(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        var clipped = bounds.Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0) return;

        buffer.FillRect(clipped, new Cell(' ', Foreground, Background));
        if (Columns.Count == 0) return;

        var colWidths = ResolveColumnWidths(bounds.Width);
        var gridLineColor = DimColor(Foreground);

        // Header row with sort indicators
        var headerTexts = new string[Columns.Count];
        for (var i = 0; i < Columns.Count; i++)
        {
            var indicator = Columns[i].SortDirection switch
            {
                SortDirection.Ascending => " \u25B2",
                SortDirection.Descending => " \u25BC",
                _ => ""
            };
            headerTexts[i] = Columns[i].Header + indicator;
        }

        RenderTextRow(buffer, bounds.X, bounds.Y, bounds.Width, colWidths, headerTexts,
            HeaderForeground, HeaderBackground, gridLineColor);

        // Separator
        var sepY = bounds.Y + 1;
        if (sepY < bounds.Bottom)
        {
            var x = bounds.X;
            for (var col = 0; col < Columns.Count; col++)
            {
                for (var c = 0; c < colWidths[col] && x < bounds.Right; c++)
                {
                    SetCharSafe(buffer, x, sepY, '\u2500', gridLineColor, Background);
                    x++;
                }
                if (ShowGridLines && col < Columns.Count - 1 && x < bounds.Right)
                {
                    SetCharSafe(buffer, x, sepY, '\u253C', gridLineColor, Background);
                    x++;
                }
            }
        }

        // Data rows — delegate cell rendering to each column.
        // Iterated in effective-items order (the order SelectedIndex indexes), not container
        // order: virtualized the realised containers are an unordered window over the list, and
        // they answer per index, so they cannot be the row sequence.
        var items = GetEffectiveItems();
        var viewportHeight = bounds.Height - HeaderRowCount;

        // Remembered for the page keys, which run outside the render pass.
        _viewportHeight = Math.Max(0, viewportHeight);

        // Keep the selected row inside the viewport, then clamp so the last page fills.
        EnsureSelectedVisible(viewportHeight);
        _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, items.Count - Math.Max(viewportHeight, 1)));

        for (var visibleRow = 0; visibleRow < viewportHeight && _scrollOffset + visibleRow < items.Count; visibleRow++)
        {
            var row = _scrollOffset + visibleRow;
            var item = items[row];

            // From the selection itself, not from a container's IsSelected flag. Virtualized
            // there are no containers to ask — the grid renders cells straight from the items —
            // and the row index is the source of truth that SelectedIndex already indexes.
            var isSelected = row == SelectedIndex;
            var fg = isSelected ? SelectedForeground : Foreground;
            var bg = isSelected ? SelectedBackground : Background;

            // Unwrapped from the item, for the same reason: a ListViewItem placed directly in the
            // collection is its own container, so the content is reachable without the dictionary.
            var dataItem = item is ListViewItem lvi ? lvi.Content ?? item : item;

            var rowY = bounds.Y + HeaderRowCount + visibleRow;
            if (isSelected)
            {
                var rowRect = new Rect(bounds.X, rowY, bounds.Width, 1).Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
                if (rowRect.Width > 0) buffer.FillRect(rowRect, new Cell(' ', fg, bg));
            }

            // Render each cell via column type
            var cellX = bounds.X;
            for (var col = 0; col < Columns.Count; col++)
            {
                Columns[col].RenderCell(buffer, cellX, rowY, colWidths[col], dataItem, fg, bg);
                cellX += colWidths[col];

                if (ShowGridLines && col < Columns.Count - 1 && cellX < bounds.Right)
                {
                    SetCharSafe(buffer, cellX, rowY, '\u2502', gridLineColor, bg);
                    cellX++;
                }
            }
        }

        // Focus border
        if (IsFocused && bounds is { Width: >= 2, Height: >= 2 })
        {
            for (var fx = bounds.X; fx < bounds.Right; fx++)
            {
                if (fx >= 0 && fx < buffer.Width)
                {
                    if (bounds.Y >= 0 && bounds.Y < buffer.Height) { var c = buffer.GetCell(fx, bounds.Y); buffer.SetCell(fx, bounds.Y, new Cell(c.Codepoint, FocusColor, c.Background)); }
                    if (bounds.Bottom - 1 >= 0 && bounds.Bottom - 1 < buffer.Height) { var c = buffer.GetCell(fx, bounds.Bottom - 1); buffer.SetCell(fx, bounds.Bottom - 1, new Cell(c.Codepoint, FocusColor, c.Background)); }
                }
            }
            for (var fy = bounds.Y; fy < bounds.Bottom; fy++)
            {
                if (fy >= 0 && fy < buffer.Height)
                {
                    if (bounds.X >= 0 && bounds.X < buffer.Width) { var c = buffer.GetCell(bounds.X, fy); buffer.SetCell(bounds.X, fy, new Cell(c.Codepoint, FocusColor, c.Background)); }
                    if (bounds.Right - 1 >= 0 && bounds.Right - 1 < buffer.Width) { var c = buffer.GetCell(bounds.Right - 1, fy); buffer.SetCell(bounds.Right - 1, fy, new Cell(c.Codepoint, FocusColor, c.Background)); }
                }
            }
        }
    }

    /// <summary>
    /// Renders a row of text cells (used for the header row).
    /// </summary>
    private void RenderTextRow(CellBuffer buffer, int startX, int y, int totalWidth, int[] colWidths,
        string[] cellTexts, Color fg, Color bg, Color gridLineColor)
    {
        if (y < 0 || y >= buffer.Height) return;
        var x = startX;
        for (var col = 0; col < Columns.Count && col < cellTexts.Length; col++)
        {
            var text = cellTexts[col];
            var w = colWidths[col];
            for (var c = 0; c < w && x < startX + totalWidth; c++)
            {
                var ch = c < text.Length ? text[c] : ' ';
                SetCharSafe(buffer, x, y, ch, fg, bg);
                x++;
            }
            if (ShowGridLines && col < Columns.Count - 1 && x < startX + totalWidth)
            {
                SetCharSafe(buffer, x, y, '\u2502', gridLineColor, bg);
                x++;
            }
        }
    }

    // ─── Input ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public override void OnMouseEvent(MouseEvent e)
    {
        switch (e.Action)
        {
            case MouseAction.ScrollUp:
                _scrollOffset = Math.Max(0, _scrollOffset - 3);
                InvalidateVisual();
                break;
            case MouseAction.ScrollDown:
                _scrollOffset = Math.Min(Math.Max(0, ItemsPanel.Children.Count - 1), _scrollOffset + 3);
                InvalidateVisual();
                break;
        }
    }

    public override bool OnKeyEvent(KeyEvent e)
    {
        // The effective items, not ItemsPanel.Children: SelectedIndex indexes the former, and the
        // two diverge whenever a container has not been realised for every item. End landing on
        // the wrong row is the visible symptom.
        var count = GetEffectiveItems().Count;
        if (count == 0) return false;

        // A page is what is on screen. Falls back to one row before the first render.
        var page = Math.Max(1, _viewportHeight);

        switch (e.Key)
        {
            case ConsoleKey.DownArrow:
                SetCurrentSelectedIndex(Math.Min(SelectedIndex + 1, count - 1));
                return true;
            case ConsoleKey.UpArrow:
                SetCurrentSelectedIndex(Math.Max(SelectedIndex - 1, 0));
                return true;
            case ConsoleKey.PageDown:
                MoveByPage(page, count);
                return true;
            case ConsoleKey.PageUp:
                MoveByPage(-page, count);
                return true;
            case ConsoleKey.Home:
                SetCurrentSelectedIndex(0);
                return true;
            case ConsoleKey.End:
                SetCurrentSelectedIndex(count - 1);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Moves the selection by <paramref name="delta"/> rows and turns the page under it.
    /// </summary>
    /// <remarks>
    /// The scroll offset is moved with the selection rather than left to
    /// <see cref="EnsureSelectedVisible"/>, which only ever scrolls the minimum needed to keep the
    /// selected row on screen. That is right for the arrow keys and wrong for these: paging down
    /// by ten would advance the selection ten rows but the window only one, leaving the cursor
    /// pinned to the bottom edge while the rows crawled past it.
    ///
    /// Keeping the selection at the same offset within the viewport is what makes a page turn
    /// read as a page turn: the row under the cursor stays where the eye left it. The render pass
    /// clamps the offset, so paging into the last page still fills the viewport.
    /// </remarks>
    private void MoveByPage(int delta, int count)
    {
        var offsetInViewport = SelectedIndex - _scrollOffset;

        SetCurrentSelectedIndex(Math.Clamp(SelectedIndex + delta, 0, count - 1));
        _scrollOffset = Math.Max(0, SelectedIndex - offsetInViewport);
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private static void SetCharSafe(CellBuffer buffer, int x, int y, char c, Color fg, Color bg)
    {
        if (x >= 0 && x < buffer.Width && y >= 0 && y < buffer.Height)
            buffer.SetChar(x, y, c, fg, bg);
    }

    private static Color DimColor(Color c) => new((byte)(c.R / 2), (byte)(c.G / 2), (byte)(c.B / 2));
}
