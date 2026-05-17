using System.Collections.Immutable;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Builtins;

/// <summary>
/// First-order pipeline builtins. Streaming ops (<c>where</c>, <c>select</c>,
/// <c>take</c>, <c>skip</c>, <c>head</c>) return <see cref="NSeq"/> backed by
/// yield-generators — they pull from the input on demand. Sinks
/// (<c>count</c>, <c>fold</c>, <c>sort</c>, <c>distinct</c>, <c>tail</c>,
/// <c>each</c>) consume the input fully. The <c>materialize</c> builtin
/// forces an <see cref="NSeq"/> back to an <see cref="NList"/>.
/// Every op takes the sequence as its first argument so it composes with
/// the <c>|</c> operator.
/// </summary>
public static class PipelineOps
{
    /// <summary>Register the pipeline builtins into <paramref name="b"/>.</summary>
    public static void Register(ImmutableDictionary<string, NValue>.Builder b)
    {
        b["where"] = NFunc2("where", Where);
        b["select"] = NFunc2("select", Select);
        b["each"] = NFunc2("each", Each);
        b["fold"] = NFunc3("fold", Fold);
        b["take"] = NFunc2("take", Take);
        b["skip"] = NFunc2("skip", Skip);
        b["count"] = NFunc1("count", Count);
        b["sort"] = NFunc1("sort", Sort);
        b["distinct"] = NFunc1("distinct", Distinct);
        b["head"] = NFunc1("head", Head);
        b["tail"] = NFunc1("tail", Tail);
        b["materialize"] = NFunc1("materialize", Materialize);
    }

    private static NValue NFunc1(string name, Func<NValue, NValue> f)
        => new NFunc(args =>
        {
            if (args.Length != 1) throw new EvaluatorException($"{name} expects 1 argument, got {args.Length}");
            return f(args[0]);
        }, 1);

    private static NValue NFunc2(string name, Func<NValue, NValue, NValue> f)
        => new NFunc(args =>
        {
            if (args.Length != 2) throw new EvaluatorException($"{name} expects 2 arguments, got {args.Length}");
            return f(args[0], args[1]);
        }, 2);

    private static NValue NFunc3(string name, Func<NValue, NValue, NValue, NValue> f)
        => new NFunc(args =>
        {
            if (args.Length != 3) throw new EvaluatorException($"{name} expects 3 arguments, got {args.Length}");
            return f(args[0], args[1], args[2]);
        }, 3);

    /// <summary>
    /// Pull items from <paramref name="v"/> regardless of whether it's an
    /// already-materialised <see cref="NList"/> or a lazy <see cref="NSeq"/>.
    /// </summary>
    internal static IEnumerable<NValue> AsEnumerable(NValue v, string op)
    {
        if (v is NList l) return l.Items;
        if (v is NSeq s) return s.Items;
        throw new EvaluatorException($"'{op}' requires a list or sequence, got {DescribeShort(v)}");
    }

    private static NFunc AsFn(NValue v, string op)
    {
        if (v is NFunc f) return f;
        throw new EvaluatorException($"'{op}' requires a function, got {DescribeShort(v)}");
    }

    private static NValue Where(NValue seq, NValue predicate)
    {
        var source = AsEnumerable(seq, "where");
        var pred = AsFn(predicate, "where");
        return new NSeq(WhereImpl(source, pred));
    }

    private static IEnumerable<NValue> WhereImpl(IEnumerable<NValue> source, NFunc pred)
    {
        foreach (var item in source)
        {
            var r = pred.Apply(new[] { item });
            if (r is not NBool nb)
                throw new EvaluatorException("'where' predicate must return a bool");
            if (nb.Value) yield return item;
        }
    }

    private static NValue Select(NValue seq, NValue projection)
    {
        var source = AsEnumerable(seq, "select");
        var proj = AsFn(projection, "select");
        return new NSeq(SelectImpl(source, proj));
    }

