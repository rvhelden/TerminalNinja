using System.Collections.Immutable;

namespace TerminalNinja.Shell.Values;

/// <summary>Canonical "no value" singleton, equivalent to F#'s <c>unit</c>.</summary>
public sealed record NUnit
{
    /// <summary>The singleton instance — there is exactly one <see cref="NUnit"/>.</summary>
    public static readonly NUnit Instance = new();
}

/// <summary>Boolean scalar.</summary>
public sealed record NBool(bool Value);

/// <summary>64-bit signed integer scalar.</summary>
public sealed record NInt(long Value);

/// <summary>Double-precision floating-point scalar.</summary>
public sealed record NFloat(double Value);

/// <summary>Immutable UTF-16 string scalar.</summary>
public sealed record NString(string Value);

/// <summary>Eagerly-materialised, ordered sequence of NinjaShell values.</summary>
public sealed record NList(ImmutableArray<NValue> Items);

/// <summary>
/// Structural, anonymous record — keys are arbitrary strings (sorted for canonical
/// equality), values are <see cref="NValue"/>s.
/// </summary>
public sealed record NRecord(ImmutableSortedDictionary<string, NValue> Fields);

/// <summary>
/// Language-level tagged-union value (named <c>NVariant</c> to avoid collision with
/// the C# 15 <c>union</c> keyword used for <see cref="NValue"/> itself).
/// </summary>
public sealed record NVariant(string Tag, ImmutableArray<NValue> Items);

/// <summary>
/// Lazy / streaming sequence of values. Produced by range literals and by the
/// streaming pipeline operators (<c>where</c>, <c>select</c>, <c>take</c>,
/// <c>skip</c>, <c>head</c>). The backing <see cref="Items"/> is an
/// <see cref="IEnumerable{T}"/> — each call to <c>GetEnumerator</c> starts a
/// fresh walk, so the value is safely re-iterable, but the upstream chain is
/// recomputed per pass. Iterate once for cheap, iterate twice for double cost.
/// </summary>
public sealed record NSeq(IEnumerable<NValue> Items);

/// <summary>
/// First-class function value. <see cref="Arity"/> of -1 indicates variadic.
/// </summary>
public sealed record NFunc(Func<NValue[], NValue> Apply, int Arity);

/// <summary>
/// The single discriminated-union value type used throughout the shell — every
/// NinjaShell expression evaluates to an <see cref="NValue"/>. Built on the C# 15
/// <c>union</c> keyword: case types convert implicitly to <see cref="NValue"/> and
/// the compiler enforces exhaustive pattern matching.
/// </summary>
public union NValue(
    NUnit,
    NBool,
    NInt,
    NFloat,
    NString,
    NList,
    NRecord,
    NVariant,
    NSeq,
    NFunc);
