namespace TerminalNinja.Core.Primitives;

/// <summary>
/// Represents padding or margin thickness on all four sides of a rectangular region.
/// </summary>
public readonly record struct Thickness
{
    /// <summary>Gets the left thickness value.</summary>
    public int Left { get; init; }
    
    /// <summary>Gets the top thickness value.</summary>
    public int Top { get; init; }
    
    /// <summary>Gets the right thickness value.</summary>
    public int Right { get; init; }
    
    /// <summary>Gets the bottom thickness value.</summary>
    public int Bottom { get; init; }
    
    /// <summary>
    /// Creates a new Thickness with the specified values for each side.
    /// </summary>
    public Thickness(int left, int top, int right, int bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }
    
    /// <summary>
    /// Creates a new Thickness with the same value for all sides.
    /// </summary>
    public Thickness(int uniform) : this(uniform, uniform, uniform, uniform)
    {
    }
    
    /// <summary>
    /// Creates a new Thickness with horizontal and vertical values.
    /// </summary>
    public Thickness(int horizontal, int vertical) : this(horizontal, vertical, horizontal, vertical)
    {
    }
    
    /// <summary>Gets the total horizontal thickness (left + right).</summary>
    public int HorizontalTotal => Left + Right;
    
    /// <summary>Gets the total vertical thickness (top + bottom).</summary>
    public int VerticalTotal => Top + Bottom;
}
