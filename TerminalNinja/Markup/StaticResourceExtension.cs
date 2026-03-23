using TerminalNinja.Xaml;

// ReSharper disable once CheckNamespace
namespace System.Windows.Markup;

/// <summary>
/// Design-time markup extension stub for <c>{Binding}</c> syntax in XAML.
/// Provides IDE IntelliSense support in Rider/Visual Studio.
/// At runtime, <c>{Binding}</c> expressions are parsed by <see cref="XamlLoader"/>
/// via string manipulation — this class's <see cref="ProvideValue"/> is never called.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class StaticResourceExtension : MarkupExtension
{
    /// <summary>
    /// Creates a new <see cref="BindingExtension"/> with no initial path.
    /// </summary>
    public StaticResourceExtension() { }
    
    /// <summary>
    /// Creates a new <see cref="BindingExtension"/> with the specified property path.
    /// Supports positional syntax: <c>{Binding PropertyName}</c>.
    /// </summary>
    public StaticResourceExtension(string path) => Path = path;

    /// <summary>
    /// The source property path to bind to.
    /// </summary>
    [ConstructorArgument("path")]
    public string? Path { get; set; }
}
