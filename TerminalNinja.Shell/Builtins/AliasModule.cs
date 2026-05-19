using System.Collections.Immutable;
using TerminalNinja.Shell.Config;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Builtins;

/// <summary>
/// The <c>alias</c> module — runtime surface for managing shell-mode aliases stored
/// in a <see cref="NinjaConfig"/>. Each function closes over the config instance
/// supplied at registration time so the module mutates per-REPL state rather than
/// a global singleton.
/// </summary>
/// <remarks>
/// Functions:
/// <list type="bullet">
///   <item><c>alias.set(name, fn)</c> — bind a callable; rejects non-<see cref="NFunc"/> values.</item>
///   <item><c>alias.unset(name) -> bool</c> — remove a binding; returns whether one existed.</item>
///   <item><c>alias.list() -> record</c> — snapshot of all bindings as a record of callables.</item>
///   <item><c>alias.get(name) -> fn | unit</c> — lookup; returns <see cref="NUnit"/> for unknowns.</item>
/// </list>
/// </remarks>
public static class AliasModule
{
    /// <summary>Register the <c>alias</c> module into <paramref name="b"/>, closing over <paramref name="config"/>.</summary>
    public static void Register(ImmutableDictionary<string, NValue>.Builder b, NinjaConfig config)
    {
        ArgumentNullException.ThrowIfNull(b);
        ArgumentNullException.ThrowIfNull(config);

        BuiltinRegistry.RegisterModule(b, "alias",
            ("set", new NFunc(args => Set(args, config), 2)),
            ("unset", new NFunc(args => Unset(args, config), 1)),
            ("list", new NFunc(args => List(args, config), 0)),
            ("get", new NFunc(args => Get(args, config), 1)));
    }

    private static NValue Set(NValue[] args, NinjaConfig config)
    {
        if (args.Length != 2) throw new EvaluatorException($"alias.set expects 2 arguments, got {args.Length}");
        if (args[0] is not NString name) throw new EvaluatorException("alias.set: first argument must be a string");
        if (args[1] is not NFunc) throw new EvaluatorException("alias.set: second argument must be a function");
        try
        {
            config.SetAlias(name.Value, args[1]);
        }
        catch (ArgumentException ex)
        {
            throw new EvaluatorException($"alias.set: {ex.Message}");
        }
        return NUnit.Instance;
    }

    private static NValue Unset(NValue[] args, NinjaConfig config)
    {
        if (args.Length != 1) throw new EvaluatorException($"alias.unset expects 1 argument, got {args.Length}");
        if (args[0] is not NString name) throw new EvaluatorException("alias.unset: argument must be a string");
        return new NBool(config.RemoveAlias(name.Value));
    }

    private static NValue List(NValue[] args, NinjaConfig config)
    {
        if (args.Length != 0) throw new EvaluatorException($"alias.list expects 0 arguments, got {args.Length}");
        var b = ImmutableSortedDictionary.CreateBuilder<string, NValue>(StringComparer.Ordinal);
        foreach (var kv in config.Aliases) b[kv.Key] = kv.Value;
        return new NRecord(b.ToImmutable());
    }

    private static NValue Get(NValue[] args, NinjaConfig config)
    {
        if (args.Length != 1) throw new EvaluatorException($"alias.get expects 1 argument, got {args.Length}");
        if (args[0] is not NString name) throw new EvaluatorException("alias.get: argument must be a string");
        return config.TryGetAlias(name.Value, out var fn) ? fn : NUnit.Instance;
    }
}
