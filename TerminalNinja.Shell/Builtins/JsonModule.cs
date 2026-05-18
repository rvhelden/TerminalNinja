using System.Buffers;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using TerminalNinja.Shell.PowerShell;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Builtins;

/// <summary>
/// The <c>json</c> module — parse and stringify JSON via <see cref="Utf8JsonReader"/>
/// / <see cref="Utf8JsonWriter"/>. No reflection, no <c>JsonSerializer.Deserialize</c>,
/// stays AOT-clean under <c>TreatWarningsAsErrors=true</c>.
/// </summary>
public static class JsonModule
{
    /// <summary>Register the <c>json</c> module.</summary>
    public static void Register(ImmutableDictionary<string, NValue>.Builder b)
    {
        BuiltinRegistry.RegisterModule(b, "json",
            ("parse", new NFunc(Parse, 1)),
            ("stringify", new NFunc(Stringify, -1)));
    }

    private static NValue Parse(NValue[] args)
    {
        if (args.Length != 1) throw new EvaluatorException($"json.parse expects 1 argument, got {args.Length}");
        if (args[0] is not NString s) throw new EvaluatorException("json.parse: input must be a string");
        try
        {
            return JsonToNValue.Parse(s.Value);
        }
        catch (Exception ex) when (ex is not EvaluatorException)
        {
            throw new EvaluatorException($"json.parse: {ex.Message}", ex);
        }
    }

    private static NValue Stringify(NValue[] args)
    {
        if (args.Length is < 1 or > 2)
            throw new EvaluatorException($"json.stringify expects 1 or 2 arguments, got {args.Length}");

        int indent = 0;
        if (args.Length == 2)
        {
            if (args[1] is not NRecord opts) throw new EvaluatorException("json.stringify: options must be a record");
            if (opts.Fields.TryGetValue("indent", out var indentVal))
            {
                if (indentVal is not NInt ni) throw new EvaluatorException("json.stringify: 'indent' must be an int");
                if (ni.Value < 0) throw new EvaluatorException("json.stringify: 'indent' must be non-negative");
                indent = (int)Math.Min(ni.Value, 32);
            }
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions
        {
            Indented = indent > 0,
            IndentSize = indent,
        }))
        {
            WriteValue(writer, args[0]);
        }
        return new NString(Encoding.UTF8.GetString(buffer.WrittenSpan));
    }

    private static void WriteValue(Utf8JsonWriter w, NValue v)
    {
        switch (v)
        {
            case NUnit:
                w.WriteNullValue();
                break;
            case NBool b:
                w.WriteBooleanValue(b.Value);
                break;
            case NInt i:
                w.WriteNumberValue(i.Value);
                break;
            case NFloat f:
                if (double.IsNaN(f.Value) || double.IsInfinity(f.Value))
                    throw new EvaluatorException($"json.stringify: cannot serialize {f.Value} (NaN/Infinity are not valid JSON)");
                w.WriteNumberValue(f.Value);
                break;
            case NString s:
                w.WriteStringValue(s.Value);
                break;
            case NList list:
                w.WriteStartArray();
                foreach (var item in list.Items) WriteValue(w, item);
                w.WriteEndArray();
                break;
            case NSeq seq:
                w.WriteStartArray();
                foreach (var item in seq.Items) WriteValue(w, item);
                w.WriteEndArray();
                break;
            case NRecord rec:
                w.WriteStartObject();
                foreach (var kv in rec.Fields)
                {
                    w.WritePropertyName(kv.Key);
                    WriteValue(w, kv.Value);
                }
                w.WriteEndObject();
                break;
            case NVariant variant:
                throw new EvaluatorException($"json.stringify: NVariant '{variant.Tag}' has no canonical JSON form — convert to a record first");
            case NFunc:
                throw new EvaluatorException("json.stringify: functions cannot be serialized to JSON");
            default:
                throw new EvaluatorException($"json.stringify: unhandled value type {v.GetType().Name}");
        }
    }
}
