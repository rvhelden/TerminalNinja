namespace TerminalNinja.DependencySystem;

/// <summary>
/// Callback invoked when a dependency property value changes.
/// </summary>
public delegate void PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e);

/// <summary>
/// Callback invoked to coerce a dependency property value before it is stored.
/// </summary>
public delegate object? CoerceValueCallback(DependencyObject d, object? baseValue);

/// <summary>
/// Metadata for a dependency property, specifying its default value and change callbacks.
/// </summary>
public class PropertyMetadata
{
    /// <summary>Gets the default value of the dependency property.</summary>
    public object? DefaultValue { get; }

    /// <summary>Gets the callback invoked when the property value changes.</summary>
    public PropertyChangedCallback? PropertyChangedCallback { get; }

    /// <summary>Gets the callback invoked to coerce the property value.</summary>
    public CoerceValueCallback? CoerceValueCallback { get; }

    public PropertyMetadata() { }

    public PropertyMetadata(object? defaultValue)
    {
        DefaultValue = defaultValue;
    }

    public PropertyMetadata(PropertyChangedCallback propertyChangedCallback)
    {
        PropertyChangedCallback = propertyChangedCallback;
    }

    public PropertyMetadata(object? defaultValue, PropertyChangedCallback? propertyChangedCallback)
    {
        DefaultValue = defaultValue;
        PropertyChangedCallback = propertyChangedCallback;
    }

    public PropertyMetadata(object? defaultValue, PropertyChangedCallback? propertyChangedCallback, CoerceValueCallback? coerceValueCallback)
    {
        DefaultValue = defaultValue;
        PropertyChangedCallback = propertyChangedCallback;
        CoerceValueCallback = coerceValueCallback;
    }
}
