using Portable.Xaml;
using TerminalNinja.Core.Elements;
using TerminalNinja.Xaml.Internal;

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
        ArgumentException.ThrowIfNullOrWhiteSpace(xaml);
        
        using var reader = new StringReader(xaml);
        return LoadFromReader<T>(reader);
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
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"XAML file not found: {path}", path);
        }
        
        using var reader = new StreamReader(path);
        return LoadFromReader<T>(reader);
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
        ArgumentNullException.ThrowIfNull(stream);
        
        using var reader = new StreamReader(stream);
        return LoadFromReader<T>(reader);
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
        
        return result;
    }
}
