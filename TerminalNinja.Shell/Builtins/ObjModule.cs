using System.Collections.Immutable;
using System.Text;
using TerminalNinja.Shell.Repl;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Builtins;

/// <summary>
/// The <c>obj</c> module — generic value-inspection builtins for the REPL and
/// scripts. <c>type</c> reports the canonical type name, <c>size</c> measures
/// container values, <c>dump</c> renders data structure with type annotations,
/// and <c>def</c> renders the shape (keys/types or function arity) without the data.
/// </summary>
public static class ObjModule
{
    /// <summary>Register the <c>obj</c> module into the default-environment builder.</summary>
    public static void Register(ImmutableDictionary<string, NValue>.Builder b)
    {
        BuiltinRegistry.RegisterModule(b, "obj",
            ("type", Fn1("obj.type", TypeOf)),
            ("size", Fn1("obj.size", Size)),
            ("dump", new NFunc(Dump, -1)),
            ("table", Fn1("obj.table", Table)),
            ("def", Fn1("obj.def", v => new NString(DefString(v)))),
            ("pairs", Fn1("obj.pairs", Pairs)),
            ("from_pairs", Fn1("obj.from_pairs", FromPairs)),
            ("keys", Fn1("obj.keys", Keys)),
            ("values", Fn1("obj.values", Values)),
            ("from_rows", Fn1("obj.from_rows", FromRows)),
            ("to_rows", Fn1("obj.to_rows", ToRows)),
            ("columns", Fn1("obj.columns", Columns)),
            ("from_columns", Fn1("obj.from_columns", FromColumns)),
            ("normalize", new NFunc(Normalize, -1)));
    }

    private static NValue Fn1(string name, Func<NValue, NValue> f)
        => new NFunc(args =>
        {
            if (args.Length != 1) throw new EvaluatorException($"{name} expects 1 argument, got {args.Length}");
            return f(args[0]);
        }, 1);

    /// <summary>Canonical NinjaShell type name. Delegates to <see cref="ValueFormatter.TypeName"/>.</summary>
    internal static NValue TypeOf(NValue v) => new NString(ValueFormatter.TypeName(v));

    private static string TypeName(NValue v) => ValueFormatter.TypeName(v);

    private static NValue Size(NValue v) => v switch
    {
        NUnit => new NInt(0),
        NString s => new NInt(s.Value.Length),
        NList l => new NInt(l.Items.Length),
        NRecord r => new NInt(r.Fields.Count),
        NSeq s => new NInt(s.Items.LongCount()),
        _ => throw new EvaluatorException($"obj.size is not defined for {ValueFormatter.TypeName(v)}"),
    };

    /// <summary>
    /// Vertical property-table renderer used by <c>obj.dump(v[, depth])</c>. Records
    /// render as <c>key | value</c> rows; depth controls how many levels of nested
    /// records/lists expand before collapsing to <c>record (N fields)</c> /
    /// <c>list (N items)</c>. Defaults to depth 2, which expands the top record and
    /// any single layer of nested records.
    /// </summary>
    private static NValue Dump(NValue[] args)
    {
        if (args.Length is < 1 or > 2)
            throw new EvaluatorException($"obj.dump expects 1 or 2 arguments, got {args.Length}");
        int depth = 2;
        if (args.Length == 2)
        {
            if (args[1] is not NInt d) throw new EvaluatorException("obj.dump: depth must be an int");
            depth = (int)d.Value;
            if (depth < 0) throw new EvaluatorException("obj.dump: depth must be non-negative");
        }
        return new NString(ValueFormatter.DumpTable(args[0], depth));
    }

