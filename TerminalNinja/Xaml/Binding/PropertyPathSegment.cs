using TerminalNinja.Aot;

namespace TerminalNinja.Xaml.Binding;

/// <summary>
/// Represents a single segment in a property path (e.g., "Name" in "User.Name").
/// Uses the AOT-compatible PropertyAccessorRegistry for property access.
/// </summary>
internal sealed class PropertyPathSegment
{
    private PropertyAccessor? _cachedAccessor;
    private Type? _cachedSourceType;
    
    public PropertyPathSegment(string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        PropertyName = propertyName;
    }
    
    /// <summary>
    /// Gets the property name for this segment.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// Gets the value of this property segment from the source object.
    /// </summary>
    public object? GetValue(object? source)
    {
        if (source == null)
        {
            return null;
        }

        var accessor = ResolveAccessor(source.GetType());
        return accessor.Getter(source);
    }
    
    /// <summary>
    /// Sets the value of this property segment on the target object.
    /// </summary>
    public void SetValue(object? target, object? value)
    {
        if (target == null)
        {
            return;
        }

        var targetType = target.GetType();
        var accessor = ResolveAccessor(targetType);
        
        if (!accessor.CanWrite)
        {
            throw new InvalidOperationException(
                $"Property '{PropertyName}' on type '{targetType.Name}' is read-only");
        }

        accessor.Setter!(target, value);
    }
    
    /// <summary>
    /// Gets the PropertyAccessor for this segment on the given source type.
    /// </summary>
    public PropertyAccessor? GetAccessor(Type sourceType)
    {
        if (_cachedAccessor.HasValue && _cachedSourceType == sourceType)
        {
            return _cachedAccessor;
        }

        if (PropertyAccessorRegistry.TryGetAccessor(sourceType, PropertyName, out var accessor))
        {
            _cachedAccessor = accessor;
            _cachedSourceType = sourceType;
            return accessor;
        }
        
        return null;
    }
    
    /// <summary>
    /// Resolves the accessor for the given type, throwing if not found.
    /// </summary>
    private PropertyAccessor ResolveAccessor(Type sourceType)
    {
        // Cache for performance
        if (_cachedAccessor.HasValue && _cachedSourceType == sourceType)
        {
            return _cachedAccessor.Value;
        }

        var accessor = PropertyAccessorRegistry.GetAccessor(sourceType, PropertyName);
        _cachedAccessor = accessor;
        _cachedSourceType = sourceType;
        
        return accessor;
    }
}
