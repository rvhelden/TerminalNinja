using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace TerminalNinja.Aot;

/// <summary>
/// Thread-safe registry of property accessors keyed by (Type, PropertyName).
/// Populated at startup by source-generated [ModuleInitializer] code.
/// At runtime, the binding system queries this instead of using reflection.
/// Falls back to reflection-based accessors for unregistered types (e.g., plain
/// data classes used as binding sources in DataTemplates).
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
    /// Falls back to reflection-based accessor for unregistered types.
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

        // Reflection fallback for unregistered types (e.g., plain data classes
        // used as binding sources in DataTemplates). The accessor is cached so
        // reflection is only used once per (type, property) pair.
        if (TryCreateReflectionAccessor(type, propertyName, out var reflectionAccessor))
        {
            Accessors[(type, propertyName)] = reflectionAccessor.Value;
            accessor = reflectionAccessor;
            return true;
        }

        accessor = null;
        return false;
    }

    /// <summary>
    /// Creates a PropertyAccessor backed by PropertyInfo reflection.
    /// Used as a fallback for types not discovered by the source generator
    /// (e.g., plain data classes used as binding sources in DataTemplates).
    /// The types are always preserved because they are instantiated directly in
    /// user code, so suppressing the trim warning is safe.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2070",
        Justification = "Binding source types are instantiated in user code and will not be trimmed.")]
    private static bool TryCreateReflectionAccessor(
        Type type,
        string propertyName,
        [NotNullWhen(true)] out PropertyAccessor? accessor)
    {
        var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (prop == null)
        {
            accessor = null;
            return false;
        }

        Func<object, object?> getter = prop.GetValue;
        Action<object, object?>? setter = prop.CanWrite ? prop.SetValue : null;

        accessor = new PropertyAccessor(prop.PropertyType, getter, setter);
        return true;
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

    /// <summary>
    /// Gets all registered property accessors for the given type, including those
    /// inherited from base types. Returns (propertyName, accessor) pairs.
    /// </summary>
    public static IEnumerable<KeyValuePair<string, PropertyAccessor>> GetAccessors(Type type)
    {
        // Collect all property names from the type hierarchy (child types first so they win)
        var seen = new HashSet<string>(StringComparer.Ordinal);

        var current = type;
        while (current is not null)
        {
            foreach (var kvp in Accessors)
            {
                if (kvp.Key.Type == current && seen.Add(kvp.Key.PropertyName))
                {
                    yield return new KeyValuePair<string, PropertyAccessor>(kvp.Key.PropertyName, kvp.Value);
                }
            }
            current = current.BaseType;
        }
    }
}
