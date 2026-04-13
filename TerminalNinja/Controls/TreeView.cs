using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Input;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// A hierarchical data display control with expand/collapse support.
/// Each node is a <see cref="TreeViewItem"/> with a <see cref="TreeViewItem.Header"/>
/// and optional child <see cref="TreeViewItem.Items"/>.
/// Corresponds to WPF's System.Windows.Controls.TreeView.
/// </summary>
[ContentProperty("Items")]
[RuntimeNameProperty("Name")]
public sealed class TreeView : Control
{
    private readonly ObservableCollection<TreeViewItem> _items;
    private int _scrollOffset;

    public TreeView()
    {
        DefaultStyleKey = typeof(TreeView);
        _items = [];
        _items.CollectionChanged += (_, _) => InvalidateVisual();
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty FocusColorProperty =
        DependencyProperty.Register(nameof(FocusColor), typeof(Color), typeof(TreeView),
            new FrameworkPropertyMetadata(Color.Cyan, affectsRender: true));

    public Color FocusColor
    {
        get => (Color)GetValue(FocusColorProperty)!;
        set => SetValue(FocusColorProperty, value);
    }

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(nameof(SelectedItem), typeof(TreeViewItem), typeof(TreeView),
            new FrameworkPropertyMetadata((object?)null, affectsRender: true));

    public static readonly DependencyProperty SelectedBackgroundProperty =
        DependencyProperty.Register(nameof(SelectedBackground), typeof(Color), typeof(TreeView),
            new FrameworkPropertyMetadata(Color.Blue, affectsRender: true));

    public static readonly DependencyProperty SelectedForegroundProperty =
        DependencyProperty.Register(nameof(SelectedForeground), typeof(Color), typeof(TreeView),
            new FrameworkPropertyMetadata(Color.White, affectsRender: true));

    /// <summary>Gets or sets the currently selected (highlighted) node.</summary>
    public TreeViewItem? SelectedItem
    {
        get => (TreeViewItem?)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>Gets or sets the background color for the selected node.</summary>
    public Color SelectedBackground
    {
        get => (Color)GetValue(SelectedBackgroundProperty)!;
        set => SetValue(SelectedBackgroundProperty, value);
    }

    /// <summary>Gets or sets the foreground color for the selected node.</summary>
    public Color SelectedForeground
    {
        get => (Color)GetValue(SelectedForegroundProperty)!;
        set => SetValue(SelectedForegroundProperty, value);
    }

    /// <summary>Gets the collection of root-level tree items.</summary>
    public IList<TreeViewItem> Items => _items;

    // ─── Layout ──────────────────────────────────────────────────────

    /// <inheritdoc />
    public override Size2D GetPreferredSize(Rect parent) => new(parent.Width, parent.Height);

    /// <inheritdoc />
    public override Rect CalculateBounds(Rect parent) => parent;

    // ─── Flattened Visible Node List ─────────────────────────────────

    private List<(TreeViewItem Item, int Depth)> FlattenVisibleNodes()
    {
        var result = new List<(TreeViewItem, int)>();
        foreach (var item in _items)
        {
            FlattenNode(item, 0, result);
        }
        return result;
    }

    private static void FlattenNode(TreeViewItem item, int depth, List<(TreeViewItem, int)> result)
    {
        result.Add((item, depth));
        if (item.IsExpanded)
        {
            foreach (var child in item.Items)
            {
                FlattenNode(child, depth + 1, result);
            }
        }
    }

    private TreeViewItem? FindParentOf(TreeViewItem target)
    {
        foreach (var root in _items)
        {
            var found = FindParentIn(root, target);
            if (found != null) return found;
        }
        return null;
    }

    private static TreeViewItem? FindParentIn(TreeViewItem current, TreeViewItem target)
    {
        foreach (var child in current.Items)
        {
            if (child == target) return current;
            var found = FindParentIn(child, target);
            if (found != null) return found;
        }
        return null;
    }

    // ─── Rendering ───────────────────────────────────────────────────

    /// <inheritdoc />
    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        var clipped = bounds.Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0) return;

        buffer.FillRect(clipped, new Cell(' ', Foreground, Background));

        var nodes = FlattenVisibleNodes();
        if (nodes.Count == 0) return;

        // Ensure scroll offset keeps selected item visible
        EnsureSelectedVisible(nodes, bounds.Height);

        for (var row = 0; row < bounds.Height && _scrollOffset + row < nodes.Count; row++)
        {
            var (item, depth) = nodes[_scrollOffset + row];
            var y = bounds.Y + row;
            if (y < 0 || y >= buffer.Height) continue;

            var isSelected = item == SelectedItem;
            var fg = isSelected ? SelectedForeground : Foreground;
            var bg = isSelected ? SelectedBackground : Background;

            // Fill row background if selected
            if (isSelected)
            {
                var rowRect = new Rect(bounds.X, y, bounds.Width, 1).Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
                if (rowRect.Width > 0)
                    buffer.FillRect(rowRect, new Cell(' ', fg, bg));
            }

            var indent = depth * 2;
            var x = bounds.X + indent;

            // Draw expand/collapse indicator
            if (item.HasItems)
            {
                var indicator = item.IsExpanded ? '\u25BC' : '\u25B6'; // ▼ or ▶
                SetCharSafe(buffer, x, y, indicator, fg, bg);
            }

            // Draw header text at indent + 2
            var textX = bounds.X + indent + 2;
            var text = item.HeaderText;
            for (var c = 0; c < text.Length && textX + c < bounds.Right; c++)
            {
                SetCharSafe(buffer, textX + c, y, text[c], fg, bg);
            }
        }

        // Focus border
        if (IsFocused && bounds is { Width: >= 2, Height: >= 2 })
        {
            for (var x = bounds.X; x < bounds.Right; x++)
            {
                if (x >= 0 && x < buffer.Width)
                {
                    if (bounds.Y >= 0 && bounds.Y < buffer.Height) { var c = buffer.GetCell(x, bounds.Y); buffer.SetCell(x, bounds.Y, new Cell(c.Character, FocusColor, c.Background)); }
                    if (bounds.Bottom - 1 >= 0 && bounds.Bottom - 1 < buffer.Height) { var c = buffer.GetCell(x, bounds.Bottom - 1); buffer.SetCell(x, bounds.Bottom - 1, new Cell(c.Character, FocusColor, c.Background)); }
                }
            }
            for (var y = bounds.Y; y < bounds.Bottom; y++)
            {
                if (y >= 0 && y < buffer.Height)
                {
                    if (bounds.X >= 0 && bounds.X < buffer.Width) { var c = buffer.GetCell(bounds.X, y); buffer.SetCell(bounds.X, y, new Cell(c.Character, FocusColor, c.Background)); }
                    if (bounds.Right - 1 >= 0 && bounds.Right - 1 < buffer.Width) { var c = buffer.GetCell(bounds.Right - 1, y); buffer.SetCell(bounds.Right - 1, y, new Cell(c.Character, FocusColor, c.Background)); }
                }
            }
        }
    }

