namespace TerminalNinja.DependencySystem;

/// <summary>
/// Base class for all dependency property expressions.
/// An expression provides a dynamic value for a dependency property
/// instead of a static local value.
/// Mirrors WPF's <c>System.Windows.Expression</c>.
/// </summary>
public abstract class Expression
{
    /// <summary>
    /// Gets the <see cref="DependencyObject"/> this expression is attached to,
    /// or <c>null</c> if the expression has not been attached yet.
    /// </summary>
    internal DependencyObject? Target { get; private set; }

    /// <summary>
    /// Gets the <see cref="DependencyProperty"/> this expression provides a value for,
    /// or <c>null</c> if the expression has not been attached yet.
    /// </summary>
    internal DependencyProperty? TargetProperty { get; private set; }

    /// <summary>
    /// Gets whether this expression is currently attached to a dependency property.
    /// </summary>
    public bool IsAttached => Target != null && TargetProperty != null;

    /// <summary>
    /// Called when the expression is attached to a dependency property on a target object.
    /// Subclasses should begin producing values (e.g., subscribe to source changes).
    /// </summary>
    /// <param name="d">The target object.</param>
    /// <param name="dp">The target dependency property.</param>
    internal void Attach(DependencyObject d, DependencyProperty dp)
    {
        ArgumentNullException.ThrowIfNull(d);
        ArgumentNullException.ThrowIfNull(dp);

        Target = d;
        TargetProperty = dp;
        OnAttach(d, dp);
    }

    /// <summary>
    /// Called when the expression is detached from its dependency property.
    /// Subclasses should stop producing values (e.g., unsubscribe from source changes).
    /// </summary>
    internal void Detach()
    {
        if (Target != null && TargetProperty != null)
        {
            OnDetach(Target, TargetProperty);
        }

        Target = null;
        TargetProperty = null;
    }

    /// <summary>
    /// Returns the current value produced by this expression.
    /// </summary>
    /// <param name="d">The target object.</param>
    /// <param name="dp">The target dependency property.</param>
    /// <returns>The current effective value.</returns>
    internal abstract object? GetValue(DependencyObject d, DependencyProperty dp);

    /// <summary>
    /// Called by the property system when a local value is written via
    /// <see cref="DependencyObject.SetValueInternal"/> while this expression is attached.
    /// Allows the expression to keep its cached value in sync with the local store
    /// so that <see cref="GetValue"/> returns the latest value.
    /// </summary>
    internal virtual void OnLocalValueWritten(object? value) { }

    /// <summary>
    /// Override to perform initialization when the expression is attached
    /// to a dependency property on a target object.
    /// </summary>
    protected virtual void OnAttach(DependencyObject d, DependencyProperty dp) { }

    /// <summary>
    /// Override to perform cleanup when the expression is detached.
    /// </summary>
    protected virtual void OnDetach(DependencyObject d, DependencyProperty dp) { }
}
