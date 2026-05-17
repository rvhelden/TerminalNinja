using System.Collections;
using System.Collections.Immutable;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Builtins;

/// <summary>
/// The <c>env</c> module — process-scoped environment variable access. Reads and
/// writes go through <see cref="System.Environment"/> at the Process target only;
/// changes do not persist past <c>ninja</c>'s lifetime, and child processes (incl.
/// <c>pwsh { ... }</c> blocks) inherit the current state at spawn time.
/// </summary>
public static class EnvModule
{
    /// <summary>Register the <c>env</c> module into the default-environment builder.</summary>
    public static void Register(ImmutableDictionary<string, NValue>.Builder b)
    {
        BuiltinRegistry.RegisterModule(b, "env",
            ("all", new NFunc(All, 0)),
            ("get", new NFunc(Get, -1)),
            ("set", new NFunc(Set, -1)),
            ("unset", new NFunc(Unset, 1)),
            ("has", new NFunc(Has, 1)));
    }

    private static NValue All(NValue[] args)
    {
        if (args.Length != 0) throw new EvaluatorException($"env.all expects 0 arguments, got {args.Length}");
        var rec = ImmutableSortedDictionary.CreateBuilder<string, NValue>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in System.Environment.GetEnvironmentVariables())
        {
            var key = entry.Key as string;
            if (key == null) continue;
            rec[key] = new NString(entry.Value as string ?? string.Empty);
        }
        return new NRecord(rec.ToImmutable());
    }

    private static NValue Get(NValue[] args)
    {
        if (args.Length is < 1 or > 2) throw new EvaluatorException($"env.get expects 1 or 2 arguments, got {args.Length}");
        if (args[0] is not NString name) throw new EvaluatorException("env.get name must be a string");
        ValidateName(name.Value, "env.get");
        var value = System.Environment.GetEnvironmentVariable(name.Value);
        if (value != null) return new NString(value);
        if (args.Length == 2) return args[1];
        throw new EvaluatorException($"environment variable '{name.Value}' is not set");
    }

    private static NValue Set(NValue[] args)
    {
        if (args.Length is < 1 or > 2) throw new EvaluatorException($"env.set expects 1 or 2 arguments, got {args.Length}");

        // Bulk form: env.set({ K1: "v1", K2: "v2" }) — returns NRecord of previous values.
        if (args.Length == 1)
        {
            if (args[0] is not NRecord rec)
                throw new EvaluatorException("env.set requires (name, value) or a single record");
            var prev = ImmutableSortedDictionary.CreateBuilder<string, NValue>(StringComparer.Ordinal);
            foreach (var kv in rec.Fields)
            {
                if (kv.Value is not NString s)
                    throw new EvaluatorException($"env.set: value for '{kv.Key}' must be a string");
                ValidateName(kv.Key, "env.set");
                prev[kv.Key] = ReadAsNValue(kv.Key);
                System.Environment.SetEnvironmentVariable(kv.Key, s.Value);
            }
            return new NRecord(prev.ToImmutable());
        }

        // Single form: env.set(name, value).
        if (args[0] is not NString name) throw new EvaluatorException("env.set name must be a string");
        if (args[1] is not NString value) throw new EvaluatorException("env.set value must be a string");
        ValidateName(name.Value, "env.set");
        var previous = ReadAsNValue(name.Value);
        System.Environment.SetEnvironmentVariable(name.Value, value.Value);
        return previous;
    }

    private static NValue Unset(NValue[] args)
    {
        if (args.Length != 1) throw new EvaluatorException($"env.unset expects 1 argument, got {args.Length}");
        if (args[0] is not NString name) throw new EvaluatorException("env.unset name must be a string");
        ValidateName(name.Value, "env.unset");
        var previous = ReadAsNValue(name.Value);
        System.Environment.SetEnvironmentVariable(name.Value, null);
        return previous;
    }

    private static NValue Has(NValue[] args)
    {
        if (args.Length != 1) throw new EvaluatorException($"env.has expects 1 argument, got {args.Length}");
        if (args[0] is not NString name) throw new EvaluatorException("env.has name must be a string");
        ValidateName(name.Value, "env.has");
        return new NBool(System.Environment.GetEnvironmentVariable(name.Value) != null);
    }

    private static NValue ReadAsNValue(string name)
    {
        var v = System.Environment.GetEnvironmentVariable(name);
        return v != null ? new NString(v) : NUnit.Instance;
    }

    private static void ValidateName(string name, string op)
    {
        if (string.IsNullOrEmpty(name))
            throw new EvaluatorException($"{op}: environment variable name must be non-empty");
        if (name.IndexOf('=') >= 0)
            throw new EvaluatorException($"{op}: environment variable name must not contain '='");
    }
}
