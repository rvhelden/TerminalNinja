using System.Reflection;

namespace TerminalNinja.Xaml;

/// <summary>
/// Concrete <see cref="IXamlLayout"/> implementation used by source-generated <c>XamlLayouts</c> classes.
/// Dependencies are lazily resolved via a factory delegate to avoid static field initialization order issues
/// (generated layout fields may reference each other).
/// </summary>
public sealed class XamlLayoutInfo : IXamlLayout
{
    private readonly Func<IXamlLayout[]> _dependencyFactory;
    private IReadOnlyList<IXamlLayout>? _dependencies;

    /// <inheritdoc />
    public string ResourceName { get; }

    /// <inheritdoc />
    public Assembly Assembly { get; }

    /// <inheritdoc />
    public IReadOnlyList<IXamlLayout> Dependencies =>
        _dependencies ??= _dependencyFactory();

    /// <summary>
    /// Creates a new <see cref="XamlLayoutInfo"/> with the specified resource name, assembly, and dependency factory.
    /// </summary>
    /// <param name="resourceName">The logical embedded resource name.</param>
    /// <param name="assembly">The assembly containing the embedded resource.</param>
    /// <param name="dependencyFactory">
    /// Factory that returns the direct dependencies. Called lazily on first access to
    /// <see cref="Dependencies"/>. May return an empty array for layouts with no dependencies.
    /// </param>
    public XamlLayoutInfo(string resourceName, Assembly assembly, Func<IXamlLayout[]> dependencyFactory)
    {
        ArgumentNullException.ThrowIfNull(resourceName);
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(dependencyFactory);

        ResourceName = resourceName;
        Assembly = assembly;
        _dependencyFactory = dependencyFactory;
    }

    /// <summary>
    /// Returns a string representation showing the resource name and dependency count.
    /// </summary>
    public override string ToString() => ResourceName;
}
