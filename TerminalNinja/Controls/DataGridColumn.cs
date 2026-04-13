using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Defines a column for a <see cref="DataGrid"/> with sorting support.
/// Extends <see cref="ListViewColumn"/> with sort direction and sortability.
/// </summary>
public class DataGridColumn : ListViewColumn
{
    /// <summary>Gets or sets the current sort direction for this column.</summary>
    public SortDirection SortDirection { get; set; } = SortDirection.None;

    /// <summary>Gets or sets whether this column supports sorting.</summary>
    public bool IsSortable { get; set; } = true;
}
