using Portable.Xaml;
using TerminalNinja.Elements;
using TerminalNinja.Xaml.Binding;
using TerminalNinja.Xaml.Internal;
using TerminalNinja.Xaml.Markup;

namespace TerminalNinja.Xaml;

/// <summary>
/// Provides methods to load TerminalNinja UI elements from XAML markup.
/// </summary>
public static class TerminalXaml
{
    /// <summary>
    /// Loads a UI element from XAML string.
    /// </summary>
    /// <typeparam name="T">The expected type of the root element.</typeparam>
    /// <param name="xaml">The XAML markup string.</param>
    /// <returns>The loaded element.</returns>
    /// <exception cref="ArgumentNullException">Thrown when xaml is null.</exception>
    /// <exception cref="InvalidCastException">Thrown when the loaded element is not of type T.</exception>
    public static T Load<T>(string xaml) where T : class, IElement
    {
        return Load<T>(xaml, dataContext: null, bindingManager: null);
    }
    
    /// <summary>
    /// Loads a UI element from XAML string with data binding support.
    /// </summary>
    /// <typeparam name="T">The expected type of the root element.</typeparam>
    /// <param name="xaml">The XAML markup string.</param>
    /// <param name="dataContext">The data context for bindings.</param>
    /// <param name="bindingManager">Optional binding manager (creates new if null).</param>
    /// <returns>The loaded element with bindings activated.</returns>
    public static T Load<T>(string xaml, object? dataContext, BindingManager? bindingManager = null) where T : class, IElement
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xaml);
        
        using var reader = new StringReader(xaml);
        return LoadFromReader<T>(reader, dataContext, bindingManager);
    }
    
    /// <summary>
    /// Loads a UI element from a XAML file.
    /// </summary>
    /// <typeparam name="T">The expected type of the root element.</typeparam>
    /// <param name="path">The path to the XAML file.</param>
    /// <returns>The loaded element.</returns>
    /// <exception cref="ArgumentNullException">Thrown when path is null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file doesn't exist.</exception>
    /// <exception cref="InvalidCastException">Thrown when the loaded element is not of type T.</exception>
    public static T LoadFromFile<T>(string path) where T : class, IElement
    {
        return LoadFromFile<T>(path, dataContext: null, bindingManager: null);
    }
    
    /// <summary>
    /// Loads a UI element from a XAML file with data binding support.
    /// </summary>
    /// <typeparam name="T">The expected type of the root element.</typeparam>
    /// <param name="path">The path to the XAML file.</param>
    /// <param name="dataContext">The data context for bindings.</param>
    /// <param name="bindingManager">Optional binding manager (creates new if null).</param>
    /// <returns>The loaded element with bindings activated.</returns>
    public static T LoadFromFile<T>(string path, object? dataContext, BindingManager? bindingManager = null) where T : class, IElement
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"XAML file not found: {path}", path);
        }
        
        using var reader = new StreamReader(path);
        return LoadFromReader<T>(reader, dataContext, bindingManager);
    }
    
    /// <summary>
    /// Loads a UI element from a stream.
    /// </summary>
    /// <typeparam name="T">The expected type of the root element.</typeparam>
    /// <param name="stream">The stream containing XAML markup.</param>
    /// <returns>The loaded element.</returns>
    /// <exception cref="ArgumentNullException">Thrown when stream is null.</exception>
    /// <exception cref="InvalidCastException">Thrown when the loaded element is not of type T.</exception>
    public static T LoadFromStream<T>(Stream stream) where T : class, IElement
    {
        return LoadFromStream<T>(stream, dataContext: null, bindingManager: null);
    }
    
    /// <summary>
    /// Loads a UI element from a stream with data binding support.
    /// </summary>
    /// <typeparam name="T">The expected type of the root element.</typeparam>
    /// <param name="stream">The stream containing XAML markup.</param>
    /// <param name="dataContext">The data context for bindings.</param>
    /// <param name="bindingManager">Optional binding manager (creates new if null).</param>
    /// <returns>The loaded element with bindings activated.</returns>
    public static T LoadFromStream<T>(Stream stream, object? dataContext, BindingManager? bindingManager = null) where T : class, IElement
    {
        ArgumentNullException.ThrowIfNull(stream);
        
        using var reader = new StreamReader(stream);
        return LoadFromReader<T>(reader, dataContext, bindingManager);
    }
    
    /// <summary>
    /// Loads a UI element from an embedded resource.
    /// </summary>
    /// <typeparam name="T">The expected type of the root element.</typeparam>
    /// <param name="resourceName">The fully qualified resource name.</param>
    /// <param name="assembly">The assembly containing the resource (defaults to calling assembly).</param>
    /// <returns>The loaded element.</returns>
    /// <exception cref="ArgumentNullException">Thrown when resourceName is null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the resource doesn't exist.</exception>
    /// <exception cref="InvalidCastException">Thrown when the loaded element is not of type T.</exception>
    public static T LoadFromEmbeddedResource<T>(string resourceName, System.Reflection.Assembly? assembly = null) 
        where T : class, IElement
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        
        assembly ??= System.Reflection.Assembly.GetCallingAssembly();
        
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new FileNotFoundException($"Embedded resource not found: {resourceName}", resourceName);
        }
        
        return LoadFromStream<T>(stream);
    }
    
    private static T LoadFromReader<T>(TextReader reader) where T : class, IElement
    {
        return LoadFromReader<T>(reader, dataContext: null, bindingManager: null);
    }
    
    private static T LoadFromReader<T>(TextReader reader, object? dataContext, BindingManager? bindingManager) where T : class, IElement
    {
        var schemaContext = new TerminalXamlSchemaContext();
        
        using var xamlReader = new XamlXmlReader(reader, schemaContext);
        using var writer = new XamlObjectWriter(schemaContext);
        
        XamlServices.Transform(xamlReader, writer);
        
        var result = writer.Result as T;
        if (result == null)
        {
            var actualType = writer.Result?.GetType()?.Name ?? "null";
            throw new InvalidCastException(
                $"XAML root element is of type {actualType}, expected {typeof(T).Name}");
        }
        
        // Post-process to handle Stack attached properties
        StackChildProcessor.ProcessElement(result);
        
        // Process StaticResource lookups
        ProcessStaticResources(result);
        
        // Process bindings if dataContext is provided
        if (dataContext != null)
        {
            bindingManager ??= new BindingManager();
            
            // Get pending bindings from BindingExtension
            var pendingBindings = BindingExtension.GetAndClearPendingBindings();
            ProcessBindings(pendingBindings, bindingManager);
            
            // Set DataContext recursively
            bindingManager.SetDataContextRecursive(result, dataContext);
        }
        
        return result;
    }
    
    /// <summary>
    /// Processes pending bindings from the static dictionary populated during XAML parsing.
    /// </summary>
    private static void ProcessBindings(Dictionary<BindingKey, BindingInfo> pendingBindings, BindingManager bindingManager)
    {
        foreach (var kvp in pendingBindings)
        {
            var key = kvp.Key;
            var info = kvp.Value;
            
            // Only process if target is an IElement
            if (key.TargetObject is IElement element)
            {
                bindingManager.CreateBinding(
                    element,
                    key.PropertyName,
                    info.Path,
                    info.Mode,
                    info.Converter,
                    info.ConverterParameter);
            }
        }
    }
    
    /// <summary>
    /// Processes pending static resource lookups from StaticResourceExtension.
    /// Walks the visual tree to resolve resources and sets property values.
    /// </summary>
    private static void ProcessStaticResources(IElement root)
    {
        var pendingLookups = StaticResourceExtension.GetAndClearPendingLookups();
        
        foreach (var lookup in pendingLookups)
        {
            // Only resolve for FrameworkElements
            if (lookup.TargetObject is not FrameworkElement targetElement)
                continue;
            
            // Find the resource
            var resource = targetElement.TryFindResource(lookup.ResourceKey);
            if (resource == null)
            {
                throw new InvalidOperationException(
                    $"StaticResource '{lookup.ResourceKey}' not found for property '{lookup.PropertyName}'");
            }
            
            // Get the property and set the value
            var property = lookup.TargetObject.GetType().GetProperty(lookup.PropertyName);
            if (property == null || !property.CanWrite)
            {
                throw new InvalidOperationException(
                    $"Property '{lookup.PropertyName}' not found or is read-only on type '{lookup.TargetObject.GetType().Name}'");
            }
            
            // Convert value if needed
            var value = resource;
            if (!property.PropertyType.IsInstanceOfType(value))
            {
                var converter = System.ComponentModel.TypeDescriptor.GetConverter(property.PropertyType);
                if (converter.CanConvertFrom(value.GetType()))
                {
                    value = converter.ConvertFrom(value);
                }
            }
            
            property.SetValue(lookup.TargetObject, value);
        }
    }
}
