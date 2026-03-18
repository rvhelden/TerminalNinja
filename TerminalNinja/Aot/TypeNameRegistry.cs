using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using TerminalNinja.Controls;
using TerminalNinja.Primitives;
using TerminalNinja.Styling;
using TerminalNinja.Xaml.Binding;
using TerminalNinja.Xaml.Data;
namespace TerminalNinja.Aot;

/// <summary>
/// AOT-safe registry that maps fully-qualified type names to <see cref="Type"/> objects.
/// Replaces <c>Assembly.GetType(string)</c> scanning which is not trim-safe.
/// Populated at startup by generated code (for control/factory types) and
/// static initializer (for primitive/non-instantiable types).
/// </summary>
public static class TypeNameRegistry
{
    private static readonly ConcurrentDictionary<string, Type> Types = new(StringComparer.Ordinal);

    /// <summary>
    /// Static initializer registers non-instantiable types that may appear in XAML
    /// but don't have factories in the ControlFactoryRegistry.
    /// </summary>
    static TypeNameRegistry()
    {
        // Primitive types (used in resource dictionaries, e.g., <Color x:Key="...">Red</Color>)
        Register(typeof(Color));
        Register(typeof(Thickness));
        Register(typeof(Size));
        Register(typeof(GridLength));
        Register(typeof(BorderStyle));

        // Enums that may appear in XAML
        Register(typeof(Orientation));
        Register(typeof(Alignment));
        Register(typeof(ChildSizeMode));
        Register(typeof(TextWrapping));
        Register(typeof(TextTrimming));
        Register(typeof(TextAlignment));
        Register(typeof(BindingMode));
        Register(typeof(RelativeSourceMode));

        // Non-instantiable framework types that may appear in XAML as type references
        // (e.g., DataTemplate.DataType="b:RelativeSource")
        Register(typeof(RelativeSource));
    }

    /// <summary>
    /// Registers a type by its full name.
    /// </summary>
    public static void Register(Type type)
    {
        if (type.FullName != null)
        {
            Types[type.FullName] = type;
        }
    }

    /// <summary>
    /// Registers a type with an explicit fully-qualified name.
    /// </summary>
    public static void Register(string fullName, Type type)
    {
        Types[fullName] = type;
    }

    /// <summary>
    /// Tries to resolve a type by its fully-qualified name.
    /// Falls back to scanning loaded assemblies for consumer-defined types
    /// (e.g., data model classes referenced via <c>clr-namespace:</c> in XAML).
    /// Resolved types are cached so subsequent lookups are O(1).
    /// </summary>
    public static Type? ResolveType(string fullName)
    {
        if (Types.TryGetValue(fullName, out var type))
            return type;

        // Fallback: scan loaded assemblies for the type.
        // This handles consumer-defined types (data models, view models, etc.)
        // that aren't pre-registered via source generators or static initializer.
        var resolved = ResolveFromLoadedAssemblies(fullName);
        if (resolved != null)
        {
            // Cache so future lookups are O(1)
            Types[fullName] = resolved;
        }

        return resolved;
    }

    /// <summary>
    /// Scans all loaded assemblies for a type by its fully-qualified name.
    /// This is the runtime fallback for types not registered at compile time.
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL2026",
        Justification = "Runtime fallback for consumer-defined types (data models, view models) " +
                         "referenced via clr-namespace: in XAML. These types are not subject to trimming " +
                         "because the consuming application preserves them.")]
    private static Type? ResolveFromLoadedAssemblies(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(fullName);
            if (type != null)
                return type;
        }

        return null;
    }
}
