using TerminalNinja.Markup;

namespace TerminalNinja.Styling;

/// <summary>
/// Represents a style that can be applied to elements.
/// Contains a collection of setters that set property values.
/// </summary>
[ContentProperty("Setters")]
public class Style
{
    /// <summary>
    /// Gets or sets the type that this style targets.
    /// If set, the style can only be applied to elements of this type or derived types.
    /// </summary>
    public Type? TargetType { get; set; }
    
    /// <summary>
    /// Gets or sets the style that this style is based on.
    /// Setters from the base style are applied first.
    /// </summary>
    public Style? BasedOn { get; set; }
    
    /// <summary>
    /// Gets the collection of setters that define property values.
    /// </summary>
    public IList<Setter> Setters { get; } = new List<Setter>();
    
    /// <summary>
    /// Creates a new empty style.
    /// </summary>
    public Style()
    {
    }
    
    /// <summary>
    /// Creates a new style with the specified target type.
    /// </summary>
    public Style(Type targetType)
    {
        TargetType = targetType;
    }
}
