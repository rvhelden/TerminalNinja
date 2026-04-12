namespace TerminalNinja.Controls;

/// <summary>
/// Defines a column for a <see cref="ListView"/>. Specifies the header text,
/// width, and optional data binding path for cell text.
/// </summary>
public class ListViewColumn
{
    /// <summary>Gets or sets the column header text.</summary>
    public string Header { get; set; } = "";

    /// <summary>
    /// Gets or sets the column width in characters.
    /// A value of 0 means star-sized (fills remaining space equally with other star columns).
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the property path on data items to extract cell text.
    /// When null, the item's ToString() is used for the first column.
    /// </summary>
    public string? DisplayMemberBinding { get; set; }
}
