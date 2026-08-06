using TerminalNinja.Aot;
using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Abstract base class for columns in a <see cref="DataGrid"/>.
/// Defines header text, width, and sorting configuration.
/// Corresponds to WPF's System.Windows.Controls.DataGridColumn.
/// </summary>
public abstract class DataGridColumn
{
    /// <summary>Gets or sets the column header text.</summary>
    public string Header { get; set; } = "";

    /// <summary>
    /// Gets or sets the column width in characters.
    /// A value of 0 means star-sized (fills remaining space equally with other star columns).
    /// </summary>
    public int Width { get; set; }

    /// <summary>Gets or sets the current sort direction for this column.</summary>
    public SortDirection SortDirection { get; set; } = SortDirection.None;

    /// <summary>Gets or sets whether the user can sort by this column.</summary>
    public bool CanUserSort { get; set; } = true;

    /// <summary>
    /// Gets or sets the property path used for sorting.
    /// When null, bound columns default to their <c>Binding</c> path.
    /// </summary>
    public string? SortMemberPath { get; set; }

    /// <summary>
    /// Gets the value used for sorting from a data item.
    /// </summary>
    internal virtual object? GetSortValue(object? dataItem)
    {
        if (dataItem == null || string.IsNullOrEmpty(SortMemberPath)) return null;
        if (PropertyAccessorRegistry.TryGetAccessor(dataItem.GetType(), SortMemberPath, out var accessor))
            return accessor.Value.Getter(dataItem);
        return null;
    }

    /// <summary>
    /// Renders a single cell for this column into the buffer.
    /// Protected internal so applications can define custom column types
    /// (e.g. per-cell colours or graphical cells) without forking the framework.
    /// </summary>
    /// <param name="buffer">The cell buffer to render into.</param>
    /// <param name="x">The X coordinate of the cell.</param>
    /// <param name="y">The Y coordinate of the cell.</param>
    /// <param name="width">The character width of the cell.</param>
    /// <param name="dataItem">The data item for this row.</param>
    /// <param name="fg">The foreground color.</param>
    /// <param name="bg">The background color.</param>
    protected internal abstract void RenderCell(CellBuffer buffer, int x, int y, int width,
        object? dataItem, Color fg, Color bg);
}
