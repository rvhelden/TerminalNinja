using System.Collections.Immutable;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Builtins;

/// <summary>
/// First-order pipeline builtins. Every operation takes the sequence as its
/// first argument so it composes with the <c>|</c> operator without any
/// special-casing in the parser.
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

    private static ImmutableArray<NValue> Items(NValue seq, string op)
    {
        if (seq is NList l) return l.Items;
        throw new EvaluatorException($"'{op}' requires a list, got {DescribeShort(seq)}");
    }

    private static NFunc AsFn(NValue v, string op)
    {
        if (v is NFunc f) return f;
        throw new EvaluatorException($"'{op}' requires a function, got {DescribeShort(v)}");
    }

    private static NValue Where(NValue seq, NValue predicate)
    {
        var items = Items(seq, "where");
        var pred = AsFn(predicate, "where");
        var b = ImmutableArray.CreateBuilder<NValue>();
        foreach (var item in items)
        {
            var r = pred.Apply(new[] { item });
            if (r is NBool nb && nb.Value) b.Add(item);
            else if (r is not NBool) throw new EvaluatorException("'where' predicate must return a bool");
        }
        return new NList(b.ToImmutable());
    }

    private static NValue Select(NValue seq, NValue projection)
    {
        var items = Items(seq, "select");
        var proj = AsFn(projection, "select");
        var b = ImmutableArray.CreateBuilder<NValue>(items.Length);
        foreach (var item in items)
            b.Add(proj.Apply(new[] { item }));
        return new NList(b.MoveToImmutable());
    }

    private static NValue Each(NValue seq, NValue action)
    {
        var items = Items(seq, "each");
        var act = AsFn(action, "each");
        foreach (var item in items)
            act.Apply(new[] { item });
        return NUnit.Instance;
    }

    private static NValue Fold(NValue seq, NValue initial, NValue combiner)
    {
        var items = Items(seq, "fold");
        var comb = AsFn(combiner, "fold");
        var acc = initial;
        foreach (var item in items)
            acc = comb.Apply(new[] { acc, item });
        return acc;
    }

    private static NValue Take(NValue seq, NValue n)
    {
        var items = Items(seq, "take");
        if (n is not NInt ni) throw new EvaluatorException("'take' count must be int");
        int count = (int)Math.Max(0, Math.Min(ni.Value, items.Length));
        if (count == items.Length) return seq;
        var b = ImmutableArray.CreateBuilder<NValue>(count);
        for (int i = 0; i < count; i++) b.Add(items[i]);
        return new NList(b.MoveToImmutable());
    }

    private static NValue Skip(NValue seq, NValue n)
    {
        var items = Items(seq, "skip");
        if (n is not NInt ni) throw new EvaluatorException("'skip' count must be int");
        int skip = (int)Math.Max(0, Math.Min(ni.Value, items.Length));
        if (skip == 0) return seq;
        var b = ImmutableArray.CreateBuilder<NValue>(items.Length - skip);
        for (int i = skip; i < items.Length; i++) b.Add(items[i]);
        return new NList(b.MoveToImmutable());
    }

    private static NValue Count(NValue seq)
    {
        var items = Items(seq, "count");
        return new NInt(items.Length);
    }

    private static NValue Sort(NValue seq)
    {
        var items = Items(seq, "sort");
        var arr = items.ToArray();
        Array.Sort(arr, (a, b) => NValueOps.Compare(a, b));
        return new NList(ImmutableArray.Create(arr));
    }

    private static NValue Distinct(NValue seq)
    {
        var items = Items(seq, "distinct");
        var b = ImmutableArray.CreateBuilder<NValue>();
        foreach (var item in items)
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
        var items = Items(seq, "head");
        if (items.Length == 0) throw new EvaluatorException("'head' on empty list");
        return items[0];
    }

    private static NValue Tail(NValue seq)
    {
        var items = Items(seq, "tail");
        if (items.Length == 0) throw new EvaluatorException("'tail' on empty list");
        var b = ImmutableArray.CreateBuilder<NValue>(items.Length - 1);
        for (int i = 1; i < items.Length; i++) b.Add(items[i]);
        return new NList(b.MoveToImmutable());
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
        NFunc => "function",
        _ => v.GetType().Name,
    };
}
