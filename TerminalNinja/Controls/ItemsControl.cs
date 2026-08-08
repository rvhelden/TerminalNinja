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
    /// This is the order <c>SelectedIndex</c> indexes, the order rows render in, and the order the
    /// container index space is defined against. <c>ItemsPanel.Children</c> matches it exactly
    /// while unvirtualized; under virtualization it holds only the realised window.
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
    /// The containers realised under virtualization, keyed by the item's index in
    /// <see cref="EffectiveItems"/>.
    /// </summary>
    /// <remarks>
    /// Containers are identified by <b>index</b>, never by the item. A collection is free to
    /// contain equal elements — two identical strings, two equal records, repeated spacer rows —
    /// and an item-keyed map cannot tell them apart, so it collapses all of them onto a single
    /// container. Virtualized, that one container was then added to the panel once per duplicate
    /// row: a single control living in several places at once, with one <c>IsSelected</c>, one
    /// parent and one set of layout bookkeeping shared between rows that are meant to be
    /// independent. Unvirtualized each row did get its own container, but the map kept only the
    /// last of them, so every lookup from an item — selection, <see cref="ContainerFromItem"/>,
    /// scroll-into-view — answered with the wrong row's control. The index is the only identity a
    /// data item is guaranteed to have.
    ///
    /// Only used while <see cref="IsVirtualizing"/>. Unvirtualized there is nothing to store:
    /// every item has a container and <c>ItemsPanel.Children[i]</c> <i>is</i> the container for
    /// item <c>i</c>, so the children collection is the map. Keeping a parallel dictionary in that
    /// mode would only be a second thing to get out of step, and would turn an append into an
    /// index-shifting walk of the whole list.
    /// </remarks>
    private readonly Dictionary<int, UIElement> _realizedContainers = new();

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

        // Drop containers for rows that have scrolled out of the window. Done first so a long
        // jump does not hold both windows at once. Eviction is by index, so two equal items in
        // and out of the window no longer evict each other.
        if (_realizedContainers.Count > 0)
        {
            List<int>? evicted = null;
            foreach (var index in _realizedContainers.Keys)
            {
                if (index < start || index >= start + count)
                {
                    (evicted ??= []).Add(index);
                }
            }

            if (evicted is not null)
            {
                foreach (var index in evicted)
                {
                    if (_realizedContainers.Remove(index, out var container))
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
            var index = start + i;
            var item = items[index];

            if (!_realizedContainers.TryGetValue(index, out var container))
            {
                container = GenerateContainer(item);
                if (container is null)
                {
                    continue;
                }

                _realizedContainers[index] = container;
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
        // containers that mostly do not exist. The realised ones are dropped rather than kept,
        // because they are keyed by index and every index at or after the change now names a
        // different item; reusing them would repaint the window one row out of step.
        if (IsVirtualizing)
        {
            DiscardRealizedContainers();
            InvalidateVisual();
            return;
        }

        // Unvirtualized, ItemsPanel.Children is the container map: child i belongs to item i.
        // Every branch below therefore patches Children at the index the notification carries,
        // never at "wherever this item's container happened to be" — with equal items in the
        // collection that lookup answers for the wrong row.
        var children = ItemsPanel.Children;

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems != null)
                {
                    var insertIndex = e.NewStartingIndex >= 0 ? e.NewStartingIndex : children.Count;
                    insertIndex = Math.Clamp(insertIndex, 0, children.Count);

                    foreach (var item in e.NewItems)
                    {
                        if (item is null)
                        {
                            // EffectiveItems drops nulls, so a container here would shift every
                            // later child off its item.
                            continue;
                        }

                        var container = GenerateContainer(item);
                        if (container != null)
                        {
                            children.Insert(insertIndex++, container);
                        }
                    }
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems != null && e.OldStartingIndex >= 0)
                {
                    for (var i = 0; i < e.OldItems.Count; i++)
                    {
                        if (e.OldStartingIndex < children.Count)
                        {
                            children.RemoveAt(e.OldStartingIndex);
                        }
                    }
                }
                else
                {
                    // A source that will not say where — the positions are unknowable, so rebuild.
                    RefreshItems();
                    return;
                }
                break;

            case NotifyCollectionChangedAction.Replace:
                if (e is { OldItems: not null, NewItems: not null } && e.NewStartingIndex >= 0)
                {
                    for (var i = 0; i < e.NewItems.Count; i++)
                    {
                        var index = e.NewStartingIndex + i;
                        if (index >= children.Count || e.NewItems[i] is not { } newItem)
                        {
                            continue;
                        }

                        var newContainer = GenerateContainer(newItem);
                        if (newContainer != null)
                        {
                            children.RemoveAt(index);
                            children.Insert(index, newContainer);
                        }
                    }
                }
                else
                {
                    RefreshItems();
                    return;
                }
                break;

            case NotifyCollectionChangedAction.Move:
                if (e is { OldStartingIndex: >= 0, NewStartingIndex: >= 0 }
                    && e.OldStartingIndex < children.Count)
                {
                    var moved = children[e.OldStartingIndex];
                    children.RemoveAt(e.OldStartingIndex);
                    children.Insert(Math.Clamp(e.NewStartingIndex, 0, children.Count), moved);
                }
                else
                {
                    RefreshItems();
                    return;
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                RefreshItems();
                return;
        }

        OnContainersChanged();
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
        _realizedContainers.Clear();
        RealizedStart = 0;

        // Virtualized, the containers are the render pass's business: it calls RealizeRange for
        // the window it is about to draw. Building them all here is exactly what virtualization
        // exists to avoid.
        if (IsVirtualizing)
        {
            InvalidateVisual();
            return;
        }

        // EffectiveItems, not the raw source: it is the list every index in this control is
        // measured against, so generating from it is what keeps child i and item i the same row.
        foreach (var item in EffectiveItems)
        {
            var container = GenerateContainer(item);
            if (container != null)
            {
                ItemsPanel.Children.Add(container);
            }
        }

        InvalidateVisual();
    }

    /// <summary>
    /// Drops every realised container. Virtualized only; unvirtualized the containers are the
    /// panel's children and are patched in place.
    /// </summary>
    private void DiscardRealizedContainers()
    {
        if (_realizedContainers.Count == 0)
        {
            return;
        }

        _realizedContainers.Clear();
        ItemsPanel.Children.Clear();
        RealizedStart = 0;
    }

    /// <summary>
    /// Called after the containers have been patched positionally by a collection change, so a
    /// subclass can re-apply anything it keys off the row index — selection, most obviously.
    /// </summary>
    /// <remarks>
    /// Needed because containers now stay where they are and the items move past them: after a
    /// removal, child <c>i</c> is the control that used to sit at <c>i + 1</c> and still carries
    /// that row's state.
    /// </remarks>
    protected virtual void OnContainersChanged()
    {
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
    /// Every container that currently exists, paired with the index of the item it shows.
    /// </summary>
    /// <remarks>
    /// The one way for a subclass to walk containers without inventing an identity for the items.
    /// Virtualized this is the realised window in no particular order; unvirtualized it is every
    /// row, in order.
    /// </remarks>
    protected IEnumerable<(int Index, UIElement Container)> RealizedContainers()
    {
        if (IsVirtualizing)
        {
            foreach (var (index, container) in _realizedContainers)
            {
                yield return (index, container);
            }

            yield break;
        }

        var children = ItemsPanel.Children;
        for (var i = 0; i < children.Count; i++)
        {
            yield return (i, children[i]);
        }
    }

    /// <summary>
    /// Gets the container element generated for the item at <paramref name="index"/> in
    /// <see cref="EffectiveItems"/>, or null when that row has no container.
    /// </summary>
    /// <param name="index">The item's index.</param>
    /// <returns>The container element, or null.</returns>
    /// <remarks>
    /// This is the lookup that always answers exactly: an index names one row even when several
    /// rows hold equal items. Virtualized, only realised rows have a container, so a row outside
    /// the window returns null.
    /// </remarks>
    public UIElement? ContainerFromIndex(int index)
    {
        if (index < 0)
        {
            return null;
        }

        if (IsVirtualizing)
        {
            return _realizedContainers.GetValueOrDefault(index);
        }

        var children = ItemsPanel.Children;
        return index < children.Count ? children[index] : null;
    }

    /// <summary>
    /// Gets the index of the item whose container is <paramref name="container"/>, or -1.
    /// </summary>
    /// <param name="container">The container element.</param>
    /// <returns>The item's index in <see cref="EffectiveItems"/>, or -1.</returns>
    /// <remarks>
    /// Matched by reference: a container is a specific control instance, and two containers built
    /// from equal items are still two different rows.
    /// </remarks>
    public int IndexFromContainer(UIElement container)
    {
        ArgumentNullException.ThrowIfNull(container);

        if (IsVirtualizing)
        {
            foreach (var (index, realised) in _realizedContainers)
            {
                if (ReferenceEquals(realised, container))
                {
                    return index;
                }
            }

            return -1;
        }

        var children = ItemsPanel.Children;
        for (var i = 0; i < children.Count; i++)
        {
            if (ReferenceEquals(children[i], container))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Gets the container element for the specified data item, or null if not found.
    /// </summary>
    /// <param name="item">The data item.</param>
    /// <returns>The container element, or null.</returns>
    /// <remarks>
    /// An item does not identify a row: a collection may hold equal items — two identical strings,
    /// two equal records, a repeated spacer — and each of them is its own row with its own
    /// container. This returns the container of the <b>first</b> item that compares equal
    /// (<see cref="EqualityComparer{T}.Default"/>, so <c>Equals</c>, not reference identity), which
    /// is only unambiguous when the items are distinct. Use <see cref="ContainerFromIndex"/> when
    /// the row matters.
    /// </remarks>
    public UIElement? ContainerFromItem(object item)
    {
        var index = EffectiveItems.IndexOf(item);
        return index < 0 ? null : ContainerFromIndex(index);
    }

    /// <summary>
    /// Gets the data item for the specified container element, or null if not found.
    /// </summary>
    /// <param name="container">The container element.</param>
    /// <returns>The data item, or null.</returns>
    public object? ItemFromContainer(UIElement container)
    {
        var index = IndexFromContainer(container);
        var items = EffectiveItems;
        return index >= 0 && index < items.Count ? items[index] : null;
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