    /// <summary>
    /// Force the aligned record-table format used by <c>Printer.Format</c> for any
    /// list/sequence of records. Useful when the default printer chose a different
    /// representation (single record, sequence sink, etc.) and you want the table
    /// rendering as a string for piping or println.
    /// </summary>
    private static NValue Table(NValue v)
    {
        if (v is NList list)
        {
            if (Printer.IsStringListShaped(list)) return new NString(Printer.FormatStringList(list));
            return new NString(Printer.FormatRecordTable(list));
        }
        if (v is NSeq seq)
        {
            var materialised = new NList(ImmutableArray.CreateRange(seq.Items));
            if (Printer.IsStringListShaped(materialised)) return new NString(Printer.FormatStringList(materialised));
            return new NString(Printer.FormatRecordTable(materialised));
        }
        if (v is NRecord rec)
        {
            // A single record renders as a one-row table — useful for symmetry with
            // multi-record cases.
            var single = new NList(ImmutableArray.Create<NValue>(rec));
            return new NString(Printer.FormatRecordTable(single));
        }
        throw new EvaluatorException($"obj.table requires a list, seq, or record, got {TypeName(v)}");
    }

    /// <summary>Schema-only inspector. Delegates to <see cref="ValueFormatter.Def"/>.</summary>
    internal static string DefString(NValue v) => ValueFormatter.Def(v);

    // ─── Record ↔ pairs ─────────────────────────────────────────────────────

    private static NValue Pairs(NValue v)
    {
        if (v is not NRecord rec) throw new EvaluatorException($"obj.pairs requires a record, got {TypeName(v)}");
        var b = ImmutableArray.CreateBuilder<NValue>(rec.Fields.Count);
        foreach (var kv in rec.Fields)
        {
            var pair = ImmutableSortedDictionary.CreateBuilder<string, NValue>(StringComparer.Ordinal);
            pair["Key"] = new NString(kv.Key);
            pair["Value"] = kv.Value;
            b.Add(new NRecord(pair.ToImmutable()));
        }
        return new NList(b.MoveToImmutable());
    }

    private static NValue FromPairs(NValue v)
    {
        var items = AsIterable(v, "obj.from_pairs");
        var b = ImmutableSortedDictionary.CreateBuilder<string, NValue>(StringComparer.Ordinal);
        int i = 0;
        foreach (var item in items)
        {
            if (item is not NRecord rec)
                throw new EvaluatorException($"obj.from_pairs: item {i} is {TypeName(item)}, expected a record with Key/Value");
            if (!rec.Fields.TryGetValue("Key", out var keyVal))
                throw new EvaluatorException($"obj.from_pairs: item {i} has no 'Key' field");
            if (keyVal is not NString keyStr)
                throw new EvaluatorException($"obj.from_pairs: item {i} 'Key' must be a string, got {TypeName(keyVal)}");
            if (!rec.Fields.TryGetValue("Value", out var valueVal))
                throw new EvaluatorException($"obj.from_pairs: item {i} has no 'Value' field");
            if (b.ContainsKey(keyStr.Value))
                throw new EvaluatorException($"obj.from_pairs: duplicate key '{keyStr.Value}'");
            b[keyStr.Value] = valueVal;
            i++;
        }
        return new NRecord(b.ToImmutable());
    }

    private static NValue Keys(NValue v)
    {
        if (v is not NRecord rec) throw new EvaluatorException($"obj.keys requires a record, got {TypeName(v)}");
        var b = ImmutableArray.CreateBuilder<NValue>(rec.Fields.Count);
        foreach (var key in rec.Fields.Keys) b.Add(new NString(key));
        return new NList(b.MoveToImmutable());
    }

    private static NValue Values(NValue v)
    {
        if (v is not NRecord rec) throw new EvaluatorException($"obj.values requires a record, got {TypeName(v)}");
        var b = ImmutableArray.CreateBuilder<NValue>(rec.Fields.Count);
        foreach (var val in rec.Fields.Values) b.Add(val);
        return new NList(b.MoveToImmutable());
    }

    // ─── Table ↔ rows / columns ─────────────────────────────────────────────

