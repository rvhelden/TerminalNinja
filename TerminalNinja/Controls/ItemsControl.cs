using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Represents a control that displays a collection of items.
/// Supports data binding with ItemsSource and visual customization with ItemTemplate.
/// </summary>
[ContentProperty("Items")]
public class ItemsControl : Control
{
    public ItemsControl()
    {
        DefaultStyleKey = typeof(ItemsControl);
        _items.CollectionChanged += OnItemsCollectionChanged;
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(ItemsControl),
            new FrameworkPropertyMetadata(null, affectsRender: true,
                propertyChangedCallback: OnItemsSourceChanged));

    public static readonly DependencyProperty ItemTemplateProperty =
        DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(ItemsControl),
            new FrameworkPropertyMetadata(null, affectsRender: true,
                propertyChangedCallback: (d, _) => ((ItemsControl)d).RefreshItems()));

    public static readonly DependencyProperty IsVirtualizingProperty =
        DependencyProperty.Register(nameof(IsVirtualizing), typeof(bool), typeof(ItemsControl),
            new FrameworkPropertyMetadata(false, affectsRender: true,
                propertyChangedCallback: (d, _) => ((ItemsControl)d).RefreshItems()));

    public static readonly DependencyProperty ItemsPanelProperty =
        DependencyProperty.Register(nameof(ItemsPanel), typeof(Panel), typeof(ItemsControl),
            new FrameworkPropertyMetadata(null, affectsRender: true,
                propertyChangedCallback: (d, _) => ((ItemsControl)d).RefreshItems()));

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ItemsControl)d;

        // Unsubscribe from old collection
        if (e.OldValue is INotifyCollectionChanged oldObservable)
        {
            oldObservable.CollectionChanged -= control.OnCollectionChanged;
        }

        // Subscribe to new collection
        if (e.NewValue is INotifyCollectionChanged newObservable)
        {
            newObservable.CollectionChanged += control.OnCollectionChanged;
        }

        control.RefreshItems();
    }

    private readonly ObservableCollection<object> _items = new();

    /// <summary>
    /// The item list, materialised. Null when it must be rebuilt.
    /// </summary>
    /// <remarks>
    /// Rebuilding it per call meant a full allocation and walk of the collection on every frame —
    /// <see cref="DataGrid"/> asks for it once per render — which is most of what "no
    /// virtualization" actually cost on a long list.
    /// </remarks>
    private List<object>? _effectiveItems;

    /// <summary>
    /// Whether the current source announces its own changes, and so can be cached safely.
    /// </summary>
    /// <remarks>
    /// A plain <see cref="IEnumerable"/> can be mutated with nothing raised, and the old
    /// re-enumerate-every-time behaviour quietly tolerated that. Caching such a source would turn
    /// a working screen into a stale one, so it is left uncached; anything observable — including
    /// this control's own <see cref="Items"/> — is cached and invalidated on notification.
    /// </remarks>
    private bool SourceIsObservable => ItemsSource is null or INotifyCollectionChanged;

    /// <summary>Drops the materialised item list so the next read rebuilds it.</summary>
    protected void InvalidateItems() => _effectiveItems = null;

    /// <summary>
    /// The items to display, in order, materialised as a list.
    /// </summary>
    /// <remarks>
    /// This is the order <c>SelectedIndex</c> indexes and the order rows render in — not
    /// <see cref="_itemContainers"/>, whose dictionary order is not guaranteed, and not
    /// <c>ItemsPanel.Children</c>, which under virtualization holds only the realised window.
    /// </remarks>
    protected List<object> EffectiveItems
    {
        get
        {
            if (_effectiveItems is { } cached && SourceIsObservable)
            {
                return cached;
            }

            var list = new List<object>();
            var source = ItemsSource ?? Items;

            if (source != null)
            {
                foreach (var item in source)
                {
                    if (item != null)
                    {
                        list.Add(item);
                    }
                }
            }

            _effectiveItems = list;
            return list;
        }
    }

    
    /// <summary>
    /// Maps data items to their generated UI containers.
    /// Protected so subclasses (e.g., Selector) can look up containers for items.
    /// </summary>
    protected readonly Dictionary<object, UIElement> _itemContainers = new();

    /// <summary>
    /// Gets the collection of items directly added to this control.
    /// Adding or removing items automatically updates the visual panel.
    /// Note: When ItemsSource is set, Items should not be used directly.
    /// </summary>
    public IList<object> Items => _items;

    /// <summary>
    /// When true, suppresses RefreshItems calls (used during lazy ItemsPanel initialization).
    /// </summary>
    private bool _suppressRefresh;

    /// <summary>
    /// Handles changes to the direct Items collection (adds/removes/resets).
    /// Only processes changes when ItemsSource is not set, since ItemsSource
    /// takes priority over direct Items.
    /// </summary>
    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // If ItemsSource is set, it takes priority — ignore Items changes
        if (ItemsSource != null)
        {
            return;
        }

        OnCollectionChanged(sender, e);
    }

    /// <summary>
    /// Gets or sets the collection used to generate the content of the ItemsControl.
    /// When set, this overrides the Items collection.
    /// Supports INotifyCollectionChanged for live updates.
    /// </summary>
    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>
    /// Gets or sets the DataTemplate used to display each item.
    /// </summary>
    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <summary>
    /// Whether containers are created only for the items currently on screen.
    /// </summary>
    /// <remarks>
    /// Off by default, because a bare <see cref="ItemsControl"/> renders every child it has and
    /// has no notion of a viewport — virtualizing it would simply hide most of the list. It is on
    /// by default in <see cref="ListBox"/> and <see cref="DataGrid"/>, which scroll and already
    /// draw only the rows that fit.
    ///
    /// Unvirtualized, a container is built for every item up front: ten thousand rows meant ten
    /// thousand live controls to show the thirty that fit. Virtualized, the control calls
    /// <see cref="RealizeRange"/> for the window it is about to draw and
    /// <c>ItemsPanel.Children</c> holds only that window — so a consumer must index the children
    /// relative to the realised range, not by absolute item index.
    /// </remarks>
    public bool IsVirtualizing
    {
        get => (bool)GetValue(IsVirtualizingProperty)!;
        set => SetValue(IsVirtualizingProperty, value);
    }

    /// <summary>The index of the first realised item, or 0 when nothing is realised.</summary>
    protected int RealizedStart { get; private set; }

    /// <summary>
    /// Ensures the panel holds containers for exactly <paramref name="count"/> items starting at
    /// <paramref name="start"/>, and nothing else.
    /// </summary>
    /// <remarks>
    /// Containers already realised for items still in the window are reused rather than rebuilt,
    /// so scrolling by a row recreates one container, not a screenful — and a container that
    /// carries state a template put there survives the scroll.
    ///
    /// A no-op when <see cref="IsVirtualizing"/> is false, so a control can call it
    /// unconditionally before rendering.
    /// </remarks>
    protected void RealizeRange(int start, int count)
    {
        if (!IsVirtualizing)
        {
            return;
        }

        var items = EffectiveItems;
        start = Math.Clamp(start, 0, Math.Max(0, items.Count - 1));
        count = Math.Clamp(count, 0, Math.Max(0, items.Count - start));

        // Drop containers for items that have scrolled out of the window. Done first so a long
        // jump does not hold both windows at once.
        if (_itemContainers.Count > 0)
        {
            List<object>? evicted = null;
            foreach (var item in _itemContainers.Keys)
            {
                var index = items.IndexOf(item);
                if (index < start || index >= start + count)
                {
                    (evicted ??= []).Add(item);
                }
            }

            if (evicted is not null)
            {
                foreach (var item in evicted)
                {
                    if (_itemContainers.Remove(item, out var container))
                    {
                        ItemsPanel.Children.Remove(container);
                    }
                }
            }
        }

        // Realise the window in order. Children is rebuilt positionally rather than patched:
        // the window is one screenful, so the cost is trivial next to getting the order wrong.
        ItemsPanel.Children.Clear();

        for (var i = 0; i < count; i++)
        {
            var item = items[start + i];

            if (!_itemContainers.TryGetValue(item, out var container))
            {
                container = GenerateContainer(item);
                if (container is null)
                {
                    continue;
                }

                _itemContainers[item] = container;
            }
            else
            {
                // Reused: its selection state may have moved on since it was built.
                PrepareContainerForItem(container, item);
            }

            ItemsPanel.Children.Add(container);
        }

        RealizedStart = start;
    }

    /// <summary>
    /// Gets or sets the panel used for laying out the items.
    /// Default is a vertical StackPanel.
    /// </summary>
    public Panel ItemsPanel
    {
        get
        {
            var panel = (Panel?)GetValue(ItemsPanelProperty);
            if (panel == null)
            {
                panel = new StackPanel { Orientation = Orientation.Vertical };
                panel.Parent = this;
                // Suppress RefreshItems during lazy init — the panel is empty and
                // OnCollectionChanged already processes Items additions in real time.
                _suppressRefresh = true;
                try
                {
                    SetValue(ItemsPanelProperty, panel);
                }
                finally
                {
                    _suppressRefresh = false;
                }
            }
            return panel;
        }
        set
        {
            // Detach old panel
            var old = (Panel?)GetValue(ItemsPanelProperty);
            if (old != null)
            {
                old.Parent = null;
            }

            // Attach new panel
            if (value != null)
            {
                value.Parent = this;
            }

            SetValue(ItemsPanelProperty, value);
        }
    }

    /// <inheritdoc />
    public override IEnumerable<(Visual Child, Rect ChildParentBounds)> GetChildrenWithBounds(Rect myBounds)
    {
        yield return (ItemsPanel, myBounds);
    }

    /// <inheritdoc />
    protected internal override IEnumerable<FrameworkElement> GetLogicalChildren()
    {
        yield return ItemsPanel;
    }

    /// <summary>
    /// Handles collection change notifications from ItemsSource.
    /// </summary>
    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateItems();

        // Virtualized, there is no per-item container to insert, move or drop — the next render
        // realises whatever window is current. Incremental patching here would be work spent on
        // containers that mostly do not exist.
        if (IsVirtualizing)
        {
            InvalidateVisual();
            return;
        }

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems != null)
                {
                    var insertIndex = e.NewStartingIndex >= 0 ? e.NewStartingIndex : ItemsPanel.Children.Count;
                    foreach (var item in e.NewItems)
                    {
                        var container = GenerateContainer(item);
                        if (container != null)
                        {
                            _itemContainers[item] = container;
                            if (insertIndex >= 0 && insertIndex <= ItemsPanel.Children.Count)
                            {
                                ItemsPanel.Children.Insert(insertIndex++, container);
                            }
                            else
                            {
                                ItemsPanel.Children.Add(container);
                            }
                        }
                    }
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems != null)
                {
                    foreach (var item in e.OldItems)
                    {
                        if (_itemContainers.TryGetValue(item, out var container))
                        {
                            ItemsPanel.Children.Remove(container);
                            _itemContainers.Remove(item);
                        }
                    }
                }
                break;

            case NotifyCollectionChangedAction.Replace:
                if (e is { OldItems: not null, NewItems: not null })
                {
                    for (var i = 0; i < e.OldItems.Count; i++)
                    {
                        var oldItem = e.OldItems[i]!;
                        var newItem = e.NewItems[i]!;

                        if (_itemContainers.TryGetValue(oldItem, out var oldContainer))
                        {
                            var index = ItemsPanel.Children.IndexOf(oldContainer);
                            ItemsPanel.Children.Remove(oldContainer);
                            _itemContainers.Remove(oldItem);

                            var newContainer = GenerateContainer(newItem);
                            if (newContainer != null)
                            {
                                _itemContainers[newItem] = newContainer;
                                if (index >= 0)
                                {
                                    ItemsPanel.Children.Insert(index, newContainer);
                                }
                                else
                                {
                                    ItemsPanel.Children.Add(newContainer);
                                }
                            }
                        }
                    }
                }
                break;

            case NotifyCollectionChangedAction.Move:
                if (e.OldItems != null)
                {
                    foreach (var item in e.OldItems)
                    {
                        if (_itemContainers.TryGetValue(item, out var container))
                        {
                            ItemsPanel.Children.Remove(container);
                            if (e.NewStartingIndex >= 0 && e.NewStartingIndex <= ItemsPanel.Children.Count)
                            {
                                ItemsPanel.Children.Insert(e.NewStartingIndex, container);
                            }
                            else
                            {
                                ItemsPanel.Children.Add(container);
                            }
                        }
                    }
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                RefreshItems();
                break;
        }

        InvalidateVisual();
    }

    /// <summary>
    /// Regenerates all item containers from the current ItemsSource or Items collection.
    /// </summary>
    protected virtual void RefreshItems()
    {
        if (_suppressRefresh)
        {
            return;
        }

        InvalidateItems();

        ItemsPanel.Children.Clear();
        _itemContainers.Clear();
        RealizedStart = 0;

        // Virtualized, the containers are the render pass's business: it calls RealizeRange for
        // the window it is about to draw. Building them all here is exactly what virtualization
        // exists to avoid.
        if (IsVirtualizing)
        {
            InvalidateVisual();
            return;
        }

        var source = ItemsSource ?? Items;
        if (source == null)
        {
            return;
        }

        foreach (var item in source)
        {
            var container = GenerateContainer(item);
            if (container != null)
            {
                _itemContainers[item] = container;
                ItemsPanel.Children.Add(container);
            }
        }

        InvalidateVisual();
    }

    /// <summary>
    /// Determines whether the specified item is (or can be) its own container.
    /// Override in subclasses to allow UIElements to be used directly without wrapping.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <returns><c>true</c> if the item is its own container; otherwise, <c>false</c>.</returns>
    protected virtual bool IsItemItsOwnContainer(object item) => item is UIElement;

    /// <summary>
    /// Creates a new container element to host the specified data item.
    /// Override in subclasses (e.g., ListBox) to provide custom containers like ListBoxItem.
    /// </summary>
    /// <param name="item">The data item that needs a container.</param>
    /// <returns>A new container element.</returns>
    protected virtual UIElement CreateContainerForItem(object item)
    {
        // Use the explicit ItemTemplate, or an implicit one matched on the item's type
        if (SelectItemTemplate(item) is { } template)
        {
            var container = template.CreateContent();
            if (container is FrameworkElement fe)
            {
                fe.DataContext = item;
            }
            return container ?? new TextBlock { Text = item?.ToString() ?? string.Empty };
        }

        // No template — create a simple TextBlock with ToString()
        return new TextBlock { Text = item?.ToString() ?? string.Empty };
    }

    /// <summary>
    /// Chooses the template for a data item: the explicitly assigned <see cref="ItemTemplate"/>,
    /// or failing that a keyless <see cref="DataTemplate"/> from the resource chain whose
    /// <c>DataType</c> matches the item.
    /// </summary>
    /// <param name="item">The data item that needs a container.</param>
    /// <returns>The template to use, or <c>null</c> to fall back to ToString().</returns>
    /// <remarks>
    /// An explicit <see cref="ItemTemplate"/> always wins: it was named for this control, whereas
    /// an implicit template is a dictionary-wide default.
    /// </remarks>
    protected DataTemplate? SelectItemTemplate(object? item)
    {
        if (ItemTemplate is { } explicitTemplate)
        {
            return explicitTemplate;
        }

        var implicitTemplate = TryFindImplicitDataTemplate(item);
        if (implicitTemplate == null)
        {
            // Containers were built without a template. Record the tree shape we looked in, so a
            // later graft onto a Window that does carry one can rebuild them — see
            // FrameworkElement.ResourceScopeVersion.
            _templatelessResolveVersion = ResourceScopeVersion;
        }

        return implicitTemplate;
    }

    /// <summary>
    /// The <see cref="FrameworkElement.ResourceScopeVersion"/> at the last container generation
    /// that found no template, or -1 when every container had one.
    /// </summary>
    private int _templatelessResolveVersion = -1;

    /// <inheritdoc />
    protected override void OnVisualParentChanged(Visual? oldParent)
    {
        base.OnVisualParentChanged(oldParent);

        // Items are usually bound before the control reaches the tree, so the first container
        // generation searches a resource chain that stops at this control. Regenerate once the
        // chain is longer, but only if something actually went untemplated.
        if (ItemTemplate == null && _templatelessResolveVersion >= 0
            && _templatelessResolveVersion != ResourceScopeVersion)
        {
            _templatelessResolveVersion = -1;
            RefreshItems();
        }
    }

    /// <summary>
    /// Prepares the specified container to display the given item.
    /// Override in subclasses to set additional properties on the container.
    /// </summary>
    /// <param name="container">The container element.</param>
    /// <param name="item">The data item.</param>
    protected virtual void PrepareContainerForItem(UIElement container, object item)
    {
        // Base implementation sets DataContext on framework elements
        if (container is FrameworkElement fe && !IsItemItsOwnContainer(item))
        {
            fe.DataContext = item;
        }
    }

    /// <summary>
    /// Generates a container control for the specified data item.
    /// Uses the template method pattern: IsItemItsOwnContainer → CreateContainerForItem → PrepareContainerForItem.
    /// </summary>
    protected UIElement? GenerateContainer(object item)
    {
        UIElement? container;

        // If the item is already a suitable container, use it directly
        if (IsItemItsOwnContainer(item))
        {
            container = (UIElement)item;
        }
        else
        {
            // Create a container via the overridable factory method
            container = CreateContainerForItem(item);
        }

        // Let subclasses prepare the container (e.g., set selection state)
        if (container != null)
        {
            PrepareContainerForItem(container, item);
        }

        return container;
    }

    /// <inheritdoc />
    public override Size2D GetPreferredSize(Rect parent)
    {
        // The ItemsControl defers to its ItemsPanel for size calculation
        return ItemsPanel.GetPreferredSize(parent);
    }

    /// <summary>
    /// Gets the container element for the specified data item, or null if not found.
    /// </summary>
    /// <param name="item">The data item.</param>
    /// <returns>The container element, or null.</returns>
    public UIElement? ContainerFromItem(object item)
    {
        return _itemContainers.TryGetValue(item, out var container) ? container : null;
    }

    /// <summary>
    /// Gets the data item for the specified container element, or null if not found.
    /// </summary>
    /// <param name="container">The container element.</param>
    /// <returns>The data item, or null.</returns>
    public object? ItemFromContainer(UIElement container)
    {
        foreach (var kvp in _itemContainers)
        {
            if (kvp.Value == container)
            {
                return kvp.Key;
            }
        }
        return null;
    }

    /// <inheritdoc />
    public override Rect CalculateBounds(Rect parent)
    {
        // The ItemsControl fills its parent bounds
        return parent;
    }

    /// <inheritdoc />
    protected override void OnRender(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        // Render the items panel
        ItemsPanel.Render(buffer, bounds);
    }
}
