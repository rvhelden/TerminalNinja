using TerminalNinja.Controls;

namespace TerminalNinja.Xaml.Binding;

/// <summary>
/// Manages all active bindings in the UI tree.
/// Tracks BindingExpressions and handles DataContext propagation.
/// </summary>
public sealed class BindingManager : IDisposable
{
    private readonly List<BindingExpression> _bindings = new();
    private readonly Dictionary<IControl, object?> _elementDataContexts = new();
    
    /// <summary>
    /// Creates a new binding and adds it to the manager.
    /// </summary>
    public void CreateBinding(
        IControl target,
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
    /// Sets the DataContext for an control and updates all bindings that depend on it.
    /// </summary>
    public void SetDataContext(IControl control, object? dataContext)
    {
        ArgumentNullException.ThrowIfNull(control);
        
        _elementDataContexts[control] = dataContext;
        control.DataContext = dataContext;
        
        // Update all bindings on this control
        UpdateBindingsForElement(control);
    }
    
    /// <summary>
    /// Gets the DataContext for an control (either explicit or inherited).
    /// </summary>
    public object? GetDataContext(IControl control)
    {
        // Check explicit DataContext first
        if (_elementDataContexts.TryGetValue(control, out var explicitContext))
            return explicitContext;
        
        // Fall back to control's own DataContext (which may be inherited via GetEffectiveDataContext)
        if (control is ControlBase elementBase)
            return elementBase.GetEffectiveDataContext();
        
        return control.DataContext;
    }
    
    /// <summary>
    /// Updates all bindings for a specific control (reactivates them with current DataContext).
    /// </summary>
    private void UpdateBindingsForElement(IControl control)
    {
        var dataContext = GetDataContext(control);
        
        foreach (var binding in _bindings.Where(b => ReferenceEquals(b.Target, control)))
        {
            binding.Activate(dataContext);
        }
    }
    
    /// <summary>
    /// Recursively sets DataContext on an control tree.
    /// </summary>
    public void SetDataContextRecursive(IControl root, object? dataContext)
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
                    if (child.Content != null)
                        SetDataContextRecursive(child.Content, dataContext);
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
