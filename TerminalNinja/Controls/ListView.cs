using System.Windows.Markup;
using TerminalNinja.Aot;
using TerminalNinja.Buffers;
using TerminalNinja.Controls.Primitives;
using TerminalNinja.Input;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// A multi-column list control with column headers and row selection.
/// Extends <see cref="Selector"/> for selection semantics.
/// Columns are defined via <see cref="Columns"/> and cell text is resolved
/// from <see cref="ListViewColumn.DisplayMemberBinding"/> or item ToString().
/// Corresponds to WPF's System.Windows.Controls.ListView.
/// </summary>
[ContentProperty("Items")]
[RuntimeNameProperty("Name")]
public sealed class ListView : Selector
{
    public ListView()
    {
        DefaultStyleKey = typeof(ListView);
        Columns = [];
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty SelectedBackgroundProperty =
        DependencyProperty.Register(nameof(SelectedBackground), typeof(Color), typeof(ListView),
            new FrameworkPropertyMetadata(Color.Blue, affectsRender: true));

    public static readonly DependencyProperty SelectedForegroundProperty =
        DependencyProperty.Register(nameof(SelectedForeground), typeof(Color), typeof(ListView),
            new FrameworkPropertyMetadata(Color.White, affectsRender: true));

    public static readonly DependencyProperty HeaderBackgroundProperty =
        DependencyProperty.Register(nameof(HeaderBackground), typeof(Color), typeof(ListView),
            new FrameworkPropertyMetadata(Color.DarkGray, affectsRender: true));

    public static readonly DependencyProperty HeaderForegroundProperty =
        DependencyProperty.Register(nameof(HeaderForeground), typeof(Color), typeof(ListView),
            new FrameworkPropertyMetadata(Color.White, affectsRender: true));

    public static readonly DependencyProperty ShowGridLinesProperty =
        DependencyProperty.Register(nameof(ShowGridLines), typeof(bool), typeof(ListView),
            new FrameworkPropertyMetadata(true, affectsRender: true));

    /// <summary>Gets or sets the background color for selected rows.</summary>
    public Color SelectedBackground
    {
        get => (Color)GetValue(SelectedBackgroundProperty)!;
        set => SetValue(SelectedBackgroundProperty, value);
    }

    /// <summary>Gets or sets the foreground color for selected rows.</summary>
    public Color SelectedForeground
    {
        get => (Color)GetValue(SelectedForegroundProperty)!;
        set => SetValue(SelectedForegroundProperty, value);
    }

    /// <summary>Gets or sets the background color for the header row.</summary>
    public Color HeaderBackground
    {
        get => (Color)GetValue(HeaderBackgroundProperty)!;
        set => SetValue(HeaderBackgroundProperty, value);
    }

    /// <summary>Gets or sets the foreground color for the header row.</summary>
    public Color HeaderForeground
    {
        get => (Color)GetValue(HeaderForegroundProperty)!;
        set => SetValue(HeaderForegroundProperty, value);
    }

    /// <summary>Gets or sets whether column separators are drawn.</summary>
    public bool ShowGridLines
    {
        get => (bool)GetValue(ShowGridLinesProperty)!;
        set => SetValue(ShowGridLinesProperty, value);
    }

    /// <summary>Gets the column definitions for this list view.</summary>
    public List<ListViewColumn> Columns { get; }

    // ─── Container Generation ────────────────────────────────────────

    /// <inheritdoc />
    protected override bool IsItemItsOwnContainer(object item) => item is ListViewItem;

    /// <inheritdoc />
    protected override UIElement CreateContainerForItem(object item)
    {
        var lvi = new ListViewItem
        {
            Background = Background,
            Foreground = Foreground,
            SelectedBackground = SelectedBackground,
            SelectedForeground = SelectedForeground
        };
        lvi.Content = item;
        return lvi;
    }

    /// <inheritdoc />
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

    // ─── Layout ──────────────────────────────────────────────────────

    /// <inheritdoc />
    public override Size2D GetPreferredSize(Rect parent) => new(parent.Width, parent.Height);

    /// <inheritdoc />
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
            if (Columns[i].Width > 0)
            {
                widths[i] = Columns[i].Width;
                fixedTotal += widths[i];
            }
            else
            {
                starCount++;
            }
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

    // ─── Cell Text Resolution ────────────────────────────────────────

    private string GetCellText(object? dataItem, ListViewColumn column)
    {
        if (dataItem == null) return "";

        if (!string.IsNullOrEmpty(column.DisplayMemberBinding))
        {
            var type = dataItem.GetType();
            if (PropertyAccessorRegistry.TryGetAccessor(type, column.DisplayMemberBinding, out var accessor))
            {
                return accessor.Value.Getter(dataItem)?.ToString() ?? "";
            }
        }

        return dataItem.ToString() ?? "";
    }

    // ─── Rendering ───────────────────────────────────────────────────

    private const int HeaderRows = 2; // header + separator

    /// <inheritdoc />
    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        var clipped = bounds.Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0) return;

