using System.Buffers;
using System.Text;
using System.Text.Json;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.PowerShell;

/// <summary>
/// AOT-safe <see cref="NValue"/>-to-JSON conversion using <see cref="Utf8JsonWriter"/>
/// directly. Mirror of <see cref="JsonToNValue"/> on the write side. No reflection-based
/// <c>JsonSerializer.Serialize</c> path so the bridge compiles cleanly under
/// <c>TreatWarningsAsErrors=true</c> and <c>IsAotCompatible=true</c>.
/// </summary>
internal static class NValueToJson
{
    /// <summary>
    /// Serialize <paramref name="value"/> to a UTF-8 JSON byte array. <paramref name="indent"/>
    /// of 0 = compact; values up to 32 produce indented JSON with that many spaces per level.
    /// </summary>
    public static byte[] SerializeToUtf8Bytes(NValue value, int indent = 0)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions
        {
            Indented = indent > 0,
            IndentSize = indent,
        }))
        {
            Write(writer, value);
        }
        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>Serialize <paramref name="value"/> to a JSON string.</summary>
    public static string SerializeToString(NValue value, int indent = 0)
        => Encoding.UTF8.GetString(SerializeToUtf8Bytes(value, indent));

    /// <summary>
    /// Write <paramref name="v"/> to <paramref name="w"/>. Throws <see cref="EvaluatorException"/>
    /// for values with no canonical JSON form (functions, variants, non-finite floats).
    /// </summary>
    public static void Write(Utf8JsonWriter w, NValue v)
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
                    throw new EvaluatorException($"cannot serialize {f.Value} (NaN/Infinity are not valid JSON)");
                w.WriteNumberValue(f.Value);
                break;
            case NString s:
                w.WriteStringValue(s.Value);
                break;
            case NList list:
                w.WriteStartArray();
                foreach (var item in list.Items) Write(w, item);
                w.WriteEndArray();
                break;
            case NSeq seq:
                w.WriteStartArray();
                foreach (var item in seq.Items) Write(w, item);
                w.WriteEndArray();
                break;
            case NRecord rec:
                w.WriteStartObject();
                foreach (var kv in rec.Fields)
                {
                    w.WritePropertyName(kv.Key);
                    Write(w, kv.Value);
                }
                w.WriteEndObject();
                break;
            case NVariant variant:
                throw new EvaluatorException($"json: NVariant '{variant.Tag}' has no canonical JSON form — convert to a record first");
            case NFunc:
                throw new EvaluatorException("json: functions cannot be serialized to JSON");
            default:
                throw new EvaluatorException($"json: unhandled value type {v.GetType().Name}");
        }
    }
}
