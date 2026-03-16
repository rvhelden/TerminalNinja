using System.Collections;
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
    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(ItemsControl),
            new FrameworkPropertyMetadata((object?)null, affectsRender: true,
                propertyChangedCallback: OnItemsSourceChanged));

    public static readonly DependencyProperty ItemTemplateProperty =
        DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(ItemsControl),
            new FrameworkPropertyMetadata((object?)null, affectsRender: true,
                propertyChangedCallback: (d, _) => ((ItemsControl)d).RefreshItems()));

    public static readonly DependencyProperty ItemsPanelProperty =
        DependencyProperty.Register(nameof(ItemsPanel), typeof(Panel), typeof(ItemsControl),
            new FrameworkPropertyMetadata((object?)null, affectsRender: true,
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

    private readonly List<object> _items = new();
    private readonly Dictionary<object, UIElement> _itemContainers = new();

    /// <summary>
    /// Gets the collection of items directly added to this control.
    /// Note: When ItemsSource is set, Items is read-only.
    /// </summary>
    public IList<object> Items => _items;

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
                SetValue(ItemsPanelProperty, panel);
            }
            return panel;
        }
        set => SetValue(ItemsPanelProperty, value);
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
                if (e.OldItems != null && e.NewItems != null)
                {
                    for (int i = 0; i < e.OldItems.Count; i++)
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
    private void RefreshItems()
    {
        ItemsPanel.Children.Clear();
        _itemContainers.Clear();

        var source = ItemsSource ?? Items;
        if (source == null)
            return;

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
    /// Generates a container control for the specified data item.
    /// </summary>
    private UIElement? GenerateContainer(object item)
    {
        UIElement? container;

        // If the item is already a UIElement, use it directly
        if (item is UIElement element)
        {
            container = element;
        }
        // Otherwise, use the ItemTemplate to create a container
        else if (ItemTemplate != null)
        {
            container = ItemTemplate.CreateContent();
            if (container is FrameworkElement fe)
            {
                // Set the DataContext so bindings work
                fe.DataContext = item;
            }
        }
        else
        {
            // No template - create a simple TextBlock with ToString()
            container = new TextBlock { Text = item?.ToString() ?? string.Empty };
        }

        return container;
    }

    /// <inheritdoc />
    public override Size2D GetPreferredSize(Rect parent)
    {
        // The ItemsControl defers to its ItemsPanel for size calculation
        return ItemsPanel.GetPreferredSize(parent);
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
