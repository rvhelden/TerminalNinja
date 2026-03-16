using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TerminalNinja.DependencySystem;

/// <summary>
/// Base class for all objects that support dependency properties.
/// Implements <see cref="INotifyPropertyChanged"/> so dependency property changes
/// automatically participate in data binding.
/// </summary>
public class DependencyObject : INotifyPropertyChanged
{
    private Dictionary<DependencyProperty, object?>? _localValues;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Returns the current effective value of the dependency property:
    /// the locally set value if present, otherwise the property's default value.
    /// </summary>
    public object? GetValue(DependencyProperty dp)
    {
        ArgumentNullException.ThrowIfNull(dp);

        if (_localValues?.TryGetValue(dp, out var local) == true)
            return local;

        return dp.DefaultMetadata.DefaultValue;
    }

    /// <summary>
    /// Sets the local value of a dependency property.
    /// Fires <see cref="PropertyChanged"/>, invokes <see cref="PropertyMetadata.PropertyChangedCallback"/>,
    /// and calls <see cref="OnPropertyAffectsRender"/> when <see cref="FrameworkPropertyMetadata.AffectsRender"/> is true.
    /// </summary>
    public void SetValue(DependencyProperty dp, object? value)
    {
        ArgumentNullException.ThrowIfNull(dp);

        var metadata = dp.DefaultMetadata;
        var oldValue = GetValue(dp);

        if (metadata.CoerceValueCallback != null)
            value = metadata.CoerceValueCallback(this, value);

        if (Equals(oldValue, value))
            return;

        _localValues ??= new Dictionary<DependencyProperty, object?>();
        _localValues[dp] = value;

        var args = new DependencyPropertyChangedEventArgs(dp, oldValue, value);

        metadata.PropertyChangedCallback?.Invoke(this, args);
        OnPropertyChanged(dp.Name);

        if (metadata is FrameworkPropertyMetadata { AffectsRender: true })
            OnPropertyAffectsRender(dp);
    }

    /// <summary>
    /// Clears the locally set value of a dependency property,
    /// reverting it to the registered default.
    /// </summary>
    public void ClearValue(DependencyProperty dp)
    {
        ArgumentNullException.ThrowIfNull(dp);

        if (_localValues == null || !_localValues.ContainsKey(dp))
            return;

        var oldValue = GetValue(dp);
        _localValues.Remove(dp);
        var newValue = GetValue(dp); // default value

        if (Equals(oldValue, newValue))
            return;

        var args = new DependencyPropertyChangedEventArgs(dp, oldValue, newValue);
        dp.DefaultMetadata.PropertyChangedCallback?.Invoke(this, args);
        OnPropertyChanged(dp.Name);

        if (dp.DefaultMetadata is FrameworkPropertyMetadata { AffectsRender: true })
            OnPropertyAffectsRender(dp);
    }

    /// <summary>
    /// Called by <see cref="SetValue"/> and <see cref="ClearValue"/> when the changed property
    /// has <see cref="FrameworkPropertyMetadata.AffectsRender"/> set to <c>true</c>.
    /// Override in subclasses to trigger visual invalidation.
    /// </summary>
    protected virtual void OnPropertyAffectsRender(DependencyProperty dp) { }

    /// <summary>
    /// Raises <see cref="PropertyChanged"/>. Also called by <see cref="SetValue"/>.
    /// Supports zero-argument calls from property setters via <see cref="CallerMemberNameAttribute"/>.
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
