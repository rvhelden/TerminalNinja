using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace TerminalNinja.DependencySystem;

/// <summary>
/// Represents a registered dependency property that can be used with <see cref="DependencyObject.GetValue"/>
/// and <see cref="DependencyObject.SetValue"/>.
/// </summary>
public sealed class DependencyProperty
{
    private static readonly Dictionary<(Type ownerType, string name), DependencyProperty> _registry = new();
    private static readonly Lock _registryLock = new();

    /// <summary>Gets the name of the dependency property.</summary>
    public string Name { get; }

    /// <summary>Gets the type of the property's value.</summary>
    public Type PropertyType { get; }

    /// <summary>Gets the type that registered this dependency property.</summary>
    public Type OwnerType { get; }

    /// <summary>Gets the metadata associated with this property.</summary>
    public PropertyMetadata DefaultMetadata { get; }

    /// <summary>Gets whether this is an attached property.</summary>
    public bool IsAttached { get; }

    private DependencyProperty(string name, Type propertyType, Type ownerType, PropertyMetadata metadata, bool isAttached)
    {
        Name = name;
        PropertyType = propertyType;
        OwnerType = ownerType;
        DefaultMetadata = metadata;
        IsAttached = isAttached;
    }

    /// <summary>
    /// Registers a dependency property on an owner type.
    /// </summary>
    public static DependencyProperty Register(string name, Type propertyType, Type ownerType, PropertyMetadata? metadata = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(propertyType);
        ArgumentNullException.ThrowIfNull(ownerType);

        return RegisterCore(name, propertyType, ownerType, metadata ?? new PropertyMetadata(), isAttached: false);
    }

    /// <summary>
    /// Registers an attached dependency property on an owner type.
    /// Attached properties can be set on any <see cref="DependencyObject"/>, not just the owner type.
    /// </summary>
    public static DependencyProperty RegisterAttached(string name, Type propertyType, Type ownerType, PropertyMetadata? metadata = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(propertyType);
        ArgumentNullException.ThrowIfNull(ownerType);

        return RegisterCore(name, propertyType, ownerType, metadata ?? new PropertyMetadata(), isAttached: true);
    }

    private static DependencyProperty RegisterCore(string name, Type propertyType, Type ownerType, PropertyMetadata metadata, bool isAttached)
    {
        var dp = new DependencyProperty(name, propertyType, ownerType, metadata, isAttached);

        lock (_registryLock)
        {
            var key = (ownerType, name);
            if (_registry.ContainsKey(key))
            {
                throw new InvalidOperationException(
                    $"DependencyProperty '{name}' is already registered on type '{ownerType.Name}'.");
            }

            _registry[key] = dp;
        }

        return dp;
    }

    /// <summary>
    /// Looks up a registered dependency property by owner type and name.
    /// Walks the type hierarchy (owner → base → base … → object) to find inherited registrations.
    /// Ensures static constructors are run for each type in the hierarchy, so that
    /// <see cref="Register"/> calls in static field initializers have executed
    /// (types with <c>beforefieldinit</c> semantics may defer their <c>.cctor</c>
    /// until a static field is actually accessed).
    /// Returns <c>null</c> if no matching property is found.
    /// </summary>
    /// <param name="ownerType">The type that owns (or inherits) the property.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The registered <see cref="DependencyProperty"/>, or <c>null</c>.</returns>
    public static DependencyProperty? Find(Type ownerType, string name)
    {
        ArgumentNullException.ThrowIfNull(ownerType);
        ArgumentException.ThrowIfNullOrEmpty(name);

        // First pass: ensure all static constructors in the hierarchy have run.
        // This is necessary because DependencyProperty.Register is typically called
        // from static field initializers, which are only executed when the runtime
        // triggers the type's .cctor. Types marked with beforefieldinit (i.e., types
        // without an explicit static constructor) can defer .cctor execution until a
        // static field is actually accessed — creating an instance alone is NOT enough.
        EnsureStaticConstructors(ownerType);

        lock (_registryLock)
        {
            var type = ownerType;
            while (type != null)
            {
                if (_registry.TryGetValue((type, name), out var dp))
                {
                    return dp;
                }

                type = type.BaseType;
            }
        }

        return null;
    }

    /// <summary>
    /// Tracks types whose static constructors have already been triggered,
    /// avoiding redundant <see cref="RuntimeHelpers.RunClassConstructor"/> calls.
    /// </summary>
    private static readonly HashSet<Type> _initializedTypes = new();

    /// <summary>
    /// Ensures the static constructor (.cctor) has been run for every type in the hierarchy
    /// from <paramref name="type"/> up to (but not including) <c>object</c>.
    /// Uses <see cref="RuntimeHelpers.RunClassConstructor"/> which is a no-op if the .cctor
    /// has already executed.
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL2059",
        Justification = "Types passed here are concrete control types from the TerminalNinja hierarchy. " +
                         "Their static constructors register DependencyProperties and are always preserved.")]
    private static void EnsureStaticConstructors(Type type)
    {
        var current = type;
        while (current != null && current != typeof(object))
        {
            // Fast path: already known to be initialized
            bool alreadyInitialized;
            lock (_registryLock)
            {
                alreadyInitialized = _initializedTypes.Contains(current);
            }

            if (!alreadyInitialized)
            {
                RuntimeHelpers.RunClassConstructor(current.TypeHandle);
                lock (_registryLock)
                {
                    _initializedTypes.Add(current);
                }
            }

            current = current.BaseType;
        }
    }
}
