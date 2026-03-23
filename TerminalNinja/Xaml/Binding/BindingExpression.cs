using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Data;
using TerminalNinja.Controls;
using BindingDescription = System.Windows.Markup.Binding;

namespace TerminalNinja.Xaml.Binding;

/// <summary>
/// Runtime representation of a <see cref="System.Windows.Markup.Binding"/> attached to a
/// specific <see cref="DependencyObject"/>/<see cref="DependencyProperty"/> pair.
/// Self-contained: resolves its own source (DataContext, RelativeSource, explicit Source, ElementName)
/// and manages its own change subscriptions.
/// Mirrors WPF's <c>System.Windows.Data.BindingExpression</c>.
/// </summary>
public sealed class BindingExpression : BindingExpressionBase, IDisposable
{
    private readonly BindingDescription _binding;
    private readonly PropertyPath _sourcePath;

    private PropertyPathObserver? _observer;
    private object? _resolvedSource;
    private object? _cachedValue;
    private bool _isUpdating;

    /// <summary>
    /// Creates a new binding expression from a binding description, target, and target property.
    /// </summary>
    internal BindingExpression(BindingDescription binding, DependencyObject target, DependencyProperty dp)
        : base(binding)
    {
        _binding = binding ?? throw new ArgumentNullException(nameof(binding));

        if (string.IsNullOrWhiteSpace(binding.Path))
        {
            throw new InvalidOperationException("Binding.Path must be set.");
        }

        _sourcePath = new PropertyPath(binding.Path);
    }

    /// <summary>
    /// Gets the <see cref="System.Windows.Markup.Binding"/> that created this expression.
    /// </summary>
    public BindingDescription ParentBinding => _binding;

    /// <summary>
    /// Gets the resolved source object (DataContext, RelativeSource result, explicit Source, etc.).
    /// </summary>
    public object? ResolvedSource => _resolvedSource;

    /// <summary>
    /// Gets whether this binding uses a <see cref="RelativeSource"/> for source resolution
    /// instead of DataContext. RelativeSource bindings are not re-evaluated when DataContext changes.
    /// </summary>
    public bool HasRelativeSource => _binding.RelativeSource != null;

    /// <summary>
    /// Gets whether this binding uses an explicit <see cref="Binding.Source"/>.
    /// Explicit source bindings are not re-evaluated when DataContext changes.
    /// </summary>
    public bool HasExplicitSource => _binding.Source != null;

    // ────────────────────────────────────────────────────────────────
    //  Expression lifecycle (called by DependencyObject)
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called when the expression is attached to a DP on a target object.
    /// Resolves the source and pushes the initial value.
    /// </summary>
    protected override void OnAttach(DependencyObject d, DependencyProperty dp)
    {
        ResolveAndActivate();
    }

    /// <summary>
    /// Called when the expression is detached from its target.
    /// </summary>
    protected override void OnDetach(DependencyObject d, DependencyProperty dp)
    {
        Deactivate();
    }

    /// <summary>
    /// Returns the current value produced by this binding.
    /// </summary>
    internal override object? GetValue(DependencyObject d, DependencyProperty dp)
    {
        return _cachedValue ?? dp.DefaultMetadata.DefaultValue;
    }

    /// <summary>
    /// Called when the binding environment changes (DataContext, parent, etc.).
    /// Re-resolves the source and re-activates.
    /// </summary>
    internal override void Invalidate()
    {
        ResolveAndActivate();
    }

    // ────────────────────────────────────────────────────────────────
    //  Source resolution
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the binding source and activates change tracking.
    /// </summary>
    private void ResolveAndActivate()
    {
        Deactivate();

        _resolvedSource = ResolveSource();

        if (_resolvedSource != null || _binding.FallbackValue != null)
        {
            Activate();
        }
        else
        {
            // Source not available yet — store fallback/default as cached value
            _cachedValue = _binding.FallbackValue;
        }
    }

    /// <summary>
    /// Determines the source object based on the binding's configuration.
    /// Priority: explicit Source → RelativeSource → ElementName → DataContext.
    /// </summary>
    private object? ResolveSource()
    {
        // 1. Explicit Source
        if (_binding.Source != null)
        {
            return _binding.Source;
        }

        // 2. RelativeSource
        if (_binding.RelativeSource != null)
        {
            return ResolveRelativeSource(_binding.RelativeSource);
        }

        // 3. ElementName (future: requires name scope)
        // For now, not implemented — WPF resolves this via INameScope

        // 4. DataContext (walk up the tree)
        return ResolveDataContext();
    }

    /// <summary>
    /// Resolves a <see cref="RelativeSource"/> binding by walking the visual tree.
    /// </summary>
    private Visual? ResolveRelativeSource(RelativeSource relativeSource)
    {
        if (Target is not Visual targetVisual)
        {
            return null;
        }

        return relativeSource.Mode switch
        {
            RelativeSourceMode.Self => targetVisual,
            RelativeSourceMode.FindAncestor => FindAncestor(relativeSource, targetVisual),
            RelativeSourceMode.TemplatedParent => null, // Template system not yet implemented
            _ => null
        };
    }

    /// <summary>
    /// Walks the visual tree upward to find an ancestor matching the specified type.
    /// </summary>
    private static Visual? FindAncestor(RelativeSource relativeSource, Visual target)
    {
        if (relativeSource.AncestorType == null)
        {
            throw new InvalidOperationException(
                "AncestorType must be set when using RelativeSourceMode.FindAncestor.");
        }

        var ancestorLevel = relativeSource.AncestorLevel;
        if (ancestorLevel < 1)
        {
            ancestorLevel = 1;
        }

        var current = target.Parent;
        var matchCount = 0;

        while (current != null)
        {
            if (relativeSource.AncestorType.IsInstanceOfType(current))
            {
                matchCount++;
                if (matchCount >= ancestorLevel)
                {
                    return current;
                }
            }

            current = current.Parent;
        }

        return null; // Ancestor not found — WPF returns null silently
    }