    private static IEnumerable<NValue> SelectImpl(IEnumerable<NValue> source, NFunc proj)
    {
        foreach (var item in source)
            yield return proj.Apply(new[] { item });
    }

    private static NValue Each(NValue seq, NValue action)
    {
        var source = AsEnumerable(seq, "each");
        var act = AsFn(action, "each");
        foreach (var item in source)
            act.Apply(new[] { item });
        return NUnit.Instance;
    }

    private static NValue Fold(NValue seq, NValue initial, NValue combiner)
    {
        var source = AsEnumerable(seq, "fold");
        var comb = AsFn(combiner, "fold");
        var acc = initial;
        foreach (var item in source)
            acc = comb.Apply(new[] { acc, item });
        return acc;
    }

    private static NValue Take(NValue seq, NValue n)
    {
        var source = AsEnumerable(seq, "take");
        if (n is not NInt ni) throw new EvaluatorException("'take' count must be int");
        return new NSeq(TakeImpl(source, ni.Value));
    }

    private static IEnumerable<NValue> TakeImpl(IEnumerable<NValue> source, long n)
    {
        if (n <= 0) yield break;
        long taken = 0;
        foreach (var item in source)
        {
            yield return item;
            taken++;
            if (taken >= n) yield break;
        }
    }

    private static NValue Skip(NValue seq, NValue n)
    {
        var source = AsEnumerable(seq, "skip");
        if (n is not NInt ni) throw new EvaluatorException("'skip' count must be int");
        return new NSeq(SkipImpl(source, ni.Value));
    }

    private static IEnumerable<NValue> SkipImpl(IEnumerable<NValue> source, long n)
    {
        long skipped = 0;
        foreach (var item in source)
        {
            if (skipped < n) { skipped++; continue; }
            yield return item;
        }
    }

    private static NValue Count(NValue seq)
    {
        var source = AsEnumerable(seq, "count");
        long n = 0;
        foreach (var _ in source) n++;
        return new NInt(n);
    }

    private static NValue Sort(NValue seq)
    {
        var arr = AsEnumerable(seq, "sort").ToArray();
        Array.Sort(arr, NValueOps.Compare);
        return new NList(ImmutableArray.Create(arr));
    }

    private static NValue Distinct(NValue seq)
    {
        var source = AsEnumerable(seq, "distinct");
        var b = ImmutableArray.CreateBuilder<NValue>();
        foreach (var item in source)
        {
            bool seen = false;
            foreach (var kept in b)
            {
                if (NValueOps.Equals(item, kept)) { seen = true; break; }
            }
            if (!seen) b.Add(item);
        }
        return new NList(b.ToImmutable());
    }

    private static NValue Head(NValue seq)
    {
        foreach (var item in AsEnumerable(seq, "head"))
            return item;
        throw new EvaluatorException("'head' on empty sequence");
    }

    private static NValue Tail(NValue seq)
    {
        var source = AsEnumerable(seq, "tail");
        bool sawAny = false;
        var b = ImmutableArray.CreateBuilder<NValue>();
        foreach (var item in source)
        {
            if (!sawAny) { sawAny = true; continue; }
            b.Add(item);
        }
        if (!sawAny) throw new EvaluatorException("'tail' on empty sequence");
        return new NList(b.ToImmutable());
    }

    private static NValue Materialize(NValue v)
    {
        if (v is NList l) return l;
        if (v is NSeq s) return new NList(ImmutableArray.CreateRange(s.Items));
        throw new EvaluatorException($"'materialize' requires a list or sequence, got {DescribeShort(v)}");
    }

    private static string DescribeShort(NValue v) => v switch
    {
        NUnit => "unit",
        NBool => "bool",
        NInt => "int",
        NFloat => "float",
        NString => "string",
        NList => "list",
        NRecord => "record",
        NSeq => "seq",
        NFunc => "function",
        _ => v.GetType().Name,
    };
}
