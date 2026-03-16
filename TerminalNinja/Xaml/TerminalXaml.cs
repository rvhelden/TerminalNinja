using Portable.Xaml;
using TerminalNinja.Controls;
using TerminalNinja.Xaml.Binding;
using TerminalNinja.Xaml.Markup;

namespace TerminalNinja.Xaml;

/// <summary>
/// Provides methods to load TerminalNinja UI elements from XAML markup.
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
    public static T Load<T>(string xaml) where T : class, IControl
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
    public static T Load<T>(string xaml, object? dataContext, BindingManager? bindingManager = null) where T : class, IControl
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xaml);
        
        using var reader = new StringReader(xaml);
        return LoadFromReader<T>(reader, dataContext, bindingManager);
    }
    
    /// <summary>
    /// Loads a UI control from a stream with data binding support.
    /// </summary>
    /// <typeparam name="T">The expected type of the root control.</typeparam>
    /// <param name="stream">The stream containing XAML markup.</param>
    /// <param name="dataContext">The data context for bindings.</param>
    /// <param name="bindingManager">Optional binding manager (creates new if null).</param>
    /// <returns>The loaded control with bindings activated.</returns>
    public static T LoadFromStream<T>(Stream stream, object? dataContext, BindingManager? bindingManager = null) where T : class, IControl
    {
        ArgumentNullException.ThrowIfNull(stream);
        
        using var reader = new StreamReader(stream);
        return LoadFromReader<T>(reader, dataContext, bindingManager);
    }
    
    private static T LoadFromReader<T>(TextReader reader, object? dataContext, BindingManager? bindingManager) where T : class, IControl
    {
        var schemaContext = new XamlSchemaContext();
        
        using var xamlReader = new XamlXmlReader(reader, schemaContext);
        using var writer = new XamlObjectWriter(schemaContext);
        
        XamlServices.Transform(xamlReader, writer);
        
        var result = writer.Result as T;
        if (result == null)
        {
            var actualType = writer.Result?.GetType()?.Name ?? "null";
            throw new InvalidCastException(
                $"XAML root control is of type {actualType}, expected {typeof(T).Name}");
        }
        
        // Process StaticResource lookups
        ProcessStaticResources();

        // Always drain pending bindings so AsyncLocal state stays clean
        var pendingBindings = BindingExtension.GetAndClearPendingBindings();

        // Process bindings if dataContext is provided
        if (dataContext != null)
        {
            bindingManager ??= new BindingManager();
            ProcessBindings(pendingBindings, bindingManager);

            // Set DataContext recursively — this re-activates bindings on each control
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
            
            // Only process if target is an IControl
            if (key.TargetObject is IControl control)
            {
                bindingManager.CreateBinding(
                    control,
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
    /// Each target element walks up its parent chain to resolve resources.
    /// </summary>
    private static void ProcessStaticResources()
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
