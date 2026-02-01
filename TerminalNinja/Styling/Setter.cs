namespace TerminalNinja.Styling;

/// <summary>
/// Sets a property value when a style is applied.
/// </summary>
public class Setter
{
    /// <summary>
    /// Gets or sets the name of the property to set.
    /// </summary>
    public string? Property { get; set; }
    
    /// <summary>
    /// Gets or sets the value to set on the property.
    /// </summary>
    public object? Value { get; set; }
    
    /// <summary>
    /// Creates a new setter with no property or value.
    /// </summary>
    public Setter()
    {
    }
    
    /// <summary>
    /// Creates a new setter with the specified property and value.
    /// </summary>
    public Setter(string property, object? value)
    {
        Property = property;
        Value = value;
    }
}
