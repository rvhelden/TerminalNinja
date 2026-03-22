namespace TerminalNinja.Xaml.Binding;

/// <summary>
/// Represents a property path like "User.Address.City" that can traverse multiple objects.
/// </summary>
internal sealed class PropertyPath
{
    private readonly PropertyPathSegment[] _segments;
    
    public PropertyPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        _segments = parts.Select(p => new PropertyPathSegment(p.Trim())).ToArray();
        
        if (_segments.Length == 0)
        {
            throw new ArgumentException("Property path cannot be empty", nameof(path));
        }
    }
    
    /// <summary>
    /// Gets the original path string.
    /// </summary>
    public string Path => string.Join(".", _segments.Select(s => s.PropertyName));
    
    /// <summary>
    /// Gets the segments in this property path.
    /// </summary>
    public IReadOnlyList<PropertyPathSegment> Segments => _segments;
    
    /// <summary>
    /// Gets whether this is a simple path (single property, e.g., "Name").
    /// </summary>
    public bool IsSimple => _segments.Length == 1;
    
    /// <summary>
    /// Gets the final value by traversing the entire path from the source.
    /// Returns null if any intermediate value is null.
    /// </summary>
    public object? GetValue(object? source)
    {
        if (source == null)
        {
            return null;
        }

        var current = source;
        
        foreach (var segment in _segments)
        {
            current = segment.GetValue(current);
            if (current == null)
            {
                return null;
            }
        }
        
        return current;
    }
    
    /// <summary>
    /// Sets the final property value by traversing the path.
    /// Throws if any intermediate value is null or the property is read-only.
    /// </summary>
    public void SetValue(object? source, object? value)
    {
        if (source == null)
        {
            throw new InvalidOperationException("Cannot set value on null source");
        }

        // Navigate to the parent of the final property
        var current = source;
        for (var i = 0; i < _segments.Length - 1; i++)
        {
            current = _segments[i].GetValue(current);
            if (current == null)
            {
                throw new InvalidOperationException(
                    $"Cannot set property '{Path}' - intermediate value at '{_segments[i].PropertyName}' is null");
            }
        }
        
        // Set the final property
        _segments[^1].SetValue(current, value);
    }
    
    /// <summary>
    /// Gets an intermediate value at the specified segment index.
    /// Used by PropertyPathObserver to subscribe to intermediate objects.
    /// </summary>
    internal object? GetValueAtSegment(object? source, int segmentIndex)
    {
        if (source == null || segmentIndex < 0 || segmentIndex >= _segments.Length)
        {
            return null;
        }

        var current = source;
        
        for (var i = 0; i <= segmentIndex; i++)
        {
            current = _segments[i].GetValue(current);
            if (current == null)
            {
                return null;
            }
        }
        
        return current;
    }
}
