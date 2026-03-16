namespace TerminalNinja.Aot;

/// <summary>
/// Holds compiled getter and setter delegates for a single property on a specific type.
/// Used by the source generator to replace reflection-based property access.
/// </summary>
public readonly record struct PropertyAccessor(
    Func<object, object?> Getter,
    Action<object, object?>? Setter)
{
    /// <summary>
    /// Whether this property can be written to.
    /// </summary>
    public bool CanWrite => Setter is not null;
}
