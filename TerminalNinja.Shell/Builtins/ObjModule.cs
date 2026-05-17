using System.Collections.Immutable;
using System.Text;
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
            ("dump", Fn1("obj.dump", v => new NString(DumpString(v)))),
            ("def", Fn1("obj.def", v => new NString(DefString(v)))),
            ("pairs", Fn1("obj.pairs", Pairs)),
            ("from_pairs", Fn1("obj.from_pairs", FromPairs)),
            ("keys", Fn1("obj.keys", Keys)),
            ("values", Fn1("obj.values", Values)),
            ("from_rows", Fn1("obj.from_rows", FromRows)),
            ("to_rows", Fn1("obj.to_rows", ToRows)),
            ("columns", Fn1("obj.columns", Columns)),
            ("from_columns", Fn1("obj.from_columns", FromColumns)));
    }

    private static NValue Fn1(string name, Func<NValue, NValue> f)
        => new NFunc(args =>
        {
            if (args.Length != 1) throw new EvaluatorException($"{name} expects 1 argument, got {args.Length}");
            return f(args[0]);
        }, 1);

    /// <summary>Canonical NinjaShell type name.</summary>
    internal static NValue TypeOf(NValue v) => new NString(TypeName(v));

    private static string TypeName(NValue v) => v switch
    {
        NUnit => "unit",
        NBool => "bool",
        NInt => "int",
        NFloat => "float",
        NString => "string",
        NList => "list",
        NRecord => "record",
        NSeq => "seq",
        NVariant => "variant",
        NFunc => "fn",
        _ => v.GetType().Name,
    };

    private static NValue Size(NValue v) => v switch
    {
        NUnit => new NInt(0),
        NString s => new NInt(s.Value.Length),
        NList l => new NInt(l.Items.Length),
        NRecord r => new NInt(r.Fields.Count),
        NSeq s => new NInt(s.Items.LongCount()),
        _ => throw new EvaluatorException($"obj.size is not defined for {TypeName(v)}"),
    };

    /// <summary>Recursive structural pretty-printer with type annotations.</summary>
    internal static string DumpString(NValue v)
    {
        var sb = new StringBuilder();
        WriteDump(sb, v, indent: 0);
        return sb.ToString();
    }

    private static void WriteDump(StringBuilder sb, NValue v, int indent)
    {
        switch (v)
        {
            case NUnit:
                sb.Append("() :: unit");
                break;
            case NBool b:
                sb.Append(b.Value ? "true" : "false").Append(" :: bool");
                break;
            case NInt i:
                sb.Append(i.Value).Append(" :: int");
                break;
            case NFloat f:
                sb.Append(f.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(" :: float");
                break;
            case NString s:
                sb.Append('"').Append(s.Value.Replace("\"", "\\\"")).Append('"').Append(" :: string");
                break;
            case NList list:
                {
                    if (list.Items.Length == 0) { sb.Append("[] :: list"); break; }
                    sb.Append("[\n");
                    for (int i = 0; i < list.Items.Length; i++)
                    {
                        AppendIndent(sb, indent + 1);
                        WriteDump(sb, list.Items[i], indent + 1);
                        if (i < list.Items.Length - 1) sb.Append(',');
                        sb.Append('\n');
                    }
                    AppendIndent(sb, indent);
                    sb.Append("] :: list");
                    break;
                }
            case NRecord rec:
                {
                    if (rec.Fields.Count == 0) { sb.Append("{} :: record"); break; }
                    sb.Append("{\n");
                    int i = 0;
                    foreach (var kv in rec.Fields)
                    {
                        AppendIndent(sb, indent + 1);
                        sb.Append(kv.Key).Append(": ");
                        WriteDump(sb, kv.Value, indent + 1);
                        if (i < rec.Fields.Count - 1) sb.Append(',');
                        sb.Append('\n');
                        i++;
                    }
                    AppendIndent(sb, indent);
                    sb.Append("} :: record");
                    break;
                }
            case NSeq seq:
                {
                    var items = seq.Items.ToList();
                    if (items.Count == 0) { sb.Append("[] :: seq"); break; }
                    sb.Append("[\n");
                    for (int i = 0; i < items.Count; i++)
                    {
                        AppendIndent(sb, indent + 1);
                        WriteDump(sb, items[i], indent + 1);
                        if (i < items.Count - 1) sb.Append(',');
                        sb.Append('\n');
                    }
                    AppendIndent(sb, indent);
                    sb.Append("] :: seq");
                    break;
                }
            case NVariant variant:
                {
                    sb.Append(variant.Tag);
                    if (variant.Items.Length > 0)
                    {
                        sb.Append('(');
                        for (int i = 0; i < variant.Items.Length; i++)
                        {
                            if (i > 0) sb.Append(", ");
                            WriteDump(sb, variant.Items[i], indent);
                        }
                        sb.Append(')');
                    }
                    sb.Append(" :: variant");
                    break;
                }
            case NFunc fn:
                sb.Append("<fn:").Append(fn.Arity).Append("> :: fn");
                break;
            default:
                sb.Append('?').Append(v.GetType().Name).Append('?');
                break;
        }
    }

    /// <summary>Schema-only inspector: returns shape, not data.</summary>
    internal static string DefString(NValue v)
    {
        switch (v)
        {
            case NRecord rec:
                {
                    if (rec.Fields.Count == 0) return "record { }";
                    var sb = new StringBuilder("record {\n");
                    int i = 0;
                    foreach (var kv in rec.Fields)
                    {
                        sb.Append("  ").Append(kv.Key).Append(": ").Append(TypeName(kv.Value));
                        if (i < rec.Fields.Count - 1) sb.Append(',');
                        sb.Append('\n');
                        i++;
                    }
                    sb.Append('}');
                    return sb.ToString();
                }
            case NList list when list.Items.Length > 0:
                {
                    string elementType = TypeName(list.Items[0]);
                    bool uniform = true;
                    for (int i = 1; i < list.Items.Length; i++)
                        if (TypeName(list.Items[i]) != elementType) { uniform = false; break; }
                    return uniform ? $"list[{elementType}]" : "list[mixed]";
                }
            case NList:
                return "list[]";
            case NFunc fn:
                return $"fn(arity={fn.Arity})";
            case NSeq:
                return "seq";
            case NVariant variant:
                return variant.Items.Length == 0
                    ? $"variant {variant.Tag}"
                    : $"variant {variant.Tag}({variant.Items.Length} items)";
            default:
                return TypeName(v);
        }
    }

    private static void AppendIndent(StringBuilder sb, int level)
    {
        for (int i = 0; i < level; i++) sb.Append("  ");
    }

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

        var headers = ExpectUniformRecordKeys(items, "obj.to_rows");
        var rows = ImmutableArray.CreateBuilder<NValue>(items.Length + 1);

        var headerRow = ImmutableArray.CreateBuilder<NValue>(headers.Length);
        foreach (var h in headers) headerRow.Add(new NString(h));
        rows.Add(new NList(headerRow.MoveToImmutable()));

        foreach (var item in items)
        {
            if (item is not NRecord rec) throw new EvaluatorException("obj.to_rows: non-record item slipped past uniformity check");
            var row = ImmutableArray.CreateBuilder<NValue>(headers.Length);
            foreach (var h in headers) row.Add(rec.Fields[h]);
            rows.Add(new NList(row.MoveToImmutable()));
        }
        return new NList(rows.MoveToImmutable());
    }

    private static NValue Columns(NValue v)
    {
        var items = MaterialiseList(v, "obj.columns");
        if (items.Length == 0) return new NRecord(ImmutableSortedDictionary<string, NValue>.Empty.WithComparers(StringComparer.Ordinal));

        var headers = ExpectUniformRecordKeys(items, "obj.columns");
        var columns = ImmutableSortedDictionary.CreateBuilder<string, NValue>(StringComparer.Ordinal);
        foreach (var h in headers)
        {
            var col = ImmutableArray.CreateBuilder<NValue>(items.Length);
            foreach (var item in items)
            {
                if (item is not NRecord rec) throw new EvaluatorException("obj.columns: non-record item slipped past uniformity check");
                col.Add(rec.Fields[h]);
            }
            columns[h] = new NList(col.MoveToImmutable());
        }
        return new NRecord(columns.ToImmutable());
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

    private static string[] ExpectUniformRecordKeys(ImmutableArray<NValue> items, string op)
    {
        if (items[0] is not NRecord first)
            throw new EvaluatorException($"{op}: items must be records, item 0 is {TypeName(items[0])}");
        var headers = first.Fields.Keys.ToArray();
        var headerSet = new HashSet<string>(headers, StringComparer.Ordinal);
        for (int i = 1; i < items.Length; i++)
        {
            if (items[i] is not NRecord rec)
                throw new EvaluatorException($"{op}: items must be records, item {i} is {TypeName(items[i])}");
            if (rec.Fields.Count != headers.Length || !headerSet.SetEquals(rec.Fields.Keys))
                throw new EvaluatorException($"{op}: item {i} has a different key set than item 0 (not a uniform table)");
        }
        return headers;
    }
}
