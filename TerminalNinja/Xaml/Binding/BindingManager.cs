using System.Windows.Data;
using TerminalNinja.Aot;
using TerminalNinja.Controls;
using TerminalNinja.Xaml.Data;

namespace TerminalNinja.Xaml.Binding;

/// <summary>
/// Manages all active bindings in the UI tree.
/// Tracks BindingExpressions and handles DataContext propagation.
/// </summary>
public sealed class BindingManager : IDisposable
{
    private readonly List<BindingExpression> _bindings = new();
    private readonly Dictionary<FrameworkElement, object?> _elementDataContexts = new();
    
    /// <summary>
    /// Creates a new binding and adds it to the manager.
    /// </summary>
    public void CreateBinding(FrameworkElement target, string targetPropertyName, string sourcePath, BindingMode mode = BindingMode.OneWay, IValueConverter? converter = null, object? converterParameter = null, RelativeSource? relativeSource = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPropertyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        
        // Get target property accessor from registry (AOT-safe, no reflection)
        var targetAccessor = PropertyAccessorRegistry.GetAccessor(target.GetType(), targetPropertyName);
        
        // Create property path
        var propertyPath = new PropertyPath(sourcePath);
        
        // Create binding expression
        var binding = new BindingExpression(
            target,
            targetPropertyName,
            targetAccessor,
            propertyPath,
            mode,
            converter,
            converterParameter,
            hasRelativeSource: relativeSource != null);
        
        _bindings.Add(binding);
        
        // Resolve the binding source
        var source =
            // RelativeSource binding — resolve from the visual tree, not DataContext
            relativeSource != null ? ResolveSource(relativeSource, target) :
            // Standard DataContext binding
            GetDataContext(target);

        binding.Activate(source);
    }
    
    /// <summary>
    /// Sets the DataContext for a control and updates all bindings that depend on it.
    /// </summary>
    public void SetDataContext(FrameworkElement control, object? dataContext)
    {
        ArgumentNullException.ThrowIfNull(control);
        
        _elementDataContexts[control] = dataContext;
        control.DataContext = dataContext;
        
        // Update all bindings on this control
        UpdateBindingsForElement(control);
    }
    
    /// <summary>
    /// Gets the DataContext for a control (either explicit or inherited).
    /// </summary>
    public object? GetDataContext(FrameworkElement control)
    {
        // Check explicit DataContext first
        if (_elementDataContexts.TryGetValue(control, out var explicitContext))
        {
            return explicitContext;
        }

        // Fall back to control's own DataContext (which may be inherited via GetEffectiveDataContext)
        return control.GetEffectiveDataContext();
    }
    
    /// <summary>
    /// Updates all DataContext-based bindings for a specific control (reactivates them with current DataContext).
    /// RelativeSource bindings are not affected by DataContext changes.
    /// </summary>
    private void UpdateBindingsForElement(FrameworkElement control)
    {
        var dataContext = GetDataContext(control);
        
        foreach (var binding in _bindings.Where(b => ReferenceEquals(b.Target, control) && !b.HasRelativeSource))
        {
            binding.Activate(dataContext);
        }
    }
    
    /// <summary>
    /// Recursively sets DataContext on a control tree using the logical tree.
    /// </summary>
    public void SetDataContextRecursive(FrameworkElement root, object? dataContext)
    {
        SetDataContext(root, dataContext);

        // Use logical tree traversal — returns all children regardless of layout/size
        foreach (var child in root.GetLogicalChildren())
        {
            SetDataContextRecursive(child, dataContext);
        }
    }
    
    private Visual? ResolveSource(RelativeSource relativeSource, FrameworkElement target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return relativeSource.Mode switch
        {
            RelativeSourceMode.Self => target,
            RelativeSourceMode.FindAncestor => FindAncestor(relativeSource, target),
            RelativeSourceMode.TemplatedParent => null, // Template system not yet implemented
            _ => null
        };
    }

    private static Visual? FindAncestor(RelativeSource relativeSource, Visual target)
    {
        if (relativeSource.AncestorType == null)
        {
            throw new InvalidOperationException("AncestorType must be set when using RelativeSourceMode.FindAncestor.");
        }

        if (relativeSource.AncestorLevel < 1)
        {
            throw new InvalidOperationException($"AncestorLevel must be >= 1, but was {relativeSource.AncestorLevel}.");
        }

        var current = target.Parent;
        var matchCount = 0;

        while (current != null && relativeSource.AncestorType != null)
        {
            if (relativeSource.AncestorType.IsInstanceOfType(current))
            {
                matchCount++;
                if (matchCount >= relativeSource.AncestorLevel)
                {
                    return current;
                }
            }

            current = current.Parent;
        }

        return null; // Ancestor not found — WPF returns null silently
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
