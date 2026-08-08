namespace TerminalNinja.Xaml.Binding;

/// <summary>
/// Represents a property path like "User.Address.City" that can traverse multiple objects.
/// A path of <c>"."</c> (or an empty/omitted path, as written by <c>{Binding}</c>) is the
/// <em>self</em> path: it resolves to the source object itself rather than to a property on it,
/// which is what lets a collection of plain strings be templated without a wrapper view model.
/// </summary>
internal sealed class PropertyPath
{
    private static readonly PropertyPathSegment[] EmptySegments = [];

    private readonly PropertyPathSegment[] _segments;

    /// <summary>
    /// The self path — binds to the source object itself. Immutable and stateless, so one
    /// instance is shared by every pathless binding.
    /// </summary>
    public static PropertyPath Self { get; } = new(".");

    public PropertyPath(string? path)
    {
        if (IsSelfPath(path))
        {
            _segments = EmptySegments;
            return;
        }

        var parts = path!.Split('.', StringSplitOptions.RemoveEmptyEntries);
        _segments = parts.Select(p => new PropertyPathSegment(p.Trim())).ToArray();

        if (_segments.Length == 0)
        {
            throw new ArgumentException("Property path cannot be empty", nameof(path));
        }
    }

    /// <summary>
    /// Returns whether the given path string means "the source object itself":
    /// null, empty, whitespace, or the single dot WPF spells it with.
    /// </summary>
    public static bool IsSelfPath(string? path)
    {
        return string.IsNullOrWhiteSpace(path) || path.Trim() == ".";
    }

    /// <summary>
    /// Gets the original path string.
    /// </summary>
    public string Path => IsSelf ? "." : string.Join(".", _segments.Select(s => s.PropertyName));

    /// <summary>
    /// Gets the segments in this property path.
    /// </summary>
    public IReadOnlyList<PropertyPathSegment> Segments => _segments;

    /// <summary>
    /// Gets whether this is a simple path (single property, e.g., "Name").
    /// </summary>
    public bool IsSimple => _segments.Length == 1;

    /// <summary>
    /// Gets whether this path resolves to the source object itself (<c>{Binding}</c> / <c>Path=.</c>).
    /// </summary>
    public bool IsSelf => _segments.Length == 0;

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

        if (IsSelf)
        {
            // There is no property to write back to — the source *is* the value. WPF treats a
            // two-way binding on the self path the same way: the forward direction works, the
            // reverse has nowhere to go.
            throw new InvalidOperationException(
                "Cannot write back through a pathless binding — there is no source property to set.");
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
