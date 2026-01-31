namespace TerminalNinja.Core.Elements;

/// <summary>
/// Represents a child element within a Stack container with its sizing mode.
/// </summary>
public readonly record struct StackChild
{
    /// <summary>Gets the child element to render.</summary>
    public required IElement Element { get; init; }
    
    /// <summary>Gets the sizing mode for this child.</summary>
    public ChildSizeMode SizeMode { get; init; }
    
    /// <summary>Gets the fixed size in cells (only used when SizeMode is Fixed).</summary>
    public int FixedSize { get; init; }
    
    /// <summary>
    /// Creates a child with a fixed size.
    /// </summary>
    /// <param name="element">The element to render.</param>
    /// <param name="size">The fixed size in cells.</param>
    public static StackChild Fixed(IElement element, int size) => new()
    {
        Element = element,
        SizeMode = ChildSizeMode.Fixed,
        FixedSize = size
    };
    
    /// <summary>
    /// Creates a child that stretches to fill available space.
    /// </summary>
    /// <param name="element">The element to render.</param>
    public static StackChild Stretch(IElement element) => new()
    {
        Element = element,
        SizeMode = ChildSizeMode.Stretch
    };
    
    /// <summary>
    /// Creates a child that sizes based on its preferred size.
    /// </summary>
    /// <param name="element">The element to render.</param>
    public static StackChild Auto(IElement element) => new()
    {
        Element = element,
        SizeMode = ChildSizeMode.Auto
    };
}
