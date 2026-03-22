using TerminalNinja.Aot;
using TerminalNinja.Primitives;
using TerminalNinja.Resources;
using TerminalNinja.Styling;
using TerminalNinja.Xaml.Binding;

namespace TerminalNinja.Controls;

/// <summary>
/// Base class for controls that participate in the WPF-like framework features:
/// resources, styles, data context, name, and common layout properties.
/// Inherits from UIElement (which provides invalidation and layout abstracts).
/// </summary>
public abstract class FrameworkElement : UIElement
{
    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty HorizontalAlignmentProperty =
        DependencyProperty.Register(nameof(HorizontalAlignment), typeof(Alignment), typeof(FrameworkElement),
            new FrameworkPropertyMetadata(Alignment.Start, affectsRender: true));

    public static readonly DependencyProperty VerticalAlignmentProperty =
        DependencyProperty.Register(nameof(VerticalAlignment), typeof(Alignment), typeof(FrameworkElement),
            new FrameworkPropertyMetadata(Alignment.Start, affectsRender: true));

    public static readonly DependencyProperty NameProperty =
        DependencyProperty.Register(nameof(Name), typeof(string), typeof(FrameworkElement),
            new PropertyMetadata((object?)null));

    public static readonly DependencyProperty DataContextProperty =
        DependencyProperty.Register(nameof(DataContext), typeof(object), typeof(FrameworkElement),
            new PropertyMetadata((object?)null,
                propertyChangedCallback: (d, e) => ((FrameworkElement)d).OnDataContextChanged(e.OldValue, e.NewValue)));

    public static readonly DependencyProperty StyleProperty =
        DependencyProperty.Register(nameof(Style), typeof(Style), typeof(FrameworkElement),
            new FrameworkPropertyMetadata(null, affectsRender: true,
                propertyChangedCallback: (d, _) => ((FrameworkElement)d).ApplyStyle()));

    /// <summary>
    /// Gets or sets the horizontal alignment of this element within its parent container.
    /// </summary>
    public Alignment HorizontalAlignment
    {
        get => (Alignment)GetValue(HorizontalAlignmentProperty)!;
        set => SetValue(HorizontalAlignmentProperty, value);
    }
    
    /// <summary>
    /// Gets or sets the vertical alignment of this element within its parent container.
    /// </summary>
    public Alignment VerticalAlignment
    {
        get => (Alignment)GetValue(VerticalAlignmentProperty)!;
        set => SetValue(VerticalAlignmentProperty, value);
    }
    
    /// <summary>
    /// Gets or sets the name of this element for lookup purposes (e.g., XAML x:Name).
    /// </summary>
    public string? Name
    {
        get => (string?)GetValue(NameProperty);
        set => SetValue(NameProperty, value);
    }
    
    /// <summary>
    /// Gets or sets the data context for this element. Used as the source for data bindings.
    /// If null, bindings will walk up the Parent chain to find an inherited DataContext.
    /// </summary>
    public object? DataContext
    {
        get => GetValue(DataContextProperty);
        set => SetValue(DataContextProperty, value);
    }

    // ─── Deferred Binding Support ─────────────────────────────────

    /// <summary>
    /// Pending bindings stored when XAML is loaded without a DataContext.
    /// Activated when DataContext is first set to a non-null value.
    /// </summary>
    private List<ElementBinding>? _pendingBindings;

    /// <summary>
    /// Adds a pending binding to be activated when DataContext becomes available.
    /// Called by the XAML loader when dataContext is null at load time.
    /// </summary>
    internal void AddPendingBinding(ElementBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        _pendingBindings ??= [];
        _pendingBindings.Add(binding);
    }

    /// <summary>
    /// Returns the pending bindings for this element (for testing/inspection).
    /// </summary>
    internal IReadOnlyList<ElementBinding>? PendingBindings => _pendingBindings;

    /// <summary>
    /// Activates all pending bindings using the specified BindingManager and DataContext.
    /// One-shot: clears the pending list after activation.
    /// </summary>
    internal void ActivatePendingBindings(BindingManager bindingManager, object dataContext)
    {
        if (_pendingBindings == null || _pendingBindings.Count == 0)
        {
            return;
        }

        var bindings = _pendingBindings;
        _pendingBindings = null; // Clear before activating to prevent re-entrancy

        // Ensure the BindingManager knows about this element's DataContext
        // so that CreateBinding → GetDataContext returns the correct source.
        bindingManager.SetDataContext(this, dataContext);

        foreach (var pb in bindings)
        {
            bindingManager.CreateBinding(
                this,
                pb.TargetPropertyName,
                pb.Path,
                pb.Mode,
                pb.Converter,
                pb.ConverterParameter,
                pb.RelativeSource);
        }
    }

