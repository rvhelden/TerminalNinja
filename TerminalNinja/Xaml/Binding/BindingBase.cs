using System.Windows.Markup;

namespace TerminalNinja.Xaml.Binding;

/// <summary>
/// Abstract base class for binding descriptions.
/// Mirrors WPF's <c>System.Windows.Data.BindingBase</c>.
/// Provides a common base for future extensibility (e.g., MultiBinding).
/// </summary>
public abstract class BindingBase : MarkupExtension
{
    /// <summary>
    /// Gets or sets the value to use when the binding cannot produce a value.
    /// </summary>
    public object? FallbackValue { get; set; }

    /// <summary>
    /// Gets or sets a format string applied to the binding value.
    /// </summary>
    public string? StringFormat { get; set; }

    /// <summary>
    /// Gets or sets the value to use when the source value is <c>null</c>.
    /// </summary>
    public object? TargetNullValue { get; set; }

    /// <summary>
    /// Creates a <see cref="BindingExpressionBase"/> for this binding on the specified target.
    /// </summary>
    /// <param name="target">The dependency object.</param>
    /// <param name="dp">The dependency property to bind.</param>
    /// <returns>A binding expression that can be attached to the target.</returns>
    internal abstract BindingExpressionBase CreateBindingExpression(
        DependencyObject target, DependencyProperty dp);
}
