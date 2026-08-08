using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Defines a column in a Grid with width specification.
/// </summary>
public class ColumnDefinition
{
    private int _minWidth;
    private int _maxWidth = int.MaxValue;
    
    /// <summary>
    /// Gets or sets the width of this column.
    /// Default is "*" (Star), meaning proportional sizing.
    /// </summary>
    public GridLength Width { get; set; } = GridLength.Star();

    /// <summary>
    /// Gets or sets the minimum width of this column in characters.
    /// </summary>
    public int MinWidth
    {
        get => _minWidth;
        set => _minWidth = Math.Max(0, value);
    }
    
    /// <summary>
    /// Gets or sets the maximum width of this column in characters.
    /// </summary>
    public int MaxWidth
    {
        get => _maxWidth;
        set => _maxWidth = Math.Max(0, value);
    }
    
    /// <summary>
    /// Groups this column with others of the same name, so they all take the size of the widest.
    /// </summary>
    /// <remarks>
    /// Only meaningful inside an element carrying <c>Grid.IsSharedSizeScope="True"</c>, which is
    /// what bounds the group — two unrelated screens using the name "keys" do not affect each
    /// other. A column in a group is measured from its content like Auto, whatever its
    /// <see cref="Width"/> says, because sharing a proportional size means nothing.
    /// </remarks>
    public string? SharedSizeGroup { get; set; }

    /// <summary>
    /// Gets or sets the actual width of this column after layout calculation.
    /// This is set during the Grid's measure/arrange pass.
    /// </summary>
    internal int ActualWidth { get; set; }
    
    /// <summary>
    /// Gets the offset (X position) of this column within the grid.
    /// This is set during the Grid's arrange pass.
    /// </summary>
    internal int Offset { get; set; }
}
