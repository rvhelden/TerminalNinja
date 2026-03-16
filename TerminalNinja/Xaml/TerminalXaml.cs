using System.Reflection;
using Portable.Xaml;
using TerminalNinja.Aot;
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
        
        // Process StaticResource lookups first — this may populate properties on BindingExtension
        // instances (e.g. Converter={StaticResource ...}) that ProcessBindings will read next.
        ProcessStaticResources(result as FrameworkElement);

        // Drain pending bindings AFTER static resources so Converter properties are resolved.
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
    /// Reads converter from the BindingExtension instance (already resolved by ProcessStaticResources).
    /// </summary>
    private static void ProcessBindings(Dictionary<BindingKey, BindingExtension> pendingBindings, BindingManager bindingManager)
    {
        foreach (var kvp in pendingBindings)
        {
            var key = kvp.Key;
            var ext = kvp.Value;

            // Only process if target is an IControl
            if (key.TargetObject is IControl control)
            {
                bindingManager.CreateBinding(
                    control,
                    key.PropertyName,
                    ext.Path!,
                    ext.Mode,
                    ext.Converter,
                    ext.ConverterParameter);
            }
        }
    }
    
    /// <summary>
    /// Processes pending static resource lookups from StaticResourceExtension.
    /// FrameworkElement targets walk up their own parent chain; other objects (e.g. BindingExtension)
    /// fall back to the root element for resource lookup.
    /// </summary>
    private static void ProcessStaticResources(FrameworkElement? root)
    {
        var pendingLookups = StaticResourceExtension.GetAndClearPendingLookups();

        foreach (var lookup in pendingLookups)
        {
            // Use the target element's own resource chain, or fall back to root for non-FrameworkElements
            // (e.g. BindingExtension instances that have Converter={StaticResource ...})
            var resolveFrom = lookup.TargetObject as FrameworkElement ?? root;
            if (resolveFrom == null)
                throw new InvalidOperationException(
                    $"StaticResource '{lookup.ResourceKey}' cannot be resolved: no FrameworkElement context available");

            // Find the resource
            var resource = resolveFrom.TryFindResource(lookup.ResourceKey);
            if (resource == null)
            {
                throw new InvalidOperationException(
                    $"StaticResource '{lookup.ResourceKey}' not found for property '{lookup.PropertyName}'");
            }

            // Try registry first (AOT-safe path for registered types)
            var targetType = lookup.TargetObject.GetType();
            if (PropertyAccessorRegistry.TryGetAccessor(targetType, lookup.PropertyName, out var accessor)
                && accessor.Value.CanWrite)
            {
                var propertyAccessor = accessor.Value;
                var value = resource;
                if (!propertyAccessor.PropertyType.IsInstanceOfType(value))
                {
                    var converter = System.ComponentModel.TypeDescriptor.GetConverter(propertyAccessor.PropertyType);
                    if (converter.CanConvertFrom(value.GetType()))
                    {
                        value = converter.ConvertFrom(value);
                    }
                }
                propertyAccessor.Setter!(lookup.TargetObject, value);
            }
            else
            {
                // Reflection fallback for types not in the registry (e.g. MarkupExtension subclasses
                // like BindingExtension). This entire code path will be removed when Portable.Xaml
                // is replaced by the XAML compiler in Phase 4.
                var property = targetType.GetProperty(lookup.PropertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null || !property.CanWrite)
                {
                    throw new InvalidOperationException(
                        $"Property '{lookup.PropertyName}' not found or is read-only on type '{targetType.Name}'");
                }

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
}
