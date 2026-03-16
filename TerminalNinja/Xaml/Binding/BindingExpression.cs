using System.ComponentModel;
using TerminalNinja.Aot;
using TerminalNinja.Xaml.Data;

namespace TerminalNinja.Xaml.Binding;

/// <summary>
/// Represents an active binding between a source property and a target property.
/// Handles synchronization, type conversion, and change notifications.
/// </summary>
internal sealed class BindingExpression : IDisposable
{
    private readonly object _target;
    private readonly string _targetPropertyName;
    private readonly PropertyAccessor _targetAccessor;
    private readonly PropertyPath _sourcePath;
    private readonly BindingMode _mode;
    private readonly IValueConverter? _converter;
    private readonly object? _converterParameter;
    
    private PropertyPathObserver? _observer;
    private object? _source;
    private bool _isUpdating;
    
    public BindingExpression(
        object target,
        string targetPropertyName,
        PropertyAccessor targetAccessor,
        PropertyPath sourcePath,
        BindingMode mode,
        IValueConverter? converter = null,
        object? converterParameter = null)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPropertyName);
        _targetPropertyName = targetPropertyName;
        _targetAccessor = targetAccessor;
        _sourcePath = sourcePath ?? throw new ArgumentNullException(nameof(sourcePath));
        _mode = mode;
        _converter = converter;
        _converterParameter = converterParameter;
    }
    
    /// <summary>
    /// Gets the target object this binding is attached to.
    /// </summary>
    public object Target => _target;
    
    /// <summary>
    /// Gets the target property name this binding updates.
    /// </summary>
    public string TargetPropertyName => _targetPropertyName;
    
    /// <summary>
    /// Activates the binding with the specified source object.
    /// </summary>
    public void Activate(object? source)
    {
        _source = source;
        
        // For OneWay and TwoWay modes, subscribe to source changes
        if (_mode != BindingMode.OneTime)
        {
            _observer = new PropertyPathObserver(_sourcePath, _source, OnSourceChanged);
        }
        
        // Initial update from source to target
        UpdateTarget();
        
        // For TwoWay mode, also subscribe to target changes
        if (_mode == BindingMode.TwoWay)
        {
            SubscribeToTargetChanges();
        }
    }
    
    /// <summary>
    /// Updates the target property from the source.
    /// </summary>
    private void UpdateTarget()
    {
        if (_isUpdating) return; // Prevent recursion
        
        try
        {
            _isUpdating = true;
            
            var sourceValue = _sourcePath.GetValue(_source);
            var convertedValue = ConvertValue(sourceValue, _targetAccessor.PropertyType, forward: true);
            
            if (_targetAccessor.CanWrite)
            {
                _targetAccessor.Setter!(_target, convertedValue);
            }
        }
        catch (Exception ex)
        {
            // In production, you might want to log this or handle it differently
            System.Diagnostics.Debug.WriteLine($"Binding update failed: {ex.Message}");
        }
        finally
        {
            _isUpdating = false;
        }
    }
    
    /// <summary>
    /// Updates the source property from the target (TwoWay only).
    /// </summary>
    private void UpdateSource()
    {
        if (_isUpdating) return; // Prevent recursion
        if (_mode != BindingMode.TwoWay) return;
        
        try
        {
            _isUpdating = true;
            
            var targetValue = _targetAccessor.Getter(_target);
            var convertedValue = ConvertValue(targetValue, _targetAccessor.PropertyType, forward: false);
            
            _sourcePath.SetValue(_source, convertedValue);
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
    
    /// <summary>
    /// Converts a value using the converter if available, otherwise uses default conversion.
    /// </summary>
    private object? ConvertValue(object? value, Type targetType, bool forward)
    {
        // Use converter if provided
        if (_converter != null)
        {
            return forward
                ? _converter.Convert(value, targetType, _converterParameter)
                : _converter.ConvertBack(value, targetType, _converterParameter);
        }
        
        // Default conversion
        return ConvertValueDefault(value, targetType);
    }
    
    /// <summary>
    /// Default type conversion logic.
    /// </summary>
    private static object? ConvertValueDefault(object? value, Type targetType)
    {
        if (value == null)
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        
        var valueType = value.GetType();
        
        // No conversion needed
        if (targetType.IsAssignableFrom(valueType))
            return value;
        
        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType);
        if (underlyingType != null)
            targetType = underlyingType;
        
        // Try Convert.ChangeType
        try
        {
            return Convert.ChangeType(value, targetType);
        }
        catch
        {
            // Conversion failed - return default value
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }
    }
    
    /// <summary>
    /// Called when the source property changes.
    /// </summary>
    private void OnSourceChanged()
    {
        UpdateTarget();
    }
    
    /// <summary>
    /// Subscribes to target property changes for TwoWay binding.
    /// </summary>
    private void SubscribeToTargetChanges()
    {
        if (_target is INotifyPropertyChanged inpc)
        {
            inpc.PropertyChanged += OnTargetPropertyChanged;
        }
    }
    
    /// <summary>
    /// Unsubscribes from target property changes.
    /// </summary>
    private void UnsubscribeFromTargetChanges()
    {
        if (_target is INotifyPropertyChanged inpc)
        {
            inpc.PropertyChanged -= OnTargetPropertyChanged;
        }
    }
    
    /// <summary>
    /// Called when a property on the target object changes.
    /// </summary>
    private void OnTargetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == _targetPropertyName || string.IsNullOrEmpty(e.PropertyName))
        {
            UpdateSource();
        }
    }
    
    public void Dispose()
    {
        _observer?.Dispose();
        UnsubscribeFromTargetChanges();
    }
}