    private void EnsureSelectedVisible(List<(TreeViewItem Item, int Depth)> nodes, int viewportHeight)
    {
        if (SelectedItem == null) return;
        var selectedIdx = -1;
        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].Item == SelectedItem) { selectedIdx = i; break; }
        }
        if (selectedIdx < 0) return;

        if (selectedIdx < _scrollOffset)
            _scrollOffset = selectedIdx;
        else if (selectedIdx >= _scrollOffset + viewportHeight)
            _scrollOffset = selectedIdx - viewportHeight + 1;
    }

    // ─── Input ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public override void OnKeyEvent(KeyEvent e)
    {
        var nodes = FlattenVisibleNodes();
        if (nodes.Count == 0) return;

        var currentIdx = SelectedItem != null
            ? nodes.FindIndex(n => n.Item == SelectedItem)
            : -1;

        switch (e.Key)
        {
            case ConsoleKey.DownArrow:
                if (currentIdx < nodes.Count - 1)
                    SelectedItem = nodes[currentIdx + 1].Item;
                else if (currentIdx < 0 && nodes.Count > 0)
                    SelectedItem = nodes[0].Item;
                break;

            case ConsoleKey.UpArrow:
                if (currentIdx > 0)
                    SelectedItem = nodes[currentIdx - 1].Item;
                break;

            case ConsoleKey.RightArrow:
                if (SelectedItem != null)
                {
                    if (!SelectedItem.IsExpanded && SelectedItem.HasItems)
                        SelectedItem.IsExpanded = true;
                    else if (SelectedItem.IsExpanded && SelectedItem.Items.Count > 0)
                        SelectedItem = SelectedItem.Items[0];
                }
                break;

            case ConsoleKey.LeftArrow:
                if (SelectedItem != null)
                {
                    if (SelectedItem.IsExpanded)
                        SelectedItem.IsExpanded = false;
                    else
                    {
                        var parent = FindParentOf(SelectedItem);
                        if (parent != null) SelectedItem = parent;
                    }
                }
                break;

            case ConsoleKey.Enter or ConsoleKey.Spacebar:
                if (SelectedItem != null && SelectedItem.HasItems)
                    SelectedItem.IsExpanded = !SelectedItem.IsExpanded;
                break;

            case ConsoleKey.Home:
                if (nodes.Count > 0)
                    SelectedItem = nodes[0].Item;
                break;

            case ConsoleKey.End:
                if (nodes.Count > 0)
                    SelectedItem = nodes[^1].Item;
                break;
        }

        InvalidateVisual();
    }

    /// <inheritdoc />
    public override void OnMouseEvent(MouseEvent e)
    {
        if (e is not { Action: MouseAction.Press, Button: MouseButton.Left }) return;

        var bounds = CalculateBounds(new Rect(0, 0, 1000, 1000)); // approximate
        var nodes = FlattenVisibleNodes();
        var clickRow = e.Y - bounds.Y + _scrollOffset;

        if (clickRow >= 0 && clickRow < nodes.Count)
        {
            var (item, depth) = nodes[clickRow];
            var indicatorX = bounds.X + depth * 2;

            // Click on indicator toggles expand
            if (e.X == indicatorX && item.HasItems)
                item.IsExpanded = !item.IsExpanded;

            SelectedItem = item;
            InvalidateVisual();
        }
    }

    // ─── Logical Children ────────────────────────────────────────────

    /// <inheritdoc />
    protected internal override IEnumerable<FrameworkElement> GetLogicalChildren()
    {
        foreach (var item in _items)
        {
            yield return item;
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private static void SetCharSafe(CellBuffer buffer, int x, int y, char c, Color fg, Color bg)
    {
        if (x >= 0 && x < buffer.Width && y >= 0 && y < buffer.Height)
            buffer.SetChar(x, y, c, fg, bg);
    }
}
