using System.ComponentModel;
using TerminalNinja.Xaml.TypeConverters;

namespace TerminalNinja.Controls;

/// <summary>
/// Represents a child control within a Stack container with its sizing mode.
/// </summary>
[TypeConverter(typeof(StackChildTypeConverter))]
public readonly record struct StackChild
{
    /// <summary>Gets the child control to render.</summary>
    public required IControl Content { get; init; }
    
    /// <summary>Gets the sizing mode for this child.</summary>
    public ChildSizeMode SizeMode { get; init; }
    
    /// <summary>Gets the fixed size in cells (only used when SizeMode is Fixed).</summary>
    public int FixedSize { get; init; }
    
    /// <summary>
    /// Creates a child with a fixed size.
    /// </summary>
    /// <param name="control">The control to render.</param>
    /// <param name="size">The fixed size in cells.</param>
    public static StackChild Fixed(IControl control, int size) => new()
    {
        Content = control,
        SizeMode = ChildSizeMode.Fixed,
        FixedSize = size
    };
    
    /// <summary>
    /// Creates a child that stretches to fill available space.
    /// </summary>
    /// <param name="control">The control to render.</param>
    public static StackChild Stretch(IControl control) => new()
    {
        Content = control,
        SizeMode = ChildSizeMode.Stretch
    };
    
    /// <summary>
    /// Creates a child that sizes based on its preferred size.
    /// </summary>
    /// <param name="control">The control to render.</param>
    public static StackChild Auto(IControl control) => new()
    {
        Content = control,
        SizeMode = ChildSizeMode.Auto
    };
}
