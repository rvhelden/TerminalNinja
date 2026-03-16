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
public class ItemsControl : FrameworkElement, IChildContainer
{
    private readonly List<object> _items = new();
    private IEnumerable? _itemsSource;
    private DataTemplate? _itemTemplate;
    private Panel? _itemsPanel;
    private readonly Dictionary<object, IControl> _itemContainers = new();

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
        get => _itemsSource;
        set
        {
            if (_itemsSource == value)
                return;

            // Unsubscribe from old collection
            if (_itemsSource is INotifyCollectionChanged oldObservable)
            {
                oldObservable.CollectionChanged -= OnCollectionChanged;
            }

            _itemsSource = value;

            // Subscribe to new collection
            if (_itemsSource is INotifyCollectionChanged newObservable)
            {
                newObservable.CollectionChanged += OnCollectionChanged;
            }

            OnPropertyChanged();
            RefreshItems();
        }
    }

    /// <summary>
    /// Gets or sets the DataTemplate used to display each item.
    /// </summary>
    public DataTemplate? ItemTemplate
    {
        get => _itemTemplate;
        set
        {
            if (_itemTemplate == value)
                return;

            _itemTemplate = value;
            OnPropertyChanged();
            RefreshItems();
        }
    }

    /// <summary>
    /// Gets or sets the panel used for laying out the items.
    /// Default is a vertical StackPanel.
    /// </summary>
    public Panel ItemsPanel
    {
        get => _itemsPanel ??= new StackPanel { Orientation = Orientation.Vertical };
        set
        {
            if (_itemsPanel == value)
                return;

            _itemsPanel = value;
            OnPropertyChanged();
            RefreshItems();
        }
    }

    /// <inheritdoc />
    public IEnumerable<(IControl Child, Rect ChildParentBounds)> GetChildrenWithBounds(Rect myBounds)
    {
        yield return (ItemsPanel, myBounds);
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
    private IControl? GenerateContainer(object item)
    {
        IControl? container;

        // If the item is already a control, use it directly
        if (item is IControl control)
        {
            container = control;
        }
        // Otherwise, use the ItemTemplate to create a container
        else if (ItemTemplate != null)
        {
            container = ItemTemplate.CreateContent();
            if (container != null)
            {
                // Set the DataContext so bindings work
                container.DataContext = item;
            }
        }
        else
        {
            // No template - create a simple label with ToString()
            container = new Label { Text = item?.ToString() ?? string.Empty };
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
