using System.Globalization;
using System.Text;

namespace TerminalNinja.Shell.Values;

/// <summary>
/// Pure value-to-string helpers. Lives in the Language project so tooling
/// (the LSP service, the REPL's hover) can format <see cref="NValue"/>s
/// without depending on the runtime. <c>obj.dump</c> and <c>obj.def</c> in
/// the runtime delegate to <see cref="Dump"/> and <see cref="Def"/> below.
/// </summary>
public static class ValueFormatter
{
    /// <summary>Canonical NinjaShell type name (matches <c>obj.type</c>).</summary>
    public static string TypeName(NValue v) => v switch
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

    /// <summary>
    /// Recursive structural pretty-print with <c>:: type</c> annotations. Used by
    /// hover previews (LSP) and by the REPL's mouse-hover tooltip. <c>obj.dump</c>
    /// no longer delegates here — it uses <see cref="DumpTable"/> for a vertical
    /// property-table view that's easier to read in the REPL.
    /// </summary>
    public static string Dump(NValue v)
    {
        var sb = new StringBuilder();
        WriteDump(sb, v, indent: 0);
        return sb.ToString();
    }

    /// <summary>
    /// Render <paramref name="v"/> as a vertical property table (the format used by
    /// <c>obj.dump</c>). Records become a 2-column <c>key | value</c> grid. Scalars
    /// render as their value with a trailing <c>:: type</c> annotation. When the
    /// recursion budget is exhausted, nested records collapse to
    /// <c>record (N fields)</c> and nested lists to <c>list (N items)</c> — only
    /// the type is shown, not the data.
    /// </summary>
    /// <param name="maxDepth">
    /// Maximum nesting depth to expand. <c>1</c> means render the top-level value
    /// but stop expanding nested records/lists. The default of <c>2</c> covers the
    /// common "show me one record + its inline lists" case.
    /// </param>
    public static string DumpTable(NValue v, int maxDepth = 2)
    {
        var sb = new StringBuilder();
        WriteDumpTable(sb, v, maxDepth, indent: 0);
        return sb.ToString().TrimEnd('\r', '\n');
    }

    /// <summary>
    /// Shape-only inspector. For records: keys and their types, no values.
    /// For lists: <c>list[elementType]</c> or <c>list[mixed]</c>. For functions:
    /// <c>fn(arity=N)</c>. Identical to <c>obj.def</c>.
    /// </summary>
    public static string Def(NValue v)
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
                sb.Append(f.Value.ToString(CultureInfo.InvariantCulture)).Append(" :: float");
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

    private static void AppendIndent(StringBuilder sb, int level)
    {
        for (int i = 0; i < level; i++) sb.Append("  ");
    }

    private static void WriteDumpTable(StringBuilder sb, NValue v, int remainingDepth, int indent)
    {
        switch (v)
        {
            case NRecord rec when rec.Fields.Count == 0:
                AppendIndent(sb, indent);
                sb.Append("{} :: record");
                return;

            case NRecord rec when remainingDepth <= 0:
                AppendIndent(sb, indent);
                sb.Append("record (").Append(rec.Fields.Count).Append(" fields)");
                return;

            case NRecord rec:
                {
                    int keyWidth = 0;
                    foreach (var k in rec.Fields.Keys)
                        if (k.Length > keyWidth) keyWidth = k.Length;

                    bool first = true;
                    foreach (var kv in rec.Fields)
                    {
                        if (!first) sb.AppendLine();
                        first = false;
                        AppendIndent(sb, indent);
                        sb.Append(kv.Key.PadRight(keyWidth)).Append(" | ");

                        switch (kv.Value)
                        {
                            case NRecord nr when nr.Fields.Count == 0:
                                sb.Append("{}");
                                break;
                            case NRecord nr when remainingDepth - 1 <= 0:
                                sb.Append("record (").Append(nr.Fields.Count).Append(" fields)");
                                break;
                            case NRecord nr:
                                sb.AppendLine();
                                WriteDumpTable(sb, nr, remainingDepth - 1, indent + 1);
                                break;
                            case NList nl when nl.Items.Length == 0:
                                sb.Append("[]");
                                break;
                            case NList nl when remainingDepth - 1 <= 0:
                                sb.Append("list (").Append(nl.Items.Length).Append(" items)");
                                break;
                            case NList nl:
                                sb.Append(FormatScalarLike(nl));
                                break;
                            default:
                                sb.Append(FormatScalarLike(kv.Value));
                                break;
                        }
                    }
                    return;
                }

            case NList list when list.Items.Length == 0:
                AppendIndent(sb, indent);
                sb.Append("[] :: list");
                return;

            case NList list when remainingDepth <= 0:
                AppendIndent(sb, indent);
                sb.Append("list (").Append(list.Items.Length).Append(" items)");
                return;

            case NList list:
                {
                    AppendIndent(sb, indent);
                    sb.Append('[');
                    for (int i = 0; i < list.Items.Length; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(FormatScalarLike(list.Items[i]));
                    }
                    sb.Append("] :: list (").Append(list.Items.Length).Append(" items)");
                    return;
                }

            default:
                AppendIndent(sb, indent);
                sb.Append(FormatScalarLike(v));
                sb.Append(" :: ").Append(TypeName(v));
                return;
        }
    }

    /// <summary>
    /// One-line value representation used inside DumpTable cells. Containers
    /// collapse to their type + size; scalars render as their literal form.
    /// </summary>
    private static string FormatScalarLike(NValue v) => v switch
    {
        NUnit => "()",
        NBool b => b.Value ? "true" : "false",
        NInt i => i.Value.ToString(CultureInfo.InvariantCulture),
        NFloat f => f.Value.ToString(CultureInfo.InvariantCulture),
        NString s => "\"" + s.Value.Replace("\"", "\\\"") + "\"",
        NFunc fn => $"<fn:{fn.Arity}>",
        NRecord r => $"record ({r.Fields.Count} fields)",
        NList l => $"list ({l.Items.Length} items)",
        NSeq => "seq",
        NVariant variant => variant.Items.Length == 0
            ? variant.Tag
            : $"{variant.Tag}({variant.Items.Length})",
        _ => v.ToString() ?? "?",
    };
}
