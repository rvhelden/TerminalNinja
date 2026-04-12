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
    private Dictionary<DependencyProperty, Expression>? _expressions;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Returns the current effective value of the dependency property.
    /// Priority order: expression value → local value → default value.
    /// </summary>
    public object? GetValue(DependencyProperty dp)
    {
        ArgumentNullException.ThrowIfNull(dp);

        // Expression takes priority — ask it for the current value
        if (_expressions?.TryGetValue(dp, out var expr) == true)
        {
            return expr.GetValue(this, dp);
        }

        if (_localValues?.TryGetValue(dp, out var local) == true)
        {
            return local;
        }

        return dp.DefaultMetadata.DefaultValue;
    }

    /// <summary>
    /// Sets the local value of a dependency property.
    /// If an expression is currently attached to this property, it is detached first
    /// (setting a local value overrides an expression — same as WPF).
    /// Fires <see cref="PropertyChanged"/>, invokes <see cref="PropertyMetadata.PropertyChangedCallback"/>,
    /// and calls <see cref="OnPropertyAffectsRender"/> when <see cref="FrameworkPropertyMetadata.AffectsRender"/> is true.
    /// </summary>
    public void SetValue(DependencyProperty dp, object? value)
    {
        ArgumentNullException.ThrowIfNull(dp);

        // Setting a local value clears any active expression (WPF behavior)
        if (_expressions?.ContainsKey(dp) == true)
        {
            ClearExpression(dp);
        }

        SetValueCore(dp, value);
    }

    /// <summary>
    /// Sets the value of a dependency property without clearing any active expression.
    /// Used by <see cref="Expression"/> subclasses to push their computed value
    /// into the property system while keeping the expression attached.
    /// </summary>
    internal void SetValueInternal(DependencyProperty dp, object? value)
    {
        ArgumentNullException.ThrowIfNull(dp);
        SetValueCore(dp, value);
    }

    /// <summary>
    /// Core value-setting logic shared by <see cref="SetValue"/> and <see cref="SetValueInternal"/>.
    /// </summary>
    private void SetValueCore(DependencyProperty dp, object? value)
    {
        var metadata = dp.DefaultMetadata;
        var oldValue = GetValueWithoutExpression(dp);

        if (metadata.CoerceValueCallback != null)
        {
            value = metadata.CoerceValueCallback(this, value);
        }

        if (Equals(oldValue, value))
        {
            return;
        }

        _localValues ??= new Dictionary<DependencyProperty, object?>();
        _localValues[dp] = value;

        // Keep the expression's cached value in sync so GetValue() returns the latest value
        if (_expressions?.TryGetValue(dp, out var activeExpr) == true)
        {
            activeExpr.OnLocalValueWritten(value);
        }

        var args = new DependencyPropertyChangedEventArgs(dp, oldValue, value);

        metadata.PropertyChangedCallback?.Invoke(this, args);
        OnPropertyChanged(dp.Name);

        if (metadata is FrameworkPropertyMetadata { AffectsRender: true })
        {
            OnPropertyAffectsRender(dp);
        }
    }

    /// <summary>
    /// Returns the local value (or default), bypassing any active expression.
    /// Used internally for old-value comparison during <see cref="SetValueCore"/>.
    /// </summary>
    private object? GetValueWithoutExpression(DependencyProperty dp)
    {
        if (_localValues?.TryGetValue(dp, out var local) == true)
        {
            return local;
        }

        return dp.DefaultMetadata.DefaultValue;
    }

    /// <summary>
    /// Returns the locally stored value for a dependency property, bypassing any active
    /// expression. Used by TwoWay binding to read the value a control set via
    /// <see cref="SetValueInternal"/> without getting the expression's stale cached value.
    /// </summary>
    internal object? GetLocalOrDefaultValue(DependencyProperty dp)
    {
        if (_localValues?.TryGetValue(dp, out var local) == true)
        {
            return local;
        }

        return dp.DefaultMetadata.DefaultValue;
    }

    /// <summary>
    /// Clears the locally set value of a dependency property,
    /// reverting it to the registered default.
    /// Also detaches any active expression on this property.
    /// </summary>
    public void ClearValue(DependencyProperty dp)
    {
        ArgumentNullException.ThrowIfNull(dp);

        // Clear expression if present
        if (_expressions?.ContainsKey(dp) == true)
        {
            ClearExpression(dp);
        }

        if (_localValues == null || !_localValues.ContainsKey(dp))
        {
            return;
        }

        var oldValue = GetValueWithoutExpression(dp);
        _localValues.Remove(dp);
        var newValue = dp.DefaultMetadata.DefaultValue;

        if (Equals(oldValue, newValue))
        {
            return;
        }

        var args = new DependencyPropertyChangedEventArgs(dp, oldValue, newValue);
        dp.DefaultMetadata.PropertyChangedCallback?.Invoke(this, args);
        OnPropertyChanged(dp.Name);

        if (dp.DefaultMetadata is FrameworkPropertyMetadata { AffectsRender: true })
        {
            OnPropertyAffectsRender(dp);
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Expression management
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Attaches an <see cref="Expression"/> to a dependency property.
    /// Any previously attached expression is detached first.
    /// </summary>
    /// <param name="dp">The dependency property.</param>
    /// <param name="expression">The expression to attach.</param>
    internal void SetExpression(DependencyProperty dp, Expression expression)
    {
        ArgumentNullException.ThrowIfNull(dp);
        ArgumentNullException.ThrowIfNull(expression);

        // Detach existing expression if any
        if (_expressions?.TryGetValue(dp, out var existing) == true)
        {
            existing.Detach();
        }

        _expressions ??= new Dictionary<DependencyProperty, Expression>();
        _expressions[dp] = expression;

        // Attach the new expression
        expression.Attach(this, dp);
    }

    /// <summary>
    /// Gets the <see cref="Expression"/> currently attached to a dependency property,
    /// or <c>null</c> if no expression is set.
    /// </summary>
    /// <param name="dp">The dependency property.</param>
    /// <returns>The attached expression, or <c>null</c>.</returns>
    internal Expression? GetExpression(DependencyProperty dp)
    {
        ArgumentNullException.ThrowIfNull(dp);

        if (_expressions?.TryGetValue(dp, out var expr) == true)
        {
            return expr;
        }

        return null;
    }

    /// <summary>
    /// Detaches and removes the <see cref="Expression"/> from a dependency property.
    /// </summary>
    /// <param name="dp">The dependency property.</param>
    internal void ClearExpression(DependencyProperty dp)
    {
        ArgumentNullException.ThrowIfNull(dp);

        if (_expressions == null)
        {
            return;
        }

        if (_expressions.TryGetValue(dp, out var expr))
        {
            expr.Detach();
            _expressions.Remove(dp);
        }
    }

    /// <summary>
    /// Returns all expressions currently attached to this object.
    /// Used for iterating expressions when DataContext or parent changes.
    /// </summary>
    internal IEnumerable<KeyValuePair<DependencyProperty, Expression>> GetAllExpressions()
    {
        if (_expressions == null || _expressions.Count == 0)
        {
            return [];
        }

        // Return a snapshot to allow safe iteration during modification
        return _expressions.ToArray();
    }

    // ────────────────────────────────────────────────────────────────
    //  Infrastructure
    // ────────────────────────────────────────────────────────────────

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
