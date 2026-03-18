using System.Windows.Markup;
using TerminalNinja.Xaml;
using TerminalNinja.Xaml.Binding;

namespace TerminalNinja.Markup;

/// <summary>
/// Design-time markup extension stub for <c>{RelativeSource}</c> syntax in XAML.
/// Provides IDE IntelliSense support in Rider/Visual Studio.
/// At runtime, <c>{RelativeSource}</c> expressions are parsed by <see cref="XamlLoader"/>
/// via string manipulation — this class's <see cref="ProvideValue"/> is never called.
/// </summary>
[MarkupExtensionReturnType(typeof(RelativeSource))]
public sealed class RelativeSourceExtension : MarkupExtension
{
    /// <summary>
    /// Creates a new <see cref="RelativeSourceExtension"/> with no initial mode.
    /// </summary>
    public RelativeSourceExtension() { }

    /// <summary>
    /// Creates a new <see cref="RelativeSourceExtension"/> with the specified mode.
    /// Supports positional syntax: <c>{RelativeSource Self}</c>.
    /// </summary>
    public RelativeSourceExtension(RelativeSourceMode mode) => Mode = mode;

    /// <summary>
    /// The mode that describes the location of the binding source relative to the binding target.
    /// </summary>
    [ConstructorArgument("mode")]
    public RelativeSourceMode Mode { get; set; }

    /// <summary>
    /// The type of ancestor to look for when <see cref="Mode"/> is
    /// <see cref="RelativeSourceMode.FindAncestor"/>.
    /// </summary>
    public Type? AncestorType { get; set; }

    /// <summary>
    /// The level of ancestor to look for (1-based) in
    /// <see cref="RelativeSourceMode.FindAncestor"/> mode.
    /// </summary>
    public int AncestorLevel { get; set; } = 1;

    /// <inheritdoc />
    public override object? ProvideValue(IServiceProvider serviceProvider)
    {
        // Never called at runtime — XamlLoader handles {RelativeSource} via string parsing.
        return new RelativeSource(Mode)
        {
            AncestorType = AncestorType,
            AncestorLevel = AncestorLevel
        };
    }
}
