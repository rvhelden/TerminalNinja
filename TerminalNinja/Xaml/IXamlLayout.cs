using System.Reflection;

namespace TerminalNinja.Xaml;

/// <summary>
/// Describes an embedded XAML layout resource, its assembly location, and its direct dependencies.
/// Source-generated for every <c>.xaml</c> file discovered in the consuming assembly.
///
/// The <see cref="Dependencies"/> property lists other <see cref="IXamlLayout"/> instances
/// that this layout references (custom controls with <c>x:Class</c> defined in other XAML files,
/// or merged resource dictionaries). This forms a dependency graph that
/// <see cref="TerminalXaml.Load{T}(IXamlLayout, object?, Binding.BindingManager?)"/>
/// uses to validate and load all required embedded resources.
/// </summary>
public interface IXamlLayout
{
    /// <summary>
    /// The logical embedded resource name (e.g., <c>"Sample.DemoLayout.xaml"</c>).
    /// This matches the <c>LogicalName</c> metadata set by the MSBuild targets.
    /// </summary>
    string ResourceName { get; }

    /// <summary>
    /// The assembly containing the embedded resource.
    /// </summary>
    Assembly Assembly { get; }

    /// <summary>
    /// Direct XAML layout dependencies — other layouts that this layout references
    /// via custom control elements (whose <c>x:Class</c> is defined in another XAML file)
    /// or merged resource dictionaries.
    /// Empty if this layout has no dependencies.
    /// </summary>
    IReadOnlyList<IXamlLayout> Dependencies { get; }
}
