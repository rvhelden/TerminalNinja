using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace TerminalNinja.Aot;

/// <summary>
/// Thread-safe registry of factory functions keyed by Type.
/// Populated at startup by source-generated [ModuleInitializer] code.
/// Replaces Activator.CreateInstance calls for AOT compatibility.
/// </summary>
public static class ControlFactoryRegistry
{
    private static readonly ConcurrentDictionary<Type, Func<object>> Factories = new();

    /// <summary>
    /// Register a factory function for a specific type.
    /// Called by generated code at module initialization time.
    /// </summary>
    public static void Register<T>(Func<T> factory) where T : class
    {
        Factories[typeof(T)] = () => factory();
    }

    /// <summary>
    /// Register a factory function for a specific type.
    /// Called by generated code at module initialization time.
    /// </summary>
    public static void Register(Type type, Func<object> factory)
    {
        Factories[type] = factory;
    }

    /// <summary>
    /// Try to create an instance of the specified type using the registered factory.
    /// </summary>
    public static bool TryCreate(Type type, [NotNullWhen(true)] out object? instance)
    {
        if (Factories.TryGetValue(type, out var factory))
        {
            instance = factory();
            return true;
        }

        instance = null;
        return false;
    }

    /// <summary>
    /// Create an instance of the specified type, throwing if no factory is registered.
    /// </summary>
    public static object Create(Type type)
    {
        if (TryCreate(type, out var instance))
        {
            return instance;
        }

        throw new InvalidOperationException(
            $"No factory registered for type '{type.FullName}'. " +
            $"Ensure the type is a non-abstract class discovered by the source generator.");
    }

    /// <summary>
    /// Create an instance of the specified type, cast to T.
    /// </summary>
    public static T Create<T>() where T : class
    {
        return (T)Create(typeof(T));
    }

    /// <summary>
    /// Returns the number of registered factories. Useful for diagnostics.
    /// </summary>
    public static int Count => Factories.Count;

    /// <summary>
    /// Checks if a factory is registered for the given type.
    /// </summary>
    public static bool HasFactory(Type type)
    {
        return Factories.ContainsKey(type);
    }
}
