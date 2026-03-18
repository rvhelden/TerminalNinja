using System.Reflection;
using TerminalNinja.Controls;
using TerminalNinja.Xaml.Binding;

namespace TerminalNinja.Xaml;

/// <summary>
/// Result of loading a XAML file, containing both the root control and named elements.
/// Used by generated InitializeComponent() methods to wire up x:Name'd fields.
/// </summary>
public sealed class XamlLoadResult<T> where T : FrameworkElement
{
    /// <summary>
    /// The root control loaded from XAML.
    /// </summary>
    public T Control { get; }

    /// <summary>
    /// Dictionary of elements with x:Name attributes, keyed by name.
    /// </summary>
    public IReadOnlyDictionary<string, object> NamedElements { get; }

    internal XamlLoadResult(T control, IReadOnlyDictionary<string, object> namedElements)
    {
        Control = control;
        NamedElements = namedElements;
    }
}

/// <summary>
/// Provides methods to load TerminalNinja UI elements from XAML markup.
/// Delegates to <see cref="XamlLoader"/> for AOT-compatible parsing.
/// </summary>
public static class TerminalXaml
{
    /// <summary>
    /// Loads a UI control from XAML string.
    /// </summary>
    /// <typeparam name="T">The expected type of the root control.</typeparam>
    /// <param name="xaml">The XAML markup string.</param>
    /// <returns>The loaded control.</returns>
    /// <exception cref="ArgumentNullException">Thrown when xaml is null.</exception>
    /// <exception cref="InvalidCastException">Thrown when the loaded control is not of type T.</exception>
    public static T Load<T>(string xaml) where T : FrameworkElement
    {
        return Load<T>(xaml, dataContext: null, bindingManager: null);
    }
    
