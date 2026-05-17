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
            ("def", Fn1("obj.def", v => new NString(DefString(v)))));
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
}
