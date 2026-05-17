using System.Collections.Immutable;
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

    private static ImmutableDictionary<string, NValue> Build()
    {
        var b = ImmutableDictionary.CreateBuilder<string, NValue>(StringComparer.Ordinal);
        PipelineOps.Register(b);
        Io.Register(b);
        Fs.Register(b);
        return b.ToImmutable();
    }
}