    private static NValue FromRows(NValue v)
    {
        if (v is not NList outer) throw new EvaluatorException($"obj.from_rows requires a list of rows, got {TypeName(v)}");
        if (outer.Items.Length == 0) return new NList(ImmutableArray<NValue>.Empty);

        if (outer.Items[0] is not NList headerRow)
            throw new EvaluatorException("obj.from_rows: first row (headers) must be a list");
        var headers = new string[headerRow.Items.Length];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < headers.Length; i++)
        {
            if (headerRow.Items[i] is not NString hs)
                throw new EvaluatorException($"obj.from_rows: header at index {i} must be a string, got {TypeName(headerRow.Items[i])}");
            if (!seen.Add(hs.Value))
                throw new EvaluatorException($"obj.from_rows: duplicate header '{hs.Value}'");
            headers[i] = hs.Value;
        }

        var rows = ImmutableArray.CreateBuilder<NValue>(outer.Items.Length - 1);
        for (int r = 1; r < outer.Items.Length; r++)
        {
            if (outer.Items[r] is not NList row)
                throw new EvaluatorException($"obj.from_rows: row {r} must be a list, got {TypeName(outer.Items[r])}");
            if (row.Items.Length != headers.Length)
                throw new EvaluatorException($"obj.from_rows: row {r} has {row.Items.Length} cell(s), expected {headers.Length} to match headers");
            var rec = ImmutableSortedDictionary.CreateBuilder<string, NValue>(StringComparer.Ordinal);
            for (int c = 0; c < headers.Length; c++)
                rec[headers[c]] = row.Items[c];
            rows.Add(new NRecord(rec.ToImmutable()));
        }
        return new NList(rows.MoveToImmutable());
    }

    private static NValue ToRows(NValue v)
    {
        var items = MaterialiseList(v, "obj.to_rows");
        if (items.Length == 0) return new NList(ImmutableArray<NValue>.Empty);

        var headers = CollectKeyUnion(items, "obj.to_rows");
        var rows = ImmutableArray.CreateBuilder<NValue>(items.Length + 1);

        var headerRow = ImmutableArray.CreateBuilder<NValue>(headers.Length);
        foreach (var h in headers) headerRow.Add(new NString(h));
        rows.Add(new NList(headerRow.MoveToImmutable()));

        foreach (var item in items)
        {
            if (item is not NRecord rec) throw new EvaluatorException("obj.to_rows: items must be records");
            var row = ImmutableArray.CreateBuilder<NValue>(headers.Length);
            foreach (var h in headers)
                row.Add(rec.Fields.TryGetValue(h, out var val) ? val : NUnit.Instance);
            rows.Add(new NList(row.MoveToImmutable()));
        }
        return new NList(rows.MoveToImmutable());
    }

    private static NValue Columns(NValue v)
    {
        var items = MaterialiseList(v, "obj.columns");
        if (items.Length == 0) return new NRecord(ImmutableSortedDictionary<string, NValue>.Empty.WithComparers(StringComparer.Ordinal));

        var headers = CollectKeyUnion(items, "obj.columns");
        var columns = ImmutableSortedDictionary.CreateBuilder<string, NValue>(StringComparer.Ordinal);
        foreach (var h in headers)
        {
            var col = ImmutableArray.CreateBuilder<NValue>(items.Length);
            foreach (var item in items)
            {
                if (item is not NRecord rec) throw new EvaluatorException("obj.columns: items must be records");
                col.Add(rec.Fields.TryGetValue(h, out var val) ? val : NUnit.Instance);
            }
            columns[h] = new NList(col.MoveToImmutable());
        }
        return new NRecord(columns.ToImmutable());
    }

    private static NValue Normalize(NValue[] args)
    {
        if (args.Length is < 1 or > 2)
            throw new EvaluatorException($"obj.normalize expects 1 or 2 arguments, got {args.Length}");

        var items = MaterialiseList(args[0], "obj.normalize");
        if (items.Length == 0) return new NList(ImmutableArray<NValue>.Empty);

        NRecord? defaults = null;
        if (args.Length == 2)
        {
            if (args[1] is not NRecord d)
                throw new EvaluatorException("obj.normalize: defaults must be a record");
            defaults = d;
        }

        // Schema = union of observed keys + defaults keys (defaults first if provided
        // so synthesised columns appear in the user-specified order, then any extras
        // in first-seen order).
        var schema = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (defaults != null)
            foreach (var k in defaults.Fields.Keys)
                if (seen.Add(k)) schema.Add(k);
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] is not NRecord rec)
                throw new EvaluatorException($"obj.normalize: item {i} is not a record");
            foreach (var k in rec.Fields.Keys)
                if (seen.Add(k)) schema.Add(k);
        }

        var rows = ImmutableArray.CreateBuilder<NValue>(items.Length);
        foreach (var item in items)
        {
            if (item is not NRecord rec) throw new EvaluatorException("obj.normalize: non-record item slipped past type check");
            var b = ImmutableSortedDictionary.CreateBuilder<string, NValue>(StringComparer.Ordinal);
            foreach (var col in schema)
            {
                NValue value = NUnit.Instance;
                if (rec.Fields.TryGetValue(col, out var existing) && existing is not NUnit)
                    value = existing;
                else if (defaults != null && defaults.Fields.TryGetValue(col, out var dflt))
                    value = dflt;
                b[col] = value;
            }
            rows.Add(new NRecord(b.ToImmutable()));
        }
        return new NList(rows.MoveToImmutable());
    }

    private static NValue FromColumns(NValue v)
    {
        if (v is not NRecord rec) throw new EvaluatorException($"obj.from_columns requires a record of lists, got {TypeName(v)}");
        if (rec.Fields.Count == 0) return new NList(ImmutableArray<NValue>.Empty);

        var headers = rec.Fields.Keys.ToArray();
        var colArrays = new ImmutableArray<NValue>[headers.Length];
        int rowCount = -1;
        for (int i = 0; i < headers.Length; i++)
        {
            if (rec.Fields[headers[i]] is not NList col)
                throw new EvaluatorException($"obj.from_columns: column '{headers[i]}' must be a list, got {TypeName(rec.Fields[headers[i]])}");
            colArrays[i] = col.Items;
            if (rowCount == -1) rowCount = col.Items.Length;
            else if (col.Items.Length != rowCount)
                throw new EvaluatorException($"obj.from_columns: column '{headers[i]}' has {col.Items.Length} row(s), expected {rowCount}");
        }

        var rows = ImmutableArray.CreateBuilder<NValue>(rowCount);
        for (int r = 0; r < rowCount; r++)
        {
            var row = ImmutableSortedDictionary.CreateBuilder<string, NValue>(StringComparer.Ordinal);
            for (int c = 0; c < headers.Length; c++)
                row[headers[c]] = colArrays[c][r];
            rows.Add(new NRecord(row.ToImmutable()));
        }
        return new NList(rows.MoveToImmutable());
    }

    private static IEnumerable<NValue> AsIterable(NValue v, string op) => v switch
    {
        NList l => l.Items,
        NSeq s => s.Items,
        _ => throw new EvaluatorException($"{op} requires a list or sequence, got {TypeName(v)}"),
    };

    private static ImmutableArray<NValue> MaterialiseList(NValue v, string op)
    {
        if (v is NList l) return l.Items;
        if (v is NSeq s) return ImmutableArray.CreateRange(s.Items);
        throw new EvaluatorException($"{op} requires a list or sequence, got {TypeName(v)}");
    }

    /// <summary>
    /// Compute the union of record keys across <paramref name="items"/>, preserving
    /// first-seen order. Throws if any item is not a record.
    /// </summary>
    private static string[] CollectKeyUnion(ImmutableArray<NValue> items, string op)
    {
        var order = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] is not NRecord rec)
                throw new EvaluatorException($"{op}: items must be records, item {i} is {TypeName(items[i])}");
            foreach (var k in rec.Fields.Keys)
                if (seen.Add(k)) order.Add(k);
        }
        return order.ToArray();
    }
}
