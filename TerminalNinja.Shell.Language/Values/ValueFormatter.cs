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
    /// Recursive structural pretty-print with <c>:: type</c> annotations.
    /// Identical to what <c>obj.dump</c> produces.
    /// </summary>
    public static string Dump(NValue v)
    {
        var sb = new StringBuilder();
        WriteDump(sb, v, indent: 0);
        return sb.ToString();
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
}
