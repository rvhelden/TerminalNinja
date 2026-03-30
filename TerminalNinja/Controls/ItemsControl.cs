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

        ItemsPanel.Children.Clear();
        _itemContainers.Clear();

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
        // Use the ItemTemplate if available
        if (ItemTemplate != null)
        {
            var container = ItemTemplate.CreateContent();
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
    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        // Render the items panel
        ItemsPanel.Render(buffer, bounds);
    }
}
