namespace TerminalNinja.Controls;

/// <summary>
/// Shared interface for item containers that support a selected state.
/// Implemented by <see cref="ListBoxItem"/> and <see cref="ComboBoxItem"/>
/// so that <see cref="Primitives.Selector.UpdateContainerSelection"/> can
/// update selection state without casting to a specific container type.
/// </summary>
internal interface ISelectableContainer
{
    bool IsSelected { get; set; }
}
