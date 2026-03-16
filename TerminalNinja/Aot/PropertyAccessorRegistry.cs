using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace TerminalNinja.Aot;

/// <summary>
/// Thread-safe registry of property accessors keyed by (Type, PropertyName).
/// Populated at startup by source-generated [ModuleInitializer] code.
/// At runtime, the binding system queries this instead of using reflection.
/// </summary>
public static class PropertyAccessorRegistry
{
    private static readonly ConcurrentDictionary<(Type Type, string PropertyName), PropertyAccessor> Accessors = new();

    /// <summary>
    /// Register a property accessor for a specific type and property name.
    /// Called by generated code at module initialization time.
    /// </summary>
    public static void Register(Type type, string propertyName, PropertyAccessor accessor)
    {
        Accessors[(type, propertyName)] = accessor;
    }

    /// <summary>
    /// Try to get a property accessor for a specific type and property name.
    /// Walks the type hierarchy if an exact match isn't found.
    /// </summary>
    public static bool TryGetAccessor(Type type, string propertyName, [NotNullWhen(true)] out PropertyAccessor? accessor)
    {
        // Try exact type first
        if (Accessors.TryGetValue((type, propertyName), out var found))
        {
            accessor = found;
            return true;
        }

        // Walk base types
        var baseType = type.BaseType;
        while (baseType is not null)
        {
            if (Accessors.TryGetValue((baseType, propertyName), out found))
            {
                // Cache for next time with the concrete type
                Accessors[(type, propertyName)] = found;
                accessor = found;
                return true;
            }
            baseType = baseType.BaseType;
        }

        accessor = null;
        return false;
    }

    /// <summary>
    /// Get a property accessor, throwing if not found.
    /// Use this in strict mode where all types must be registered.
    /// </summary>
    public static PropertyAccessor GetAccessor(Type type, string propertyName)
    {
        if (TryGetAccessor(type, propertyName, out var accessor))
            return accessor.Value;

        throw new InvalidOperationException(
            $"No property accessor registered for '{propertyName}' on type '{type.FullName}'. " +
            $"Ensure the type is discovered by the source generator. " +
            $"The type must implement IControl, inherit from ViewModelBase, or implement INotifyPropertyChanged.");
    }

    /// <summary>
    /// Returns the number of registered accessors. Useful for diagnostics.
    /// </summary>
    public static int Count => Accessors.Count;

    /// <summary>
    /// Checks if an accessor is registered for the given type and property name
    /// (including via base type lookup).
    /// </summary>
    public static bool HasAccessor(Type type, string propertyName)
    {
        return TryGetAccessor(type, propertyName, out _);
    }
}