    /// <summary>
    /// Called when DataContext changes. If transitioning from null to non-null
    /// and pending bindings exist, activates them with a new BindingManager.
    /// </summary>
    private void OnDataContextChanged(object? oldValue, object? newValue)
    {
        if (newValue != null && _pendingBindings is { Count: > 0 })
        {
            var manager = new BindingManager();
            ActivatePendingBindings(manager, newValue);
        }
    }

    private ResourceDictionary? _resources;

    /// <summary>
    /// Gets or sets the effective DataContext by walking up the parent chain if needed.
    /// </summary>
    public object? GetEffectiveDataContext()
    {
        var dc = DataContext;
        if (dc != null)
        {
            return dc;
        }

        return Parent switch
        {
            FrameworkElement fe => fe.GetEffectiveDataContext(),
            _ => null
        };
    }
    
    /// <summary>
    /// Gets the resource dictionary for this element.
    /// Resources defined here take precedence over parent resources.
    /// </summary>
    public ResourceDictionary Resources => _resources ??= new ResourceDictionary();
    
    /// <summary>
    /// Gets or sets whether this element has any resources defined.
    /// </summary>
    internal bool HasResources => _resources != null && _resources.Count > 0;
    
    /// <summary>
    /// Gets or sets the style applied to this element.
    /// </summary>
    public Style? Style
    {
        get => (Style?)GetValue(StyleProperty);
        set => SetValue(StyleProperty, value);
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
        {
            return value;
        }

        // 2. Walk up Parent chain (if FrameworkElement)
        if (Parent is FrameworkElement parentFe)
        {
            return parentFe.TryFindResource(key);
        }

        // 3. Check Application.Current.Resources
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
        {
            throw new ResourceNotFoundException($"Resource with key '{key}' not found");
        }

        return result;
    }
    
    /// <summary>
    /// Static hook for Application to provide resource lookup.
    /// Set by Application when it's created.
    /// </summary>
    internal static Func<object, object?>? ApplicationResourceLookup { get; set; }
    
    /// <summary>
    /// Applies alignment to position a resolved rect (w x h) within the parent bounds.
    /// </summary>
    /// <param name="parent">The parent bounds to align within.</param>
    /// <param name="w">The resolved width of this element.</param>
    /// <param name="h">The resolved height of this element.</param>
    /// <returns>A Rect positioned according to HorizontalAlignment and VerticalAlignment.</returns>
    protected Rect ApplyAlignment(Rect parent, int w, int h)
    {
        var x = HorizontalAlignment switch
        {
            Alignment.Center => parent.X + (parent.Width - w) / 2,
            Alignment.End => parent.X + parent.Width - w,
            _ => parent.X // Start
        };
        
        var y = VerticalAlignment switch
        {
            Alignment.Center => parent.Y + (parent.Height - h) / 2,
            Alignment.End => parent.Y + parent.Height - h,
            _ => parent.Y // Start
        };
        
        return new Rect(x, y, w, h);
    }
    
    /// <summary>
    /// Enumerates the logical children of this element.
    /// Used for DataContext propagation and resource lookup — unlike
    /// <see cref="Visual.GetChildrenWithBounds"/>, this method returns all children
    /// regardless of layout or size.
    /// </summary>
    protected internal virtual IEnumerable<FrameworkElement> GetLogicalChildren()
    {
        return [];
    }

    /// <summary>
    /// Applies the current style to this element by setting property values.
    /// </summary>
    protected virtual void ApplyStyle()
    {
        var style = Style;
        if (style == null)
        {
            return;
        }

        // Check if style is compatible with this element type
        if (style.TargetType != null && !style.TargetType.IsInstanceOfType(this))
        {
            throw new InvalidOperationException(
                $"Style with TargetType '{style.TargetType.Name}' cannot be applied to control of type '{GetType().Name}'");
        }
        
        // Apply each setter
        var controlType = GetType();
        foreach (var setter in style.Setters)
        {
            if (string.IsNullOrEmpty(setter.Property))
            {
                continue;
            }

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