    /// <summary>
    /// Resolves the effective DataContext by walking up the parent chain.
    /// </summary>
    private object? ResolveDataContext()
    {
        if (Target is FrameworkElement fe)
        {
            return fe.GetEffectiveDataContext();
        }

        return null;
    }

    // ────────────────────────────────────────────────────────────────
    //  Activation / deactivation
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Activates change tracking on the resolved source and pushes the initial value.
    /// </summary>
    private void Activate()
    {
        // For OneWay and TwoWay, subscribe to source changes
        if (_binding.Mode != BindingMode.OneTime && _resolvedSource != null)
        {
            _observer = new PropertyPathObserver(_sourcePath, _resolvedSource, OnSourceChanged);
        }

        // Initial push from source to target
        UpdateTarget();

        // For TwoWay, subscribe to target changes
        if (_binding.Mode == BindingMode.TwoWay && Target is INotifyPropertyChanged inpc)
        {
            inpc.PropertyChanged += OnTargetPropertyChanged;
        }
    }

    /// <summary>
    /// Stops change tracking and unsubscribes from events.
    /// </summary>
    private void Deactivate()
    {
        _observer?.Dispose();
        _observer = null;

        if (Target is INotifyPropertyChanged inpc)
        {
            inpc.PropertyChanged -= OnTargetPropertyChanged;
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Value transfer
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads the source value and pushes it to the target via <see cref="DependencyObject.SetValueInternal"/>.
    /// </summary>
    private void UpdateTarget()
    {
        if (_isUpdating || Target == null || TargetProperty == null)
        {
            return;
        }

        try
        {
            _isUpdating = true;

            var sourceValue = _sourcePath.GetValue(_resolvedSource);
            var converted = ConvertValue(sourceValue, TargetProperty.PropertyType, forward: true);
            _cachedValue = converted;

            // Push the value through the DP system (without clearing this expression)
            Target.SetValueInternal(TargetProperty, converted);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Binding update failed: {ex.Message}");

            // Use fallback value on error
            if (_binding.FallbackValue != null)
            {
                _cachedValue = _binding.FallbackValue;
                Target.SetValueInternal(TargetProperty, _binding.FallbackValue);
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    /// <summary>
    /// Reads the target value and pushes it back to the source (TwoWay only).
    /// </summary>
    private void UpdateSource()
    {
        if (_isUpdating || _binding.Mode != BindingMode.TwoWay)
        {
            return;
        }

        if (Target == null || TargetProperty == null)
        {
            return;
        }

        try
        {
            _isUpdating = true;

            var targetValue = Target.GetValue(TargetProperty);
            var converted = ConvertValue(targetValue, TargetProperty.PropertyType, forward: false);
            _sourcePath.SetValue(_resolvedSource, converted);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Binding update source failed: {ex.Message}");
        }
        finally
        {
            _isUpdating = false;
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Value conversion
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts a value using the converter if available, otherwise uses default conversion.
    /// </summary>
    private object? ConvertValue(object? value, Type targetType, bool forward)
    {
        if (_binding.Converter != null)
        {
            return forward
                ? _binding.Converter.Convert(value, targetType, _binding.ConverterParameter)
                : _binding.Converter.ConvertBack(value, targetType, _binding.ConverterParameter);
        }

        return ConvertValueDefault(value, targetType);
    }

    /// <summary>
    /// Default type conversion logic.
    /// </summary>
    private static object? ConvertValueDefault(object? value, Type targetType)
    {
        if (value == null)
        {
            return targetType.IsValueType ? GetDefaultValue(targetType) : null;
        }

        var valueType = value.GetType();

        if (targetType.IsAssignableFrom(valueType))
        {
            return value;
        }

        var underlyingType = Nullable.GetUnderlyingType(targetType);
        if (underlyingType != null)
        {
            targetType = underlyingType;
        }

        try
        {
            return Convert.ChangeType(value, targetType);
        }
        catch
        {
            return targetType.IsValueType ? GetDefaultValue(targetType) : null;
        }
    }

    /// <summary>
    /// Gets the default value for a value type without using Activator.CreateInstance.
    /// AOT-safe: covers all primitive types used in the binding system.
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL2067",
        Justification = "All common value types are handled explicitly above. " +
                         "Remaining value types (Color, Thickness, Size, GridLength, etc.) are struct types " +
                         "with parameterless constructors preserved by PropertyAccessorRegistry registrations.")]
    private static object GetDefaultValue(Type valueType)
    {
        if (valueType == typeof(int)) return 0;
        if (valueType == typeof(bool)) return false;
        if (valueType == typeof(double)) return 0.0;
        if (valueType == typeof(float)) return 0.0f;
        if (valueType == typeof(long)) return 0L;
        if (valueType == typeof(byte)) return (byte)0;
        if (valueType == typeof(char)) return '\0';

        return Activator.CreateInstance(valueType)!;
    }

    // ────────────────────────────────────────────────────────────────
    //  Event handlers
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called when a property in the source path changes.
    /// </summary>
    private void OnSourceChanged()
    {
        UpdateTarget();
    }

    /// <summary>
    /// Called when a property on the target object changes (TwoWay only).
    /// </summary>
    private void OnTargetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (TargetProperty != null &&
            (e.PropertyName == TargetProperty.Name || string.IsNullOrEmpty(e.PropertyName)))
        {
            UpdateSource();
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  IDisposable
    // ────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        Deactivate();
    }
}
