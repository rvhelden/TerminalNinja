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
}
