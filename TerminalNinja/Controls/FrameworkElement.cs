using TerminalNinja.Aot;
using TerminalNinja.Resources;
using TerminalNinja.Styling;

namespace TerminalNinja.Controls;

/// <summary>
/// Base class for controls that support resources and styles.
/// Extends ControlBase with ResourceDictionary and resource lookup capabilities.
/// </summary>
public abstract class FrameworkElement : ControlBase
{
    private ResourceDictionary? _resources;
    private Style? _style;
    
    /// <summary>
    /// Gets the resource dictionary for this control.
    /// Resources defined here take precedence over parent resources.
    /// </summary>
    public ResourceDictionary Resources => _resources ??= new ResourceDictionary();
    
    /// <summary>
    /// Gets or sets whether this control has any resources defined.
    /// </summary>
    internal bool HasResources => _resources != null && _resources.Count > 0;
    
    /// <summary>
    /// Gets or sets the style applied to this control.
    /// </summary>
    public Style? Style
    {
        get => _style;
        set
        {
            if (SetProperty(ref _style, value, invalidate: false))
            {
                ApplyStyle();
                InvalidateVisual();
            }
        }
    }
    
    /// <summary>
    /// Finds a resource by key, walking up the visual tree.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <returns>The resource value, or null if not found.</returns>
    public object? TryFindResource(object key)
    {
        ArgumentNullException.ThrowIfNull(key);
        
        // 1. Check own Resources
        if (_resources != null && _resources.TryGetValue(key, out var value))
            return value;
        
        // 2. Walk up Parent chain (if FrameworkElement)
        if (Parent is FrameworkElement parentFe)
            return parentFe.TryFindResource(key);
        
        // 3. Check Application.Current.Resources (will be implemented later)
        // For now, we'll add a static hook that can be set by Application
        return ApplicationResourceLookup?.Invoke(key);
    }
    
    /// <summary>
    /// Finds a resource by key, throwing if not found.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <returns>The resource value.</returns>
    /// <exception cref="ResourceNotFoundException">Thrown if the resource is not found.</exception>
    public object FindResource(object key)
    {
        var result = TryFindResource(key);
        if (result == null)
            throw new ResourceNotFoundException($"Resource with key '{key}' not found");
        return result;
    }
    
    /// <summary>
    /// Static hook for Application to provide resource lookup.
    /// Set by Application when it's created.
    /// </summary>
    internal static Func<object, object?>? ApplicationResourceLookup { get; set; }
    
    /// <summary>
    /// Applies the current style to this control by setting property values.
    /// </summary>
    protected virtual void ApplyStyle()
    {
        if (_style == null) return;
        
        // Check if style is compatible with this control type
        if (_style.TargetType != null && !_style.TargetType.IsInstanceOfType(this))
        {
            throw new InvalidOperationException(
                $"Style with TargetType '{_style.TargetType.Name}' cannot be applied to control of type '{GetType().Name}'");
        }
        
        // Apply each setter
        var controlType = GetType();
        foreach (var setter in _style.Setters)
        {
            if (string.IsNullOrEmpty(setter.Property))
                continue;
            
            var accessor = PropertyAccessorRegistry.GetAccessor(controlType, setter.Property);
            
            if (!accessor.CanWrite)
            {
                throw new InvalidOperationException(
                    $"Property '{setter.Property}' on type '{controlType.Name}' is read-only");
            }
            
            // Convert value if needed
            var value = setter.Value;
            if (value != null && !accessor.PropertyType.IsInstanceOfType(value))
            {
                // Try to use TypeConverterRegistry (AOT-safe)
                var converter = TypeConverterRegistry.GetConverterOrEnum(accessor.PropertyType);
                if (converter != null && converter.CanConvertFrom(value.GetType()))
                {
                    value = converter.ConvertFrom(value);
                }
            }
            
            accessor.Setter!(this, value);
        }
    }
}

/// <summary>
/// Exception thrown when a resource lookup fails.
/// </summary>
public class ResourceNotFoundException : Exception
{
    public ResourceNotFoundException(string message) : base(message) { }
    public ResourceNotFoundException(string message, Exception innerException) : base(message, innerException) { }
}
