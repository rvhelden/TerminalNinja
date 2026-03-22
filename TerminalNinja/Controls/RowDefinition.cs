using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Defines a row in a Grid with height specification.
/// </summary>
public class RowDefinition
{
    private int _minHeight;
    private int _maxHeight = int.MaxValue;
    
    /// <summary>
    /// Gets or sets the height of this row.
    /// Default is "*" (Star), meaning proportional sizing.
    /// </summary>
    public GridLength Height { get; set; } = GridLength.Star();

    /// <summary>
    /// Gets or sets the minimum height of this row in characters.
    /// </summary>
    public int MinHeight
    {
        get => _minHeight;
        set => _minHeight = Math.Max(0, value);
    }
    
    /// <summary>
    /// Gets or sets the maximum height of this row in characters.
    /// </summary>
    public int MaxHeight
    {
        get => _maxHeight;
        set => _maxHeight = Math.Max(0, value);
    }
    
    /// <summary>
    /// Gets or sets the actual height of this row after layout calculation.
    /// This is set during the Grid's measure/arrange pass.
    /// </summary>
    internal int ActualHeight { get; set; }
    
    /// <summary>
    /// Gets the offset (Y position) of this row within the grid.
    /// This is set during the Grid's arrange pass.
    /// </summary>
    internal int Offset { get; set; }
}
