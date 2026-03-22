using System.Collections.Concurrent;

namespace TerminalNinja.Aot;

/// <summary>
/// AOT-safe registry that maps types to their content property names.
/// Replaces the hardcoded dictionary in <c>XamlLoader</c> and removes the need
/// to read <c>[ContentProperty]</c> attributes via reflection at runtime.
/// Populated at startup by source-generated <c>[ModuleInitializer]</c> code.
/// </summary>
public static class ContentPropertyRegistry
{
    private static readonly ConcurrentDictionary<Type, string> ContentProperties = new();

    /// <summary>
    /// Registers the content property name for a type.
    /// Called by generated code at module initialization time.
    /// </summary>
    public static void Register(Type type, string propertyName)
    {
        ContentProperties[type] = propertyName;
    }

    /// <summary>
    /// Gets the content property name for a type by walking the type hierarchy.
    /// Returns null if no content property is registered for the type or any base type.
    /// </summary>
    public static string? GetContentPropertyName(Type type)
    {
        var current = type;
        while (current != null)
        {
            if (ContentProperties.TryGetValue(current, out var name))
            {
                return name;
            }

            current = current.BaseType;
        }
        return null;
    }
}
