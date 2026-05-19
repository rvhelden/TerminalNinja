using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Config;

/// <summary>
/// Seeds a <see cref="NinjaConfig"/> with the shell-mode aliases that ship out of
/// the box: <c>cd</c>, <c>ls</c>, <c>pwd</c>, <c>cat</c>, <c>mkdir</c>, <c>rm</c>,
/// <c>cp</c>, <c>mv</c>, <c>echo</c>. Each alias is bound to the same callable
/// <see cref="NValue"/> the canonical syntax would resolve to, so calling
/// <c>cd foo</c> and <c>fs.cd("foo")</c> produces identical behaviour.
/// </summary>
/// <remarks>
/// Lookups skip silently when a target module or top-level builtin is missing —
/// this keeps the seeder robust against builtin-set changes (e.g. an embedder
/// stripping the <c>fs</c> module would simply ship without those aliases
/// rather than crashing at startup).
/// </remarks>
public static class DefaultAliases
{
    /// <summary>
    /// Bind the standard shell-mode aliases against the callables currently in
    /// <paramref name="env"/>. Safe to call multiple times — later calls overwrite
    /// earlier bindings with the same names.
    /// </summary>
    public static void Seed(NinjaConfig config, Env env)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(env);

        SeedModuleMember(config, env, "cd", "fs", "cd");
        SeedModuleMember(config, env, "ls", "fs", "ls");
        SeedModuleMember(config, env, "pwd", "fs", "pwd");
        SeedModuleMember(config, env, "cat", "fs", "cat");
        SeedModuleMember(config, env, "mkdir", "fs", "mkdir");
        SeedModuleMember(config, env, "rm", "fs", "rm");
        SeedModuleMember(config, env, "cp", "fs", "copy");
        SeedModuleMember(config, env, "mv", "fs", "move");
        SeedTopLevel(config, env, "echo", "println");
    }

    private static void SeedModuleMember(NinjaConfig config, Env env, string alias, string moduleName, string memberName)
    {
        if (!env.Contains(moduleName)) return;
        if (env.Lookup(moduleName) is not NRecord rec) return;
        if (!rec.Fields.TryGetValue(memberName, out var fn)) return;
        if (fn is not NFunc) return;
        config.SetAlias(alias, fn);
    }

    private static void SeedTopLevel(NinjaConfig config, Env env, string alias, string globalName)
    {
        if (!env.Contains(globalName)) return;
        var fn = env.Lookup(globalName);
        if (fn is not NFunc) return;
        config.SetAlias(alias, fn);
    }
}
