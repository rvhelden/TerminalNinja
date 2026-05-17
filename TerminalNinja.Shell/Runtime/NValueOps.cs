using System.Globalization;
using System.Text;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Runtime;

/// <summary>Arithmetic, comparison, and formatting operations on <see cref="NValue"/>s.</summary>
public static class NValueOps
{
    /// <summary>Structural equality on NinjaShell values.</summary>
    public static bool Equals(NValue a, NValue b)
    {
        return (a, b) switch
        {
            (NUnit, NUnit) => true,
            (NBool ab, NBool bb) => ab.Value == bb.Value,
            (NInt ai, NInt bi) => ai.Value == bi.Value,
            (NFloat af, NFloat bf) => af.Value == bf.Value,
            (NInt ai, NFloat bf) => (double)ai.Value == bf.Value,
            (NFloat af, NInt bi) => af.Value == (double)bi.Value,
            (NString s1, NString s2) => string.Equals(s1.Value, s2.Value, StringComparison.Ordinal),
            (NList l1, NList l2) => ListEquals(l1, l2),
            (NRecord r1, NRecord r2) => RecordEquals(r1, r2),
            _ => false,
        };
    }

    /// <summary>Total order on comparable values. Throws for incomparable pairs.</summary>
    public static int Compare(NValue a, NValue b)
    {
        switch (a, b)
        {
            case (NInt ai, NInt bi): return ai.Value.CompareTo(bi.Value);
            case (NFloat af, NFloat bf): return af.Value.CompareTo(bf.Value);
            case (NInt ai, NFloat bf): return ((double)ai.Value).CompareTo(bf.Value);
            case (NFloat af, NInt bi): return af.Value.CompareTo((double)bi.Value);
            case (NString s1, NString s2): return string.Compare(s1.Value, s2.Value, StringComparison.Ordinal);
            case (NBool b1, NBool b2): return b1.Value.CompareTo(b2.Value);
        }
        throw new EvaluatorException($"cannot compare {NValueDescriber.Describe(a)} and {NValueDescriber.Describe(b)}");
    }

    /// <summary>Numeric addition with int→float promotion; string + string is concatenation.</summary>
    public static NValue Add(NValue a, NValue b) => (a, b) switch
    {
        (NInt ai, NInt bi) => new NInt(ai.Value + bi.Value),
        (NFloat af, NFloat bf) => new NFloat(af.Value + bf.Value),
        (NInt ai, NFloat bf) => new NFloat((double)ai.Value + bf.Value),
        (NFloat af, NInt bi) => new NFloat(af.Value + (double)bi.Value),
        (NString s1, NString s2) => new NString(s1.Value + s2.Value),
        _ => throw new EvaluatorException($"'+' is not defined for {NValueDescriber.Describe(a)} and {NValueDescriber.Describe(b)}"),
    };

    /// <summary>Numeric subtraction with int→float promotion.</summary>
    public static NValue Sub(NValue a, NValue b) => (a, b) switch
    {
        (NInt ai, NInt bi) => new NInt(ai.Value - bi.Value),
        (NFloat af, NFloat bf) => new NFloat(af.Value - bf.Value),
        (NInt ai, NFloat bf) => new NFloat((double)ai.Value - bf.Value),
        (NFloat af, NInt bi) => new NFloat(af.Value - (double)bi.Value),
        _ => throw new EvaluatorException($"'-' is not defined for {NValueDescriber.Describe(a)} and {NValueDescriber.Describe(b)}"),
    };

    /// <summary>Numeric multiplication with int→float promotion.</summary>
    public static NValue Mul(NValue a, NValue b) => (a, b) switch
    {
        (NInt ai, NInt bi) => new NInt(ai.Value * bi.Value),
        (NFloat af, NFloat bf) => new NFloat(af.Value * bf.Value),
        (NInt ai, NFloat bf) => new NFloat((double)ai.Value * bf.Value),
        (NFloat af, NInt bi) => new NFloat(af.Value * (double)bi.Value),
        _ => throw new EvaluatorException($"'*' is not defined for {NValueDescriber.Describe(a)} and {NValueDescriber.Describe(b)}"),
    };

