namespace TerminalNinja.Controls.Charts;

/// <summary>
/// Determines how a <see cref="BarChart"/> arranges multiple series that share a category.
/// </summary>
public enum BarMode
{
    /// <summary>Series are drawn side by side within each category slot.</summary>
    Grouped = 0,

    /// <summary>Series values are stacked on top of one another within each category slot.</summary>
    Stacked = 1,
}
