using System.Runtime.CompilerServices;

namespace TerminalNinja.Aot;

/// <summary>
/// Identifies an attached property by its declaring type and property name.
/// Used as a key in the attached property store.
/// </summary>
/// <param name="DeclaringType">The type that declares the attached property (e.g., typeof(Grid)).</param>
/// <param name="PropertyName">The name of the attached property (e.g., "Row").</param>
public readonly record struct AttachedPropertyKey(Type DeclaringType, string PropertyName);

/// <summary>
/// Provides per-object storage for attached property values.
/// Replaces <c>Portable.Xaml.AttachablePropertyServices</c> with a thread-safe,
/// AOT-compatible implementation backed by <see cref="ConditionalWeakTable{TKey,TValue}"/>.
/// </summary>
/// <remarks>
/// Values are stored in a <see cref="ConditionalWeakTable{TKey,TValue}"/> keyed by the target object,
/// so entries are automatically cleaned up when the target is garbage collected.
/// <see cref="ConditionalWeakTable{TKey,TValue}"/> is thread-safe, eliminating the need for
/// <c>[NotInParallel]</c> on tests that use attached properties.
/// </remarks>
public static class AttachedPropertyStore
{
    /// <summary>
    /// Maps each target object to its dictionary of attached property values.
    /// The outer ConditionalWeakTable ensures entries are GC'd when the target is collected.
    /// </summary>
    private static readonly ConditionalWeakTable<object, Dictionary<AttachedPropertyKey, object?>> Store = new();

    /// <summary>
    /// Sets an attached property value on the specified target object.
    /// </summary>
    /// <typeparam name="T">The type of the property value.</typeparam>
    /// <param name="target">The object to set the property on.</param>
    /// <param name="key">The attached property key.</param>
    /// <param name="value">The value to set.</param>
    public static void SetValue<T>(object target, AttachedPropertyKey key, T value)
    {
        var dict = Store.GetOrCreateValue(target);
        lock (dict)
        {
            dict[key] = value;
        }
    }

    /// <summary>
    /// Tries to get an attached property value from the specified target object.
    /// </summary>
    /// <typeparam name="T">The type of the property value.</typeparam>
    /// <param name="target">The object to get the property from.</param>
    /// <param name="key">The attached property key.</param>
    /// <param name="value">When this method returns, contains the property value if found; otherwise, the default value of <typeparamref name="T"/>.</param>
    /// <returns><c>true</c> if the property was found; otherwise, <c>false</c>.</returns>
    public static bool TryGetValue<T>(object target, AttachedPropertyKey key, out T value)
    {
        if (Store.TryGetValue(target, out var dict))
        {
            lock (dict)
            {
                if (dict.TryGetValue(key, out var raw))
                {
                    value = (T)raw!;
                    return true;
                }
            }
        }

        value = default!;
        return false;
    }
}
