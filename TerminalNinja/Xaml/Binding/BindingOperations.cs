namespace TerminalNinja.Xaml.Binding;

/// <summary>
/// Provides static methods for creating and managing data bindings on dependency objects.
/// This is the primary entry point for programmatically establishing bindings.
/// Mirrors WPF's <c>System.Windows.Data.BindingOperations</c>.
/// </summary>
public static class BindingOperations
{
    /// <summary>
    /// Creates a <see cref="BindingExpressionBase"/> from the specified <see cref="BindingBase"/>
    /// and attaches it to the specified <see cref="DependencyProperty"/> on the target object.
    /// If the property already has a binding expression, it is detached first.
    /// </summary>
    /// <param name="target">The dependency object that owns the property.</param>
    /// <param name="dp">The dependency property to bind.</param>
    /// <param name="binding">The binding description.</param>
    /// <returns>The created and attached binding expression.</returns>
    public static BindingExpressionBase SetBinding(
        DependencyObject target, DependencyProperty dp, BindingBase binding)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(dp);
        ArgumentNullException.ThrowIfNull(binding);

        // Create the expression from the binding description
        var expression = binding.CreateBindingExpression(target, dp);

        // Attach via the DependencyObject expression system —
        // this calls Expression.Attach() → OnAttach() → ResolveAndActivate()
        target.SetExpression(dp, expression);

        return expression;
    }

    /// <summary>
    /// Returns the <see cref="BindingExpressionBase"/> currently attached to the specified
    /// dependency property, or <c>null</c> if no binding is set.
    /// </summary>
    /// <param name="target">The dependency object to inspect.</param>
    /// <param name="dp">The dependency property to query.</param>
    /// <returns>The binding expression, or <c>null</c>.</returns>
    public static BindingExpressionBase? GetBindingExpression(
        DependencyObject target, DependencyProperty dp)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(dp);

        return target.GetExpression(dp) as BindingExpressionBase;
    }

    /// <summary>
    /// Returns the <see cref="BindingBase"/> description currently active on the specified
    /// dependency property, or <c>null</c> if no binding is set.
    /// </summary>
    /// <param name="target">The dependency object to inspect.</param>
    /// <param name="dp">The dependency property to query.</param>
    /// <returns>The binding description, or <c>null</c>.</returns>
    public static BindingBase? GetBinding(DependencyObject target, DependencyProperty dp)
    {
        return GetBindingExpression(target, dp)?.ParentBindingBase;
    }

    /// <summary>
    /// Removes the binding (if any) from the specified dependency property,
    /// restoring the local value or default.
    /// </summary>
    /// <param name="target">The dependency object.</param>
    /// <param name="dp">The dependency property to unbind.</param>
    public static void ClearBinding(DependencyObject target, DependencyProperty dp)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(dp);

        target.ClearExpression(dp);
    }

    /// <summary>
    /// Removes all bindings from the specified dependency object.
    /// </summary>
    /// <param name="target">The dependency object.</param>
    public static void ClearAllBindings(DependencyObject target)
    {
        ArgumentNullException.ThrowIfNull(target);

        // Snapshot the expressions to avoid modification during iteration
        foreach (var (dp, _) in target.GetAllExpressions())
        {
            target.ClearExpression(dp);
        }
    }

    /// <summary>
    /// Returns whether the specified dependency property currently has a binding expression attached.
    /// </summary>
    /// <param name="target">The dependency object.</param>
    /// <param name="dp">The dependency property.</param>
    /// <returns><c>true</c> if a binding expression is attached; otherwise <c>false</c>.</returns>
    public static bool IsDataBound(DependencyObject target, DependencyProperty dp)
    {
        return GetBindingExpression(target, dp) != null;
    }
}
