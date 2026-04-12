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

    public static readonly DependencyProperty MarginProperty =
        DependencyProperty.Register(nameof(Margin), typeof(Thickness), typeof(FrameworkElement),
            new FrameworkPropertyMetadata(new Thickness(0), affectsRender: true));

    public static readonly DependencyProperty NameProperty =
        DependencyProperty.Register(nameof(Name), typeof(string), typeof(FrameworkElement),
            new PropertyMetadata((object?)null));

    public static readonly DependencyProperty DataContextProperty =
        DependencyProperty.Register(nameof(DataContext), typeof(object), typeof(FrameworkElement),
            new PropertyMetadata(null,
                propertyChangedCallback: (d, e) => ((FrameworkElement)d).OnDataContextChanged(e.OldValue, e.NewValue)));

    public static readonly DependencyProperty StyleProperty =
        DependencyProperty.Register(nameof(Style), typeof(Style), typeof(FrameworkElement),
            new FrameworkPropertyMetadata(null, affectsRender: true,
                propertyChangedCallback: (d, _) => ((FrameworkElement)d).OnStyleChanged()));

    /// <summary>
    /// Identifies the <see cref="DefaultStyleKey"/> dependency property.
    /// Each control type overrides this property's metadata to set <c>typeof(Self)</c>,
    /// enabling implicit style lookup from resource dictionaries.
    /// </summary>
    public static readonly DependencyProperty DefaultStyleKeyProperty =
        DependencyProperty.Register(nameof(DefaultStyleKey), typeof(Type), typeof(FrameworkElement),
            new PropertyMetadata(null,
                propertyChangedCallback: (d, _) => ((FrameworkElement)d).InvalidateImplicitStyle()));

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

    /// <summary>Gets or sets the outer margin (space outside the control's bounds).</summary>
    public Thickness Margin
    {
        get => (Thickness)GetValue(MarginProperty)!;
        set => SetValue(MarginProperty, value);
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

    /// <summary>
    /// Gets or sets the key used to look up the default (implicit) style for this element.
    /// Controls override this property's metadata in their static constructor to set
    /// <c>typeof(Self)</c>, which causes the framework to automatically look up a
    /// <see cref="Style"/> resource keyed by that <see cref="Type"/> and apply it.
    /// </summary>
    protected Type? DefaultStyleKey
    {
        get => (Type?)GetValue(DefaultStyleKeyProperty);
        set => SetValue(DefaultStyleKeyProperty, value);
    }

    // ─── Implicit Style ──────────────────────────────────────────────

    /// <summary>
    /// The implicit (theme) style resolved from resources keyed by <see cref="DefaultStyleKey"/>.
    /// Applied at lower priority than the explicit <see cref="Style"/> property.
    /// </summary>
    private Style? _implicitStyle;

    // ─── Binding Support ──────────────────────────────────────────────

    /// <summary>
    /// Convenience method: attaches a binding to a dependency property on this element.
    /// Equivalent to <see cref="BindingOperations.SetBinding"/>.
    /// </summary>
    /// <param name="dp">The dependency property to bind.</param>
    /// <param name="binding">The binding description.</param>
    /// <returns>The created binding expression.</returns>
    public BindingExpressionBase SetBinding(DependencyProperty dp, BindingBase binding)
    {
        return BindingOperations.SetBinding(this, dp, binding);
    }

    /// <summary>
    /// Called when <see cref="DataContext"/> changes.
    /// Invalidates all DataContext-dependent binding expressions on this element,
    /// then recursively propagates to logical children that inherit DataContext.
    /// </summary>
    private void OnDataContextChanged(object? oldValue, object? newValue)
    {
        InvalidateDataContextBindings();

        // Propagate to logical children that don't have their own explicit DataContext
        foreach (var child in GetLogicalChildren())
        {
            if (child.DataContext == null)
            {
                child.OnInheritedDataContextChanged();
            }
        }
    }

    /// <summary>
    /// Called when an ancestor's DataContext changes and this element inherits DataContext
    /// (i.e., this element has no explicit DataContext set).
    /// Re-evaluates DataContext-dependent bindings and propagates to children.
    /// </summary>
    private void OnInheritedDataContextChanged()
    {
        InvalidateDataContextBindings();

        foreach (var child in GetLogicalChildren())
        {
            if (child.DataContext == null)
            {
                child.OnInheritedDataContextChanged();
            }
        }
    }

    /// <summary>
    /// Invalidates all binding expressions on this element that depend on DataContext
    /// (i.e., bindings without an explicit Source or RelativeSource).
    /// </summary>
    private void InvalidateDataContextBindings()
    {
        foreach (var (_, expr) in GetAllExpressions())
        {
            if (expr is BindingExpression { HasRelativeSource: false, HasExplicitSource: false } be)
            {
                be.Invalidate();
            }
        }
    }

    /// <summary>
    /// Called when this element's visual parent changes.
    /// Re-evaluates RelativeSource bindings, propagates inherited DataContext,
    /// and resolves implicit styles now that the element is in the tree.
    /// </summary>
    protected override void OnVisualParentChanged(Visual? oldParent)
    {
        base.OnVisualParentChanged(oldParent);

        // Re-evaluate RelativeSource bindings (FindAncestor depends on parent chain)
        InvalidateRelativeSourceBindings();

        // If this element inherits DataContext and the new parent has a different
        // effective DataContext, re-evaluate DataContext-dependent bindings too.
        if (DataContext == null)
        {
            InvalidateDataContextBindings();
        }

        // Resolve implicit style now that we are in the visual tree
        // (resources can be looked up via TryFindResource)
        InvalidateImplicitStyle();
    }

    /// <summary>
    /// Invalidates all binding expressions that use <c>RelativeSource</c> for source resolution.
    /// Called when the parent chain changes.
    /// </summary>
    private void InvalidateRelativeSourceBindings()
    {
        foreach (var (_, expr) in GetAllExpressions())
        {
            if (expr is BindingExpression { HasRelativeSource: true } be)
            {
                be.Invalidate();
            }
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
    internal bool HasResources => _resources is { Count: > 0 };
    
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
    public virtual object? TryFindResource(object key)
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
        // Reduce available area by margin (margin is space outside the control)
        var margin = Margin;
        var availableX = parent.X + margin.Left;
        var availableY = parent.Y + margin.Top;
        var availableW = Math.Max(0, parent.Width - margin.HorizontalTotal);
        var availableH = Math.Max(0, parent.Height - margin.VerticalTotal);

        // Clamp control size to available space
        w = Math.Min(w, availableW);
        h = Math.Min(h, availableH);

        var x = HorizontalAlignment switch
        {
            Alignment.Center => availableX + (availableW - w) / 2,
            Alignment.End => availableX + availableW - w,
            _ => availableX // Start
        };

        var y = VerticalAlignment switch
        {
            Alignment.Center => availableY + (availableH - h) / 2,
            Alignment.End => availableY + availableH - h,
            _ => availableY // Start
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
    /// Called when the explicit <see cref="Style"/> property changes.
    /// Re-applies the combined implicit + explicit style.
    /// </summary>
    private void OnStyleChanged()
    {
        ApplyEffectiveStyle();
    }

    /// <summary>
    /// Resolves the implicit style for this element from the resource chain.
    /// Called when the element enters the visual tree, or when <see cref="DefaultStyleKey"/> changes.
    /// </summary>
    internal void InvalidateImplicitStyle()
    {
        var key = DefaultStyleKey;
        if (key == null)
        {
            if (_implicitStyle != null)
            {
                _implicitStyle = null;
                ApplyEffectiveStyle();
            }
            return;
        }
        
        var resolved = TryFindResource(key) as Style;
        if (!ReferenceEquals(resolved, _implicitStyle))
        {
            _implicitStyle = resolved;
            ApplyEffectiveStyle();
        }
    }

    /// <summary>
    /// Applies the effective style to this element.
    /// The implicit style (keyed by <see cref="DefaultStyleKey"/>) is applied first (lowest priority),
    /// then the explicit <see cref="Style"/> property overrides on top.
    /// Walks <see cref="Style.BasedOn"/> chains depth-first: base-most setters are applied first.
    /// </summary>
    protected virtual void ApplyEffectiveStyle()
    {
        var controlType = GetType();

        // 1. Apply implicit (theme) style first — lowest priority
        if (_implicitStyle != null)
        {
            var chain = FlattenStyleChain(_implicitStyle);
            for (var i = chain.Count - 1; i >= 0; i--)
            {
                ApplySetters(chain[i], controlType);
            }
        }
        
        // 2. Apply explicit style on top — highest priority (overrides implicit)
        var explicitStyle = Style;
        if (explicitStyle != null)
        {
            var chain = FlattenStyleChain(explicitStyle);
            for (var i = chain.Count - 1; i >= 0; i--)
            {
                ApplySetters(chain[i], controlType);
            }
        }
    }
    
    /// <summary>
    /// Flattens a <see cref="Style.BasedOn"/> chain into a list ordered from derived to base.
    /// Throws on circular references or incompatible TargetType.
    /// </summary>
    private List<Style> FlattenStyleChain(Style style)
    {
        var chain = new List<Style>();
        var visited = new HashSet<Style>(ReferenceEqualityComparer.Instance);
        var current = style;
        
        while (current != null)
        {
            if (!visited.Add(current))
            {
                throw new InvalidOperationException(
                    "Circular Style.BasedOn reference detected");
            }
            
            // Validate TargetType compatibility
            if (current.TargetType != null && !current.TargetType.IsInstanceOfType(this))
            {
                throw new InvalidOperationException(
                    $"Style with TargetType '{current.TargetType.Name}' cannot be applied to control of type '{GetType().Name}'");
            }
            
            chain.Add(current);
            current = current.BasedOn;
        }
        
        return chain;
    }
    
    /// <summary>
    /// Applies all setters from a single <see cref="Style"/> to this element.
    /// </summary>
    private void ApplySetters(Style style, Type controlType)
    {
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
                else if (value is string stringValue)
                {
                    // Fallback: convert primitive types from string
                    // (mirrors XamlLoader.ConvertValue for direct attributes)
                    var pt = accessor.PropertyType;
                    if (pt == typeof(bool))
                        value = bool.Parse(stringValue);
                    else if (pt == typeof(int))
                        value = int.Parse(stringValue);
                    else if (pt == typeof(double))
                        value = double.Parse(stringValue);
                    else if (pt == typeof(float))
                        value = float.Parse(stringValue);
                    else if (pt == typeof(char) && stringValue.Length == 1)
                        value = stringValue[0];
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
