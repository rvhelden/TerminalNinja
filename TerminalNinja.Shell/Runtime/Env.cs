using System.Collections.Immutable;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Runtime;

/// <summary>
/// A single slot in an <see cref="Env"/>. The slot is allocated when the binding
/// is reserved; its <see cref="Value"/> is filled in once the RHS of the
/// <c>let</c> evaluates. Closures capture the slot by reference, which is how a
/// recursive lambda can call itself by name.
/// </summary>
public sealed class EnvRef
{
    /// <summary>The bound value. Reserved slots hold <see cref="NUnit.Instance"/> as a placeholder until filled.</summary>
    public NValue Value { get; set; } = NUnit.Instance;
}

/// <summary>
/// Immutable lexical environment. New bindings produce a new <see cref="Env"/>
/// that shares structure with its parent through <see cref="ImmutableDictionary{TKey,TValue}.SetItem"/>.
/// </summary>
public sealed class Env
{
    private readonly ImmutableDictionary<string, EnvRef> _bindings;

    /// <summary>The empty environment.</summary>
    public static Env Empty { get; } = new(ImmutableDictionary<string, EnvRef>.Empty.WithComparers(StringComparer.Ordinal));

    private Env(ImmutableDictionary<string, EnvRef> bindings) => _bindings = bindings;

    /// <summary>Extend the environment with a binding whose value is already known.</summary>
    public Env Extend(string name, NValue value)
    {
        var slot = new EnvRef { Value = value };
        return new Env(_bindings.SetItem(name, slot));
    }

    /// <summary>
    /// Reserve a slot for a binding whose value isn't yet known. The slot is
    /// returned via <paramref name="slot"/> so the caller can fill it after
    /// evaluating the RHS in the new environment — enabling recursive <c>let</c>.
    /// </summary>
    public Env Reserve(string name, out EnvRef slot)
    {
        slot = new EnvRef();
        return new Env(_bindings.SetItem(name, slot));
    }

    /// <summary>Look up <paramref name="name"/>; throws <see cref="EvaluatorException"/> if unbound.</summary>
    public NValue Lookup(string name)
    {
        if (_bindings.TryGetValue(name, out var slot)) return slot.Value;
        throw new EvaluatorException($"unbound identifier '{name}'");
    }

    /// <summary>True if <paramref name="name"/> is bound (possibly to a not-yet-filled slot).</summary>
    public bool Contains(string name) => _bindings.ContainsKey(name);
}