        buffer.FillRect(clipped, new Cell(' ', Foreground, Background));

        // Auto-create a default column if none defined
        if (Columns.Count == 0)
        {
            Columns.Add(new ListViewColumn { Header = "Item", Width = 0 });
        }

        var colWidths = ResolveColumnWidths(bounds.Width);
        var gridLineColor = DimColor(Foreground);

        // ── Row 0: Headers ──
        RenderRow(buffer, bounds.X, bounds.Y, bounds.Width, colWidths,
            Columns.Select(c => c.Header).ToArray(),
            HeaderForeground, HeaderBackground, gridLineColor);

        // ── Row 1: Separator ──
        var sepY = bounds.Y + 1;
        if (sepY < bounds.Bottom)
        {
            var x = bounds.X;
            for (var col = 0; col < Columns.Count; col++)
            {
                for (var c = 0; c < colWidths[col] && x < bounds.Right; c++)
                {
                    SetCharSafe(buffer, x, sepY, '─', gridLineColor, Background);
                    x++;
                }

                if (ShowGridLines && col < Columns.Count - 1 && x < bounds.Right)
                {
                    SetCharSafe(buffer, x, sepY, '┼', gridLineColor, Background);
                    x++;
                }
            }
        }

        // ── Data rows ──
        var items = GetEffectiveItemsList();
        for (var row = 0; row < items.Count && bounds.Y + HeaderRows + row < bounds.Bottom; row++)
        {
            var item = items[row];
            var dataItem = GetDataItem(item);
            var isSelected = IsItemSelected(item);
            var fg = isSelected ? SelectedForeground : Foreground;
            var bg = isSelected ? SelectedBackground : Background;

            var cellTexts = new string[Columns.Count];
            for (var col = 0; col < Columns.Count; col++)
            {
                cellTexts[col] = GetCellText(dataItem, Columns[col]);
            }

            var rowY = bounds.Y + HeaderRows + row;
            if (isSelected)
            {
                var rowRect = new Rect(bounds.X, rowY, bounds.Width, 1).Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
                if (rowRect.Width > 0) buffer.FillRect(rowRect, new Cell(' ', fg, bg));
            }

            RenderRow(buffer, bounds.X, rowY, bounds.Width, colWidths, cellTexts, fg, bg, gridLineColor);
        }
    }

    private void RenderRow(CellBuffer buffer, int startX, int y, int totalWidth, int[] colWidths,
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
                SetCharSafe(buffer, x, y, '│', gridLineColor, bg);
                x++;
            }
        }
    }

    private List<object> GetEffectiveItemsList()
    {
        var result = new List<object>();
        foreach (var kvp in _itemContainers)
        {
            result.Add(kvp.Key);
        }
        return result;
    }

    private object? GetDataItem(object item)
    {
        if (_itemContainers.TryGetValue(item, out var container))
        {
            if (container is ListViewItem lvi)
                return lvi.Content ?? item;
        }
        return item;
    }

    private bool IsItemSelected(object item)
    {
        if (_itemContainers.TryGetValue(item, out var container))
        {
            if (container is ISelectableContainer sc)
                return sc.IsSelected;
        }
        return false;
    }

    // ─── Input ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public override void OnKeyEvent(KeyEvent e)
    {
        var count = ItemsPanel.Children.Count;
        if (count == 0) return;

        switch (e.Key)
        {
            case ConsoleKey.DownArrow:
                SelectedIndex = Math.Min(SelectedIndex + 1, count - 1);
                break;
            case ConsoleKey.UpArrow:
                SelectedIndex = Math.Max(SelectedIndex - 1, 0);
                break;
            case ConsoleKey.Home:
                SelectedIndex = 0;
                break;
            case ConsoleKey.End:
                SelectedIndex = count - 1;
                break;
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private static void SetCharSafe(CellBuffer buffer, int x, int y, char c, Color fg, Color bg)
    {
        if (x >= 0 && x < buffer.Width && y >= 0 && y < buffer.Height)
            buffer.SetChar(x, y, c, fg, bg);
    }

    private static Color DimColor(Color c) =>
        new((byte)(c.R / 2), (byte)(c.G / 2), (byte)(c.B / 2));
}
