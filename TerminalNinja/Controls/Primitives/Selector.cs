using TerminalNinja.Primitives;

namespace TerminalNinja.Controls.Primitives;

/// <summary>
/// Abstract base class for controls that maintain a selected item.
/// Inherits from <see cref="ItemsControl"/> and adds selection semantics
/// (SelectedIndex, SelectedItem, SelectionChanged event).
/// Corresponds to WPF's System.Windows.Controls.Primitives.Selector.
/// </summary>
public abstract class Selector : ItemsControl
{
    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty SelectedIndexProperty =
        DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(Selector),
            new FrameworkPropertyMetadata(-1, affectsRender: true,
                propertyChangedCallback: OnSelectedIndexChanged)
            {
                // The user drives selection with the keyboard/mouse, so a plain
                // {Binding SelectedIndex} must flow that change back to the view model.
                BindsTwoWayByDefault = true,
            });

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(Selector),
            new FrameworkPropertyMetadata(null, affectsRender: true,
                propertyChangedCallback: OnSelectedItemChanged)
            {
                // As above: selection is user state, so {Binding SelectedItem} defaults to
                // two-way, matching WPF's Selector. Without this the view model's value is
                // pushed back onto the control on the next refresh, snapping the selection home.
                BindsTwoWayByDefault = true,
            });

    public static readonly DependencyProperty SelectionModeProperty =
        DependencyProperty.Register(nameof(SelectionMode), typeof(SelectionMode), typeof(Selector),
            new PropertyMetadata(SelectionMode.Single));

    // ─── Coercion guard ──────────────────────────────────────────────

    private bool _updatingSelection;

    // ─── Property changed callbacks ──────────────────────────────────

    private static void OnSelectedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var selector = (Selector)d;
        if (selector._updatingSelection)
        {
            return;
        }

        var newIndex = (int)e.NewValue!;
        selector._updatingSelection = true;
        try
        {
            var items = selector.GetEffectiveItems();
            if (newIndex < 0 || newIndex >= items.Count)
            {
                selector.SetValueInternal(SelectedItemProperty, null);
                selector.UpdateContainerSelection(null);
            }
            else
            {
                var item = items[newIndex];
                selector.SetValueInternal(SelectedItemProperty, item);
                selector.UpdateContainerSelection(item);
            }

            selector.OnSelectionChanged(
                e.OldValue is int oldIdx and >= 0 && oldIdx < items.Count
                    ? [items[oldIdx]]
                    : [],
                newIndex >= 0 && newIndex < items.Count
                    ? [items[newIndex]]
                    : []);
        }
        finally
        {
            selector._updatingSelection = false;
        }
    }

    private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var selector = (Selector)d;
        if (selector._updatingSelection)
        {
            return;
        }

        selector._updatingSelection = true;
        try
        {
            var items = selector.GetEffectiveItems();
            var newItem = e.NewValue;
            var newIndex = newItem != null ? items.IndexOf(newItem) : -1;
            selector.SetValueInternal(SelectedIndexProperty, newIndex);
            selector.UpdateContainerSelection(newItem);

            var removed = e.OldValue != null ? [e.OldValue] : Array.Empty<object>();
            var added = newItem != null ? [newItem] : Array.Empty<object>();
            selector.OnSelectionChanged(removed, added);
        }
        finally
        {
            selector._updatingSelection = false;
        }
    }

    // ─── CLR wrappers ────────────────────────────────────────────────

    /// <summary>
    /// Gets or sets the index of the currently selected item, or -1 if nothing is selected.
    /// </summary>
    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty)!;
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>
    /// Gets or sets the currently selected data item, or null if nothing is selected.
    /// </summary>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>
    /// Gets or sets the selection mode. Currently only <see cref="Primitives.SelectionMode.Single"/> is supported.
    /// </summary>
    public SelectionMode SelectionMode
    {
        get => (SelectionMode)GetValue(SelectionModeProperty)!;
        set => SetValue(SelectionModeProperty, value);
    }

    // ─── Event ───────────────────────────────────────────────────────

    /// <summary>
    /// Raised when the selection changes.
    /// </summary>
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;

    // ─── Selection logic ─────────────────────────────────────────────

    /// <summary>
    /// Called internally (e.g., by <see cref="ListBoxItem"/>) to select a specific data item.
    /// </summary>
    internal void NotifyItemClicked(object item)
    {
        var items = GetEffectiveItems();
        var index = items.IndexOf(item);
        if (index >= 0)
        {
            SetCurrentSelectedIndex(index);
        }
    }

    /// <summary>
    /// Moves the selection the way the control's own input must: the new index is published and a
    /// two-way binding writes it back, but the binding itself survives.
    /// </summary>
    /// <remarks>
    /// Every key and mouse path in a <see cref="Selector"/> subclass has to go through this rather
    /// than the public <see cref="SelectedIndex"/> setter. That setter is
    /// <see cref="DependencyObject.SetValue"/>, which detaches the expression — "a local value
    /// overrides a binding", as in WPF — so the very first arrow key would delete the binding it
    /// was meant to drive, and the control and its view model would disagree from then on with
    /// nothing reported anywhere.
    /// </remarks>
    protected void SetCurrentSelectedIndex(int index) =>
        SetCurrentValue(SelectedIndexProperty, index);

    /// <summary>
    /// Called internally to select a specific container element.
    /// </summary>
    internal void NotifyContainerClicked(UIElement container)
    {
        var item = ItemFromContainer(container);
        if (item != null)
        {
            NotifyItemClicked(item);
        }
    }

    /// <summary>
    /// Raises the <see cref="SelectionChanged"/> event.
    /// </summary>
    protected virtual void OnSelectionChanged(IList<object> removed, IList<object> added)
    {
        SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(removed, added));
    }

    /// <summary>
    /// Updates the IsSelected state on all item containers to reflect the current selection.
    /// </summary>
    private void UpdateContainerSelection(object? selectedItem)
    {
        foreach (var kvp in _itemContainers)
        {
            if (kvp.Value is ISelectableContainer container)
            {
                container.IsSelected = kvp.Key == selectedItem && selectedItem != null;
            }
        }
    }

    /// <inheritdoc />
    protected override void RefreshItems()
    {
        base.RefreshItems();

        // Re-apply selection after items have been regenerated
        var selectedItem = SelectedItem;
        if (selectedItem != null)
        {
            var items = GetEffectiveItems();
            var idx = items.IndexOf(selectedItem);
            if (idx >= 0)
            {
                _updatingSelection = true;
                try
                {
                    SetValueInternal(SelectedIndexProperty, idx);
                    UpdateContainerSelection(selectedItem);
                }
                finally
                {
                    _updatingSelection = false;
                }
            }
            else
            {
                // Selected item no longer exists — clear selection
                _updatingSelection = true;
                try
                {
                    SetValueInternal(SelectedIndexProperty, -1);
                    SetValueInternal(SelectedItemProperty, null);
                    UpdateContainerSelection(null);
                }
                finally
                {
                    _updatingSelection = false;
                }
            }
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns the effective list of data items (ItemsSource cast to list, or Items).
    /// Protected so derived selectors (e.g. DataGrid) can render rows in the same
    /// order that <see cref="SelectedIndex"/> indexes.
    /// </summary>
    protected List<object> GetEffectiveItems()
    {
        var source = ItemsSource ?? Items;
        var list = new List<object>();
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
        return list;
    }
}
