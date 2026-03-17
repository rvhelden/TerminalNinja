namespace TerminalNinja.Controls;

/// <summary>
/// Provides data for the <see cref="Selector.SelectionChanged"/> event.
/// Matches WPF's System.Windows.Controls.SelectionChangedEventArgs pattern.
/// </summary>
public sealed class SelectionChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the list of items that were selected.
    /// </summary>
    public IList<object> AddedItems { get; }

    /// <summary>
    /// Gets the list of items that were unselected.
    /// </summary>
    public IList<object> RemovedItems { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SelectionChangedEventArgs"/> class.
    /// </summary>
    /// <param name="removedItems">The items that were unselected.</param>
    /// <param name="addedItems">The items that were selected.</param>
    public SelectionChangedEventArgs(IList<object> removedItems, IList<object> addedItems)
    {
        RemovedItems = removedItems ?? throw new ArgumentNullException(nameof(removedItems));
        AddedItems = addedItems ?? throw new ArgumentNullException(nameof(addedItems));
    }
}
