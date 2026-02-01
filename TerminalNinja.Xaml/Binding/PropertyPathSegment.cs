using System.Reflection;

namespace TerminalNinja.Xaml.Binding;

/// <summary>
/// Represents a single segment in a property path (e.g., "Name" in "User.Name").
/// Uses compiled reflection for fast property access.
/// </summary>
internal sealed class PropertyPathSegment
{
    private readonly string _propertyName;
    private PropertyInfo? _propertyInfo;
    private Type? _cachedSourceType;
    
    public PropertyPathSegment(string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        _propertyName = propertyName;
    }
    
    /// <summary>
    /// Gets the property name for this segment.
    /// </summary>
    public string PropertyName => _propertyName;
    
    /// <summary>
    /// Gets the value of this property segment from the source object.
    /// </summary>
    public object? GetValue(object? source)
    {
        if (source == null)
            return null;
        
        var sourceType = source.GetType();
        
        // Cache PropertyInfo for performance
        if (_propertyInfo == null || _cachedSourceType != sourceType)
        {
            _propertyInfo = sourceType.GetProperty(_propertyName, 
                BindingFlags.Public | BindingFlags.Instance);
            _cachedSourceType = sourceType;
            
            if (_propertyInfo == null)
                throw new InvalidOperationException(
                    $"Property '{_propertyName}' not found on type '{sourceType.Name}'");
        }
        
        return _propertyInfo.GetValue(source);
    }
    
    /// <summary>
    /// Sets the value of this property segment on the target object.
    /// </summary>
    public void SetValue(object? target, object? value)
    {
        if (target == null)
            return;
        
        var targetType = target.GetType();
        
        // Cache PropertyInfo for performance
        if (_propertyInfo == null || _cachedSourceType != targetType)
        {
            _propertyInfo = targetType.GetProperty(_propertyName, 
                BindingFlags.Public | BindingFlags.Instance);
            _cachedSourceType = targetType;
            
            if (_propertyInfo == null)
                throw new InvalidOperationException(
                    $"Property '{_propertyName}' not found on type '{targetType.Name}'");
        }
        
        if (!_propertyInfo.CanWrite)
            throw new InvalidOperationException(
                $"Property '{_propertyName}' on type '{targetType.Name}' is read-only");
        
        _propertyInfo.SetValue(target, value);
    }
    
    /// <summary>
    /// Gets the PropertyInfo for this segment on the given source type.
    /// </summary>
    public PropertyInfo? GetPropertyInfo(Type sourceType)
    {
        if (_propertyInfo == null || _cachedSourceType != sourceType)
        {
            _propertyInfo = sourceType.GetProperty(_propertyName, 
                BindingFlags.Public | BindingFlags.Instance);
            _cachedSourceType = sourceType;
        }
        
        return _propertyInfo;
    }
}
