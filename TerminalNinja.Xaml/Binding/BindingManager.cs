using TerminalNinja.Core.Elements;

namespace TerminalNinja.Xaml.Binding;

/// <summary>
/// Manages all active bindings in the UI tree.
/// Tracks BindingExpressions and handles DataContext propagation.
/// </summary>
public sealed class BindingManager : IDisposable
{
    private readonly List<BindingExpression> _bindings = new();
    private readonly Dictionary<IElement, object?> _elementDataContexts = new();
    
    /// <summary>
    /// Creates a new binding and adds it to the manager.
    /// </summary>
    public void CreateBinding(
        IElement target,
        string targetPropertyName,
        string sourcePath,
        BindingMode mode = BindingMode.OneWay,
        IValueConverter? converter = null,
        object? converterParameter = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPropertyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        
        // Get target property
        var targetProperty = target.GetType().GetProperty(targetPropertyName);
        if (targetProperty == null)
            throw new ArgumentException($"Property '{targetPropertyName}' not found on type '{target.GetType().Name}'");
        
        // Create property path
        var propertyPath = new PropertyPath(sourcePath);
        
        // Create binding expression
        var binding = new BindingExpression(
            target,
            targetProperty,
            propertyPath,
            mode,
            converter,
            converterParameter);
        
        _bindings.Add(binding);
        
        // Activate with current DataContext
        var dataContext = GetDataContext(target);
        binding.Activate(dataContext);
    }
    
    /// <summary>
    /// Sets the DataContext for an element and updates all bindings that depend on it.
    /// </summary>
    public void SetDataContext(IElement element, object? dataContext)
    {
        ArgumentNullException.ThrowIfNull(element);
        
        _elementDataContexts[element] = dataContext;
        element.DataContext = dataContext;
        
        // Update all bindings on this element
        UpdateBindingsForElement(element);
    }
    
    /// <summary>
    /// Gets the DataContext for an element (either explicit or inherited).
    /// </summary>
    public object? GetDataContext(IElement element)
    {
        // Check explicit DataContext first
        if (_elementDataContexts.TryGetValue(element, out var explicitContext))
            return explicitContext;
        
        // Fall back to element's own DataContext (which may be inherited via GetEffectiveDataContext)
        if (element is ElementBase elementBase)
            return elementBase.GetEffectiveDataContext();
        
        return element.DataContext;
    }
    
    /// <summary>
    /// Updates all bindings for a specific element (reactivates them with current DataContext).
    /// </summary>
    private void UpdateBindingsForElement(IElement element)
    {
        var dataContext = GetDataContext(element);
        
        foreach (var binding in _bindings.Where(b => ReferenceEquals(b.Target, element)))
        {
            binding.Activate(dataContext);
        }
    }
    
    /// <summary>
    /// Recursively sets DataContext on an element tree.
    /// </summary>
    public void SetDataContextRecursive(IElement root, object? dataContext)
    {
        SetDataContext(root, dataContext);
        
        // Recursively set on children
        switch (root)
        {
            case Rectangle rect when rect.Child != null:
                SetDataContextRecursive(rect.Child, dataContext);
                break;
            
            case Stack stack:
                foreach (var child in stack.Children)
                    if (child.Element != null)
                        SetDataContextRecursive(child.Element, dataContext);
                break;
        }
    }
    
    /// <summary>
    /// Gets all bindings for debugging/inspection purposes.
    /// </summary>
    internal IReadOnlyList<BindingExpression> GetAllBindings() => _bindings.AsReadOnly();
    
    /// <summary>
    /// Clears all bindings and disposes them.
    /// </summary>
    public void Clear()
    {
        foreach (var binding in _bindings)
        {
            binding.Dispose();
        }
        _bindings.Clear();
        _elementDataContexts.Clear();
    }
    
    public void Dispose()
    {
        Clear();
    }
}
