using TerminalNinja.Xaml.Binding;
using TerminalNinja.Xaml.Data;

// ReSharper disable once CheckNamespace
namespace System.Windows.Markup;

public sealed class Binding : BindingBase
{
    public Binding() { }
    public Binding(string path) => Path = path;

    /// <summary>
    /// The source property path to bind to.
    /// </summary>
    [ConstructorArgument("path")]
    public string? Path { get; set; }

    public UpdateSourceTrigger UpdateSourceTrigger { get; set; } = UpdateSourceTrigger.Default;
    
    /// <summary> object to use as the source </summary>
    /// <remarks> To clear this property, set it to DependencyProperty.UnsetValue. </remarks>
    public object? Source { get; set; }
    
    /// <summary>
    /// The binding mode (Default, OneWay, TwoWay, OneTime). When left at
    /// <see cref="BindingMode.Default"/>, the effective mode is taken from the target property's
    /// metadata — <see cref="BindingMode.TwoWay"/> for properties registered with
    /// <see cref="TerminalNinja.DependencySystem.FrameworkPropertyMetadata.BindsTwoWayByDefault"/>,
    /// otherwise <see cref="BindingMode.OneWay"/>.
    /// </summary>
    public BindingMode Mode { get; set; } = BindingMode.Default;

    /// <summary>
    /// An optional <see cref="IValueConverter"/> to apply during binding.
    /// </summary>
    public IValueConverter? Converter { get; set; }

    /// <summary>
    /// An optional parameter to pass to the <see cref="Converter"/>.
    /// </summary>
    public object? ConverterParameter { get; set; }

    /// <summary>
    /// An optional <see cref="Xaml.Binding.RelativeSource"/> that describes the location of the binding source
    /// relative to the binding target. When set, the binding source is determined by the
    /// <see cref="Xaml.Binding.RelativeSource"/> instead of DataContext.
    /// </summary>
    public RelativeSource? RelativeSource { get; set; }

    /// <summary>
    /// Gets or sets the name of the element to use as the binding source.
    /// Mutually exclusive with <see cref="Source"/> and <see cref="RelativeSource"/>.
    /// </summary>
    public string? ElementName { get; set; }

    /// <summary>
    /// Creates a <see cref="BindingExpression"/> for this binding on the specified target.
    /// </summary>
    internal override BindingExpressionBase CreateBindingExpression(
        DependencyObject target, DependencyProperty dp)
    {
        return new BindingExpression(this, target, dp);
    }
}

public enum UpdateSourceTrigger
{
    Default = 0,
    Explicit = 3,
    LostFocus = 2,
    PropertyChanged = 1,
}