    /// <summary>
    /// Loads a UI control from XAML string with data binding support.
    /// </summary>
    /// <typeparam name="T">The expected type of the root control.</typeparam>
    /// <param name="xaml">The XAML markup string.</param>
    /// <param name="dataContext">The data context for bindings.</param>
    /// <param name="bindingManager">Optional binding manager (creates new if null).</param>
    /// <returns>The loaded control with bindings activated.</returns>
    public static T Load<T>(string xaml, object? dataContext, BindingManager? bindingManager = null) where T : FrameworkElement
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xaml);
        
        var loader = new XamlLoader();
        return loader.Load<T>(xaml, dataContext, bindingManager);
    }
    
    /// <summary>
    /// Loads a UI control from a stream with data binding support.
    /// </summary>
    /// <typeparam name="T">The expected type of the root control.</typeparam>
    /// <param name="stream">The stream containing XAML markup.</param>
    /// <param name="dataContext">The data context for bindings.</param>
    /// <param name="bindingManager">Optional binding manager (creates new if null).</param>
    /// <returns>The loaded control with bindings activated.</returns>
    public static T LoadFromStream<T>(Stream stream, object? dataContext, BindingManager? bindingManager = null) where T : FrameworkElement
    {
        ArgumentNullException.ThrowIfNull(stream);
        
        var loader = new XamlLoader();
        return loader.LoadFromStream<T>(stream, dataContext, bindingManager);
    }

    /// <summary>
    /// Loads a UI control from a stream and returns both the control and named elements.
    /// This overload is used by generated InitializeComponent() methods from x:Class XAML files.
    /// </summary>
    /// <typeparam name="T">The expected type of the root control.</typeparam>
    /// <param name="stream">The stream containing XAML markup.</param>
    /// <param name="dataContext">The data context for bindings.</param>
    /// <param name="bindingManager">Optional binding manager (creates new if null).</param>
    /// <returns>A result containing the loaded control and named elements.</returns>
    public static XamlLoadResult<T> LoadFromStreamWithNamedElements<T>(Stream stream, object? dataContext, BindingManager? bindingManager = null) where T : FrameworkElement
    {
        ArgumentNullException.ThrowIfNull(stream);
        
        var loader = new XamlLoader();
        var control = loader.LoadFromStream<T>(stream, dataContext, bindingManager);
        return new XamlLoadResult<T>(control, loader.NamedElements);
    }

    // ────────────────────────────────────────────────────────────────
    //  IXamlLayout-based loading
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads a UI control from an <see cref="IXamlLayout"/> descriptor.
    /// Validates and pre-loads all transitive XAML dependencies before loading the root layout.
    /// </summary>
    /// <typeparam name="T">The expected type of the root control.</typeparam>
    /// <param name="layout">The layout descriptor (typically from the generated <c>XamlLayouts</c> class).</param>
    /// <returns>The loaded control.</returns>
    public static T Load<T>(IXamlLayout layout) where T : FrameworkElement
    {
        return Load<T>(layout, dataContext: null, bindingManager: null);
    }

    /// <summary>
    /// Loads a UI control from an <see cref="IXamlLayout"/> descriptor with data binding support.
    /// Validates and pre-loads all transitive XAML dependencies before loading the root layout.
    /// </summary>
    /// <typeparam name="T">The expected type of the root control.</typeparam>
    /// <param name="layout">The layout descriptor (typically from the generated <c>XamlLayouts</c> class).</param>
    /// <param name="dataContext">The data context for bindings.</param>
    /// <param name="bindingManager">Optional binding manager (creates new if null).</param>
    /// <returns>The loaded control with bindings activated.</returns>
    public static T Load<T>(IXamlLayout layout, object? dataContext, BindingManager? bindingManager = null) where T : FrameworkElement
    {
        ArgumentNullException.ThrowIfNull(layout);

        // Collect all transitive dependencies (depth-first, deduplicated)
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<IXamlLayout>();
        CollectDependencies(layout, visited, ordered);

        // Validate that all embedded resources exist before loading anything
        ValidateEmbeddedResources(ordered);

        // Load the root layout from its embedded resource stream
        using var stream = OpenEmbeddedResource(layout);
        return LoadFromStream<T>(stream, dataContext, bindingManager);
    }

    /// <summary>
    /// Recursively collects all transitive dependencies in depth-first order.
    /// Dependencies are added before the layouts that depend on them.
    /// </summary>
    private static void CollectDependencies(IXamlLayout layout, HashSet<string> visited, List<IXamlLayout> ordered)
    {
        if (!visited.Add(layout.ResourceName))
            return;

        foreach (var dep in layout.Dependencies)
        {
            CollectDependencies(dep, visited, ordered);
        }

        ordered.Add(layout);
    }

    /// <summary>
    /// Validates that every layout in the list has its embedded resource present in its assembly.
    /// Throws an <see cref="InvalidOperationException"/> listing all missing resources.
    /// </summary>
    private static void ValidateEmbeddedResources(List<IXamlLayout> layouts)
    {
        var missing = new List<string>();

        foreach (var layout in layouts)
        {
            using var stream = layout.Assembly.GetManifestResourceStream(layout.ResourceName);
            if (stream == null)
            {
                missing.Add($"  - '{layout.ResourceName}' in assembly '{layout.Assembly.GetName().Name}'");
            }
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"The following embedded XAML resources are missing:\n{string.Join("\n", missing)}\n" +
                "Ensure all XAML files are included as EmbeddedResource with the correct LogicalName. " +
                "If using TerminalNinja MSBuild targets, verify <EnableTerminalNinjaXamlAutoInclude> is not set to false.");
        }
    }

    /// <summary>
    /// Opens the embedded resource stream for a layout, throwing a descriptive error if not found.
    /// </summary>
    private static Stream OpenEmbeddedResource(IXamlLayout layout)
    {
        var stream = layout.Assembly.GetManifestResourceStream(layout.ResourceName);
        if (stream == null)
        {
            var available = layout.Assembly.GetManifestResourceNames();
            throw new InvalidOperationException(
                $"Embedded XAML resource '{layout.ResourceName}' not found in assembly '{layout.Assembly.GetName().Name}'. " +
                $"Available resources: [{string.Join(", ", available)}]");
        }
        return stream;
    }
}
