using System.Runtime.CompilerServices;
using Portable.Xaml;
using Portable.Xaml.Markup;
using TerminalNinja.Xaml.Binding;
using TerminalNinja.Xaml.Data;

namespace TerminalNinja.Xaml.Markup;

/// <summary>
/// Markup extension for data binding in XAML: {Binding Path=PropertyName}
/// </summary>
public class BindingExtension : MarkupExtension
{
    // Per-execution-context storage for pending bindings during XAML parsing.
    // Uses AsyncLocal to ensure parallel test execution doesn't share binding state.
    private static readonly AsyncLocal<Dictionary<BindingKey, BindingInfo>?> _pendingBindings = new();
    
    /// <summary>
    /// Gets or creates the pending bindings dictionary for the current execution context.
    /// </summary>
    private static Dictionary<BindingKey, BindingInfo> PendingBindings => 
        _pendingBindings.Value ??= new Dictionary<BindingKey, BindingInfo>();
    
    /// <summary>
    /// Gets or sets the path to the binding source property.
    /// </summary>
    public string? Path { get; set; }
    
    /// <summary>
    /// Gets or sets the binding mode (OneWay, TwoWay, OneTime).
    /// </summary>
    public BindingMode Mode { get; set; } = BindingMode.OneWay;
    
    /// <summary>
    /// Gets or sets the converter to use for value conversion.
    /// </summary>
    public IValueConverter? Converter { get; set; }
    
    /// <summary>
    /// Gets or sets a parameter to pass to the converter.
    /// </summary>
    public object? ConverterParameter { get; set; }
    
    /// <summary>
    /// Constructor with no arguments (Path must be set via property).
    /// </summary>
    public BindingExtension()
    {
    }
    
    /// <summary>
    /// Constructor with path (for shorthand: {Binding PropertyName}).
    /// </summary>
    public BindingExtension(string path)
    {
        Path = path;
    }
    
    /// <summary>
    /// Provides the value for the binding. Uses IProvideValueTarget to get target object
    /// and property, stores binding info for later processing, and returns a default value.
    /// </summary>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        // Validate path
        if (string.IsNullOrWhiteSpace(Path))
            throw new InvalidOperationException("Binding Path cannot be null or empty");
        
        // Get the target object and property from the service provider
        var target = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
        if (target?.TargetObject == null || target.TargetProperty == null)
        {
            // Can't get target info - this shouldn't normally happen
            throw new InvalidOperationException("Cannot resolve binding target");
        }
        
        // Get property name from target property
        string propertyName;
        Type? propertyType = null;
        
        if (target.TargetProperty is System.Reflection.PropertyInfo propInfo)
        {
            propertyName = propInfo.Name;
            propertyType = propInfo.PropertyType;
        }
        else if (target.TargetProperty is XamlMember xamlMember)
        {
            propertyName = xamlMember.Name;
            propertyType = xamlMember.Type?.UnderlyingType;
        }
        else
        {
            throw new InvalidOperationException($"Unsupported target property type: {target.TargetProperty.GetType()}");
        }
        
        // Store binding information for later processing
        var key = new BindingKey(target.TargetObject, propertyName);
        var info = new BindingInfo(Path, Mode, Converter, ConverterParameter);
        PendingBindings[key] = info;
        
        // Return a default value appropriate for the property type
        // This prevents type mismatch errors during XAML parsing
        if (propertyType != null)
        {
            if (propertyType == typeof(string))
                return string.Empty;
            if (propertyType.IsValueType)
                return Activator.CreateInstance(propertyType) ?? throw new InvalidOperationException($"Cannot create default value for {propertyType}");
            // For reference types (like ICommand), return null
            return null!;
        }
        
        return null!;
    }
    
    /// <summary>
    /// Gets all pending bindings and clears the internal storage for the current execution context.
    /// Called by TerminalXaml after XAML parsing is complete.
    /// </summary>
    internal static Dictionary<BindingKey, BindingInfo> GetAndClearPendingBindings()
    {
        var bindings = _pendingBindings.Value ?? new Dictionary<BindingKey, BindingInfo>();
        _pendingBindings.Value = null;
        return bindings;
    }
}

/// <summary>
/// Key for identifying a specific binding target (object + property).
/// </summary>
internal sealed class BindingKey : IEquatable<BindingKey>
{
    public object TargetObject { get; }
    public string PropertyName { get; }
    
    public BindingKey(object targetObject, string propertyName)
    {
        TargetObject = targetObject;
        PropertyName = propertyName;
    }
    
    public bool Equals(BindingKey? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return ReferenceEquals(TargetObject, other.TargetObject) && PropertyName == other.PropertyName;
    }
    
    public override bool Equals(object? obj) => Equals(obj as BindingKey);
    
    public override int GetHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(TargetObject), PropertyName);
}

/// <summary>
/// Information about a binding that needs to be set up.
/// </summary>
internal sealed class BindingInfo
{
    public string Path { get; }
    public BindingMode Mode { get; }
    public IValueConverter? Converter { get; }
    public object? ConverterParameter { get; }
    
    public BindingInfo(string path, BindingMode mode, IValueConverter? converter, object? converterParameter)
    {
        Path = path;
        Mode = mode;
        Converter = converter;
        ConverterParameter = converterParameter;
    }
}
