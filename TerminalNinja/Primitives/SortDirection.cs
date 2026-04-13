namespace TerminalNinja.Primitives;

/// <summary>
/// Specifies the sort direction for a column in a <see cref="Controls.DataGrid"/>.
/// </summary>
public enum SortDirection : byte
{
    /// <summary>No sorting applied.</summary>
    None = 0,

    /// <summary>Sorted in ascending order (A→Z, 0→9).</summary>
    Ascending = 1,

    /// <summary>Sorted in descending order (Z→A, 9→0).</summary>
    Descending = 2
}
