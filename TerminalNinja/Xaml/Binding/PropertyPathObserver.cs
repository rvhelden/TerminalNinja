using System.ComponentModel;

namespace TerminalNinja.Xaml.Binding;

/// <summary>
/// Observes a property path and raises events when any property in the chain changes.
/// Handles multi-level INotifyPropertyChanged subscriptions (e.g., User.Address.City).
/// </summary>
internal sealed class PropertyPathObserver : IDisposable
{
    private readonly PropertyPath _path;
    private readonly Action _onChanged;
    private object? _source;
    private readonly List<INotifyPropertyChanged> _subscribedObjects = new();
    
    public PropertyPathObserver(PropertyPath path, object? source, Action onChanged)
    {
        _path = path;
        _source = source;
        _onChanged = onChanged;
        
        Subscribe();
    }
    
    /// <summary>
    /// Updates the source object and resubscribes to property changes.
    /// </summary>
    public void UpdateSource(object? newSource)
    {
        if (ReferenceEquals(_source, newSource))
        {
            return;
        }

        Unsubscribe();
        _source = newSource;
        Subscribe();
    }
    
    /// <summary>
    /// Subscribes to PropertyChanged events for all objects in the property path.
    /// </summary>
    private void Subscribe()
    {
        if (_source == null)
        {
            return;
        }

        var current = _source;
        
        // Subscribe to each level in the path
        for (var i = 0; i < _path.Segments.Count; i++)
        {
            // Subscribe to this level's PropertyChanged
            if (current is INotifyPropertyChanged inpc)
            {
                inpc.PropertyChanged += OnPropertyChanged;
                _subscribedObjects.Add(inpc);
            }
            
            // Navigate to next level (skip for the last segment)
            if (i < _path.Segments.Count - 1)
            {
                current = _path.Segments[i].GetValue(current);
                if (current == null)
                {
                    break; // Stop if intermediate value is null
                }
            }
        }
    }
    
    /// <summary>
    /// Unsubscribes from all PropertyChanged events.
    /// </summary>
    private void Unsubscribe()
    {
        foreach (var inpc in _subscribedObjects)
        {
            inpc.PropertyChanged -= OnPropertyChanged;
        }
        _subscribedObjects.Clear();
    }
    
    /// <summary>
    /// Handles PropertyChanged events from any object in the chain.
    /// </summary>
    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Check if the changed property is part of our path
        var propertyName = e.PropertyName;
        
        // If PropertyName is null or empty, it means all properties changed
        if (string.IsNullOrEmpty(propertyName))
        {
            // Resubscribe to handle structural changes
            Unsubscribe();
            Subscribe();
            _onChanged();
            return;
        }
        
        // Check if this property is in our path
        var isInPath = _path.Segments.Any(s => s.PropertyName == propertyName);
        
        if (isInPath)
        {
            // If an intermediate property changed, we need to resubscribe
            // because the object graph may have changed
            var segmentIndex = Array.FindIndex(_path.Segments.ToArray(), s => s.PropertyName == propertyName);
            
            if (segmentIndex < _path.Segments.Count - 1)
            {
                // Intermediate property changed - resubscribe
                Unsubscribe();
                Subscribe();
            }
            
            _onChanged();
        }
    }
    
    public void Dispose()
    {
        Unsubscribe();
    }
}