    /// <summary>Numeric division. Integer/integer rounds toward zero; mixed promotes to float.</summary>
    public static NValue Div(NValue a, NValue b) => (a, b) switch
    {
        (NInt ai, NInt bi) when bi.Value == 0 => throw new EvaluatorException("integer division by zero"),
        (NInt ai, NInt bi) => new NInt(ai.Value / bi.Value),
        (NFloat af, NFloat bf) => new NFloat(af.Value / bf.Value),
        (NInt ai, NFloat bf) => new NFloat((double)ai.Value / bf.Value),
        (NFloat af, NInt bi) => new NFloat(af.Value / (double)bi.Value),
        _ => throw new EvaluatorException($"'/' is not defined for {NValueDescriber.Describe(a)} and {NValueDescriber.Describe(b)}"),
    };

    /// <summary>How a value renders inside <c>$"{...}"</c> — primitives unquoted, lists/records use the standard printer.</summary>
    public static string FormatForInterpolation(NValue v)
    {
        switch (v)
        {
            case NUnit: return string.Empty;
            case NBool b: return b.Value ? "true" : "false";
            case NInt i: return i.Value.ToString(CultureInfo.InvariantCulture);
            case NFloat f: return f.Value.ToString(CultureInfo.InvariantCulture);
            case NString s: return s.Value;
            case NList list:
                {
                    var sb = new StringBuilder("[");
                    for (int i = 0; i < list.Items.Length; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(FormatForDisplay(list.Items[i]));
                    }
                    sb.Append(']');
                    return sb.ToString();
                }
            case NSeq seq:
                {
                    // Materialise to render — display is a sink. For unbounded
                    // sources the caller is expected to `take(N)` first.
                    var sb = new StringBuilder("[");
                    bool firstSeq = true;
                    foreach (var item in seq.Items)
                    {
                        if (!firstSeq) sb.Append(", ");
                        firstSeq = false;
                        sb.Append(FormatForDisplay(item));
                    }
                    sb.Append(']');
                    return sb.ToString();
                }
            case NRecord rec:
                {
                    var sb = new StringBuilder("{ ");
                    bool first = true;
                    foreach (var kv in rec.Fields)
                    {
                        if (!first) sb.Append(", ");
                        first = false;
                        sb.Append(kv.Key).Append(": ").Append(FormatForDisplay(kv.Value));
                    }
                    sb.Append(" }");
                    return sb.ToString();
                }
            case NFunc fn: return $"<fn:{fn.Arity}>";
            default: return v.ToString() ?? "()";
        }
    }

    /// <summary>How a value renders in a list/record body — strings get quoted, everything else as for interpolation.</summary>
    public static string FormatForDisplay(NValue v)
    {
        if (v is NString s) return "\"" + s.Value.Replace("\"", "\\\"") + "\"";
        return FormatForInterpolation(v);
    }

    private static bool ListEquals(NList a, NList b)
    {
        if (a.Items.Length != b.Items.Length) return false;
        for (int i = 0; i < a.Items.Length; i++)
            if (!Equals(a.Items[i], b.Items[i])) return false;
        return true;
    }

    private static bool RecordEquals(NRecord a, NRecord b)
    {
        if (a.Fields.Count != b.Fields.Count) return false;
        foreach (var kv in a.Fields)
        {
            if (!b.Fields.TryGetValue(kv.Key, out var other)) return false;
            if (!Equals(kv.Value, other)) return false;
        }
        return true;
    }
}

/// <summary>Type-name helper for evaluator error messages.</summary>
internal static class NValueDescriber
{
    public static string Describe(NValue v) => v switch
    {
        NUnit => "unit",
        NBool => "bool",
        NInt => "int",
        NFloat => "float",
        NString => "string",
        NList => "list",
        NRecord => "record",
        NVariant => "variant",
        NSeq => "seq",
        NFunc => "function",
        _ => v.GetType().Name,
    };
}
