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
}
