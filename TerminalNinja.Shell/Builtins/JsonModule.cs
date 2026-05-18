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

        try
        {
            return new NString(NValueToJson.SerializeToString(args[0], indent));
        }
        catch (EvaluatorException ex)
        {
            throw new EvaluatorException("json.stringify: " + ex.Message, ex);
        }
    }
}
