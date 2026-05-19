using System.Collections.Immutable;
using TerminalNinja.Shell.Config;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Builtins;

/// <summary>Compile-time registry of built-in functions baked into the default environment.</summary>
public static class BuiltinRegistry
{
    /// <summary>The default set of builtins (pipeline operators today; Fs/Io land in Phase 6).</summary>
    public static ImmutableDictionary<string, NValue> Defaults { get; } = Build();

    /// <summary>Build an <see cref="Env"/> seeded with the defaults.</summary>
    public static Env CreateDefaultEnv()
    {
        var env = Env.Empty;
        foreach (var kv in Defaults)
            env = env.Extend(kv.Key, kv.Value);
        return env;
    }

    /// <summary>
    /// Build an <see cref="Env"/> seeded with the defaults plus the per-REPL
    /// alias / key modules closed over <paramref name="config"/>. Use this when
    /// the embedder owns a <see cref="NinjaConfig"/> and wants <c>alias.set</c>
    /// / <c>key.bind</c> to mutate that specific instance.
    /// </summary>
    public static Env CreateDefaultEnvWith(NinjaConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var b = ImmutableDictionary.CreateBuilder<string, NValue>(StringComparer.Ordinal);
        foreach (var kv in Defaults) b[kv.Key] = kv.Value;
        AliasModule.Register(b, config);
        KeyModule.Register(b, config);
        var env = Env.Empty;
        foreach (var kv in b) env = env.Extend(kv.Key, kv.Value);
        return env;
    }

    private static ImmutableDictionary<string, NValue> Build()
    {
        var b = ImmutableDictionary.CreateBuilder<string, NValue>(StringComparer.Ordinal);
        PipelineOps.Register(b);
        Io.Register(b);
        FsModule.Register(b);
        ObjModule.Register(b);
        EnvModule.Register(b);
        ProcModule.Register(b);
        JsonModule.Register(b);
        XmlModule.Register(b);
        HttpModule.Register(b);
        return b.ToImmutable();
    }

    /// <summary>
    /// Build an <see cref="NRecord"/> module from a list of <c>(name, NFunc)</c> entries
    /// and bind it under <paramref name="moduleName"/> in <paramref name="b"/>. Keeps the
    /// per-module Register methods DRY.
    /// </summary>
    internal static void RegisterModule(
        ImmutableDictionary<string, NValue>.Builder b,
        string moduleName,
        params (string Name, NValue Fn)[] entries)
    {
        var module = ImmutableSortedDictionary.CreateBuilder<string, NValue>(StringComparer.Ordinal);
        foreach (var (name, fn) in entries)
            module[name] = fn;
        b[moduleName] = new NRecord(module.ToImmutable());
    }
}
