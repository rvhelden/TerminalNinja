using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.PowerShell;

/// <summary>
/// AOT-safe JSON-to-<see cref="NValue"/> conversion using <see cref="Utf8JsonReader"/>
/// directly — no reflection-based <c>JsonSerializer.Deserialize</c> path so the bridge
/// compiles cleanly under <c>TreatWarningsAsErrors=true</c> and <c>IsAotCompatible=true</c>.
/// </summary>
public static class JsonToNValue
{
    /// <summary>Parse a JSON document into an <see cref="NValue"/>. Empty input becomes <see cref="NUnit"/>.</summary>
    public static NValue Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (string.IsNullOrWhiteSpace(json)) return NUnit.Instance;
        var bytes = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(bytes);
        if (!reader.Read()) return NUnit.Instance;
        return Read(ref reader);
    }

    private static NValue Read(ref Utf8JsonReader r)
    {
        switch (r.TokenType)
        {
            case JsonTokenType.Null: return NUnit.Instance;
            case JsonTokenType.True: return new NBool(true);
            case JsonTokenType.False: return new NBool(false);
            case JsonTokenType.Number:
                if (r.TryGetInt64(out var i)) return new NInt(i);
                return new NFloat(r.GetDouble());
            case JsonTokenType.String:
                return new NString(r.GetString() ?? string.Empty);
            case JsonTokenType.StartArray: return ReadArray(ref r);
            case JsonTokenType.StartObject: return ReadObject(ref r);
            default:
                throw new InvalidOperationException($"unexpected JSON token {r.TokenType}");
        }
    }

    private static NValue ReadArray(ref Utf8JsonReader r)
    {
        var b = ImmutableArray.CreateBuilder<NValue>();
        while (r.Read() && r.TokenType != JsonTokenType.EndArray)
            b.Add(Read(ref r));
        return new NList(b.ToImmutable());
    }

    private static NValue ReadObject(ref Utf8JsonReader r)
    {
        var b = ImmutableSortedDictionary.CreateBuilder<string, NValue>(StringComparer.Ordinal);
        while (r.Read() && r.TokenType != JsonTokenType.EndObject)
        {
            if (r.TokenType != JsonTokenType.PropertyName)
                throw new InvalidOperationException($"expected property name, got {r.TokenType}");
            var key = r.GetString() ?? string.Empty;
            if (!r.Read())
                throw new InvalidOperationException("unexpected end of object");
            b[key] = Read(ref r);
        }
        return new NRecord(b.ToImmutable());
    }
}
