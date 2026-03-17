using TerminalNinja.Markup;
using TerminalNinja.Xaml.Data;

namespace TerminalNinja.Xaml.Binding;

/// <summary>
/// Design-time markup extension stub for <c>{Binding}</c> syntax in XAML.
/// Provides IDE IntelliSense support in Rider/Visual Studio.
/// At runtime, <c>{Binding}</c> expressions are parsed by <see cref="XamlLoader"/>
/// via string manipulation — this class's <see cref="ProvideValue"/> is never called.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class BindingExtension : MarkupExtension
{
    /// <summary>
    /// Creates a new <see cref="BindingExtension"/> with no initial path.
    /// </summary>
    public BindingExtension() { }

    /// <summary>
    /// Creates a new <see cref="BindingExtension"/> with the specified property path.
    /// Supports positional syntax: <c>{Binding PropertyName}</c>.
    /// </summary>
    public BindingExtension(string path) => Path = path;

    /// <summary>
    /// The source property path to bind to.
    /// </summary>
    [ConstructorArgument("path")]
    public string? Path { get; set; }

    /// <summary>
    /// The binding mode (OneWay, TwoWay, OneTime).
    /// </summary>
    public BindingMode Mode { get; set; } = BindingMode.OneWay;

    /// <summary>
    /// An optional <see cref="IValueConverter"/> to apply during binding.
    /// </summary>
    public IValueConverter? Converter { get; set; }

    /// <summary>
    /// An optional parameter to pass to the <see cref="Converter"/>.
    /// </summary>
    public object? ConverterParameter { get; set; }

    /// <summary>
    /// An optional <see cref="Binding.RelativeSource"/> that describes the location of the binding source
    /// relative to the binding target. When set, the binding source is determined by the
    /// <see cref="Binding.RelativeSource"/> instead of DataContext.
    /// </summary>
    public RelativeSource? RelativeSource { get; set; }

    /// <inheritdoc />
    public override object? ProvideValue(IServiceProvider serviceProvider)
    {
        // Never called at runtime — XamlLoader handles {Binding} via string parsing.
        return null;
    }
}
