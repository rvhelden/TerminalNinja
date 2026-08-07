using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Markup;
using TerminalNinja.Aot;
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

    public static readonly DependencyProperty SelectedValueProperty =
        DependencyProperty.Register(nameof(SelectedValue), typeof(object), typeof(TreeView),
            new FrameworkPropertyMetadata((object?)null, affectsRender: true,
                propertyChangedCallback: static (d, e) => ((TreeView)d).OnSelectedValueChanged(e.NewValue)));

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(nameof(SelectedItem), typeof(TreeViewItem), typeof(TreeView),
            new FrameworkPropertyMetadata((object?)null, affectsRender: true,
                propertyChangedCallback: static (d, e) =>
                    ((TreeView)d).SetValueInternal(SelectedValueProperty, (e.NewValue as TreeViewItem)?.DataContext)));

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(TreeView),
            new FrameworkPropertyMetadata((object?)null, affectsRender: true,
                propertyChangedCallback: OnItemsSourceChanged));

    public static readonly DependencyProperty ItemTemplateProperty =
        DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(TreeView),
            new FrameworkPropertyMetadata((object?)null, affectsRender: true,
                propertyChangedCallback: static (d, _) => ((TreeView)d).RefreshItems()));

    public static readonly DependencyProperty ChildrenPathProperty =
        DependencyProperty.Register(nameof(ChildrenPath), typeof(string), typeof(TreeView),
            new FrameworkPropertyMetadata((object?)null, affectsRender: true,
                propertyChangedCallback: static (d, _) => ((TreeView)d).RefreshItems()));

    public static readonly DependencyProperty HeaderPathProperty =
        DependencyProperty.Register(nameof(HeaderPath), typeof(string), typeof(TreeView),
            new FrameworkPropertyMetadata((object?)null, affectsRender: true,
                propertyChangedCallback: static (d, _) => ((TreeView)d).RefreshItems()));

    public static readonly DependencyProperty IsExpandedPathProperty =
        DependencyProperty.Register(nameof(IsExpandedPath), typeof(string), typeof(TreeView),
            new FrameworkPropertyMetadata((object?)null, affectsRender: true,
                propertyChangedCallback: static (d, _) => ((TreeView)d).RefreshItems()));

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

    /// <summary>
    /// Gets or sets the data item behind the selected node (its DataContext).
    /// Setting it selects the node that was generated for that data item.
    /// Only meaningful when the tree is populated via <see cref="ItemsSource"/>.
    /// </summary>
    public object? SelectedValue
    {
        get => GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    /// <summary>
    /// Gets or sets the root-level data items. Each item becomes a <see cref="TreeViewItem"/>;
    /// child nodes are resolved through <see cref="ChildrenPath"/>, headers through
    /// <see cref="ItemTemplate"/> or <see cref="HeaderPath"/>. When the source implements
    /// <see cref="INotifyCollectionChanged"/>, root-level changes rebuild the tree; for changes
    /// deeper in the hierarchy, call <see cref="RefreshItems"/>. Expansion state and selection
    /// are preserved across rebuilds, keyed by data item.
    /// When set, <see cref="Items"/> is managed by the tree and must not be modified directly.
    /// </summary>
    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>
    /// Gets or sets the template used to build each node's header visual. The template content
    /// is instantiated per node with the node's data item as its DataContext, and is rendered
    /// as the node's header — so per-node colour and composition bind normally.
    /// When null, the header is the text resolved via <see cref="HeaderPath"/>.
    /// </summary>
    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets the property path (dot-separated) resolved against each data item to find
    /// its children collection. Null means the items are flat.
    /// </summary>
    public string? ChildrenPath
    {
        get => (string?)GetValue(ChildrenPathProperty);
        set => SetValue(ChildrenPathProperty, value);
    }

    /// <summary>
    /// Gets or sets the property path (dot-separated) resolved against each data item for its
    /// header text when no <see cref="ItemTemplate"/> is set. Null falls back to ToString().
    /// </summary>
    public string? HeaderPath
    {
        get => (string?)GetValue(HeaderPathProperty);
        set => SetValue(HeaderPathProperty, value);
    }

    /// <summary>
    /// Gets or sets the property path (dot-separated, resolving to a bool) that provides a
    /// node's INITIAL expansion state from its data item. Consulted only when a rebuild has no
    /// preserved state for that item — the user's expand/collapse actions always win.
    /// </summary>
    public string? IsExpandedPath
    {
        get => (string?)GetValue(IsExpandedPathProperty);
        set => SetValue(IsExpandedPathProperty, value);
    }

    /// <summary>Gets the collection of root-level tree items.</summary>
    public IList<TreeViewItem> Items => _items;

    // ─── ItemsSource Materialization ─────────────────────────────────

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var tree = (TreeView)d;

        if (e.OldValue is INotifyCollectionChanged oldObservable)
        {
            oldObservable.CollectionChanged -= tree.OnItemsSourceCollectionChanged;
        }

        if (e.NewValue is INotifyCollectionChanged newObservable)
        {
            newObservable.CollectionChanged += tree.OnItemsSourceCollectionChanged;
        }

        tree.RefreshItems();
    }

    private void OnItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        RefreshItems();

    /// <summary>
    /// Rebuilds the tree from <see cref="ItemsSource"/>, preserving expansion state and
    /// selection by data item. Call this after mutating a nested children collection —
    /// only root-level collection changes are observed automatically.
    /// </summary>
    public void RefreshItems()
    {
        if (ItemsSource is not { } source)
        {
            return;
        }

        // Capture expansion + selection state keyed by data item before tearing down.
        var expansion = new Dictionary<object, bool>(ReferenceEqualityComparer.Instance);
        CollectExpansionState(_items, expansion);
        var selectedData = SelectedItem?.DataContext ?? SelectedValue;

        _items.Clear();

        TreeViewItem? toSelect = null;
        foreach (var dataItem in source)
        {
            if (dataItem is null)
            {
                continue;
            }

            _items.Add(MaterializeNode(dataItem, expansion, selectedData, ref toSelect));
        }

        SetValueInternal(SelectedItemProperty, toSelect);
        SetValueInternal(SelectedValueProperty, toSelect?.DataContext);
        InvalidateVisual();
    }

    private static void CollectExpansionState(IEnumerable<TreeViewItem> items, Dictionary<object, bool> expansion)
    {
        foreach (var item in items)
        {
            if (item.DataContext is { } data)
            {
                expansion[data] = item.IsExpanded;
            }

            CollectExpansionState(item.Items, expansion);
        }
    }

    private TreeViewItem MaterializeNode(
        object dataItem,
        Dictionary<object, bool> expansion,
        object? selectedData,
        ref TreeViewItem? toSelect)
    {
        var node = new TreeViewItem
        {
            DataContext = dataItem,
            Header = CreateHeader(dataItem),
        };

        if (expansion.TryGetValue(dataItem, out var wasExpanded))
        {
            node.IsExpanded = wasExpanded;
        }
        else if (IsExpandedPath is { Length: > 0 } expandedPath
                 && ResolvePath(dataItem, expandedPath) is bool initiallyExpanded)
        {
            node.IsExpanded = initiallyExpanded;
        }

        if (selectedData is not null && ReferenceEquals(dataItem, selectedData))
        {
            toSelect = node;
        }

        if (ChildrenPath is { Length: > 0 } childrenPath
            && ResolvePath(dataItem, childrenPath) is IEnumerable children and not string)
        {
            foreach (var child in children)
            {
                if (child is null)
                {
                    continue;
                }

                node.Items.Add(MaterializeNode(child, expansion, selectedData, ref toSelect));
            }
        }

        return node;
    }

    private object? CreateHeader(object dataItem)
    {
        if (ItemTemplate?.CreateContent() is { } content)
        {
            if (content is FrameworkElement fe)
            {
                fe.DataContext = dataItem;
            }

            // A binding change inside the header must repaint the tree, not the orphan visual.
            content.InvalidationCallback = InvalidateVisual;
            return content;
        }

        if (HeaderPath is { Length: > 0 } headerPath)
        {
            return ResolvePath(dataItem, headerPath);
        }

        return dataItem;
    }

    /// <summary>
    /// Resolves a dot-separated property path via the AOT accessor registry (no reflection).
    /// Returns null when any segment is missing or null.
    /// </summary>
    private static object? ResolvePath(object source, string path)
    {
        object? current = source;
        foreach (var segment in path.Split('.'))
        {
            if (current is null
                || !PropertyAccessorRegistry.TryGetAccessor(current.GetType(), segment, out var accessor))
            {
                return null;
            }

            current = accessor.Value.Getter(current);
        }

        return current;
    }

    private bool _isSyncingSelectedValue;

    private void OnSelectedValueChanged(object? newValue)
    {
        if (_isSyncingSelectedValue || ReferenceEquals(SelectedItem?.DataContext, newValue))
        {
            return;
        }

        // Find the node generated for this data item and select it.
        _isSyncingSelectedValue = true;
        try
        {
            SetValueInternal(SelectedItemProperty, newValue is null ? null : FindNodeByData(_items, newValue));
        }
        finally
        {
            _isSyncingSelectedValue = false;
        }
    }

    private static TreeViewItem? FindNodeByData(IEnumerable<TreeViewItem> items, object data)
    {
        foreach (var item in items)
        {
            if (ReferenceEquals(item.DataContext, data))
            {
                return item;
            }

            if (FindNodeByData(item.Items, data) is { } found)
            {
                return found;
            }
        }

        return null;
    }

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
    protected override void OnRender(CellBuffer buffer, Rect parentBounds)
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
            var fg = isSelected ? SelectedForeground : (item.HeaderForeground ?? Foreground);
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

            // Draw the header at indent + 2: a UIElement header renders as a visual
            // (per-node colour and composition come from its own bindings), anything
            // else renders as text.
            var textX = bounds.X + indent + 2;
            if (item.Header is UIElement headerVisual)
            {
                var headerWidth = Math.Min(bounds.Right, buffer.Width) - textX;
                if (headerWidth > 0 && y >= 0 && y < buffer.Height)
                {
                    headerVisual.Render(buffer, new Rect(textX, y, headerWidth, 1));
                }
            }
            else
            {
                var text = item.HeaderText;
                for (var c = 0; c < text.Length && textX + c < bounds.Right; c++)
                {
                    SetCharSafe(buffer, textX + c, y, text[c], fg, bg);
                }
            }
        }

        // Focus border
        if (IsFocused && bounds is { Width: >= 2, Height: >= 2 })
        {
            for (var x = bounds.X; x < bounds.Right; x++)
            {
                if (x >= 0 && x < buffer.Width)
                {
                    if (bounds.Y >= 0 && bounds.Y < buffer.Height) { var c = buffer.GetCell(x, bounds.Y); buffer.SetCell(x, bounds.Y, new Cell(c.Codepoint, FocusColor, c.Background)); }
                    if (bounds.Bottom - 1 >= 0 && bounds.Bottom - 1 < buffer.Height) { var c = buffer.GetCell(x, bounds.Bottom - 1); buffer.SetCell(x, bounds.Bottom - 1, new Cell(c.Codepoint, FocusColor, c.Background)); }
                }
            }
            for (var y = bounds.Y; y < bounds.Bottom; y++)
            {
                if (y >= 0 && y < buffer.Height)
                {
                    if (bounds.X >= 0 && bounds.X < buffer.Width) { var c = buffer.GetCell(bounds.X, y); buffer.SetCell(bounds.X, y, new Cell(c.Codepoint, FocusColor, c.Background)); }
                    if (bounds.Right - 1 >= 0 && bounds.Right - 1 < buffer.Width) { var c = buffer.GetCell(bounds.Right - 1, y); buffer.SetCell(bounds.Right - 1, y, new Cell(c.Codepoint, FocusColor, c.Background)); }
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
    public override bool OnKeyEvent(KeyEvent e)
    {
        var nodes = FlattenVisibleNodes();
        if (nodes.Count == 0) return false;

        var currentIdx = SelectedItem != null
            ? nodes.FindIndex(n => n.Item == SelectedItem)
            : -1;

        var handled = true;

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

            default:
                handled = false;
                break;
        }

        InvalidateVisual();
        return handled;
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
