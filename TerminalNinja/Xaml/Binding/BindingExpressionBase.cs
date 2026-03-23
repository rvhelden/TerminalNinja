namespace TerminalNinja.Xaml.Binding;

/// <summary>
/// Abstract base class for all binding expressions.
/// A binding expression is the runtime representation of a <see cref="BindingBase"/>
/// attached to a specific <see cref="DependencyObject"/>/<see cref="DependencyProperty"/> pair.
/// Extends <see cref="Expression"/> to participate in the DependencyProperty value system.
/// Mirrors WPF's <c>System.Windows.Data.BindingExpressionBase</c>.
/// </summary>
public abstract class BindingExpressionBase : Expression
{
    /// <summary>
    /// Gets the binding description that created this expression.
    /// </summary>
    public BindingBase ParentBindingBase { get; }

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="binding">The binding description.</param>
    protected BindingExpressionBase(BindingBase binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ParentBindingBase = binding;
    }

    /// <summary>
    /// Called when the data context or source environment changes.
    /// Subclasses should re-evaluate their source and update the target value.
    /// </summary>
    internal abstract void Invalidate();
}
