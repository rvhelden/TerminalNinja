using TerminalNinja.Shell.Builtins;
using TerminalNinja.Shell.Config;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Tests.Unit;

public class DefaultAliasesTests
{
    [Test]
    public async Task Seed_AgainstDefaultEnv_BindsAllCommonAliases()
    {
        var config = NinjaConfig.Empty();
        var env = BuiltinRegistry.CreateDefaultEnv();
        DefaultAliases.Seed(config, env);

        foreach (var alias in new[] { "cd", "ls", "pwd", "cat", "mkdir", "rm", "cp", "mv", "echo" })
            await Assert.That(config.Aliases.ContainsKey(alias)).IsTrue();
    }

    [Test]
    public async Task Seed_BindsCdToFsCd_SameCallableReference()
    {
        var config = NinjaConfig.Empty();
        var env = BuiltinRegistry.CreateDefaultEnv();
        DefaultAliases.Seed(config, env);

        var fsModule = env.Lookup("fs");
        if (fsModule is not NRecord r) throw new InvalidOperationException();
        var fsCd = r.Fields["cd"];
        if (fsCd is not NFunc expected) throw new InvalidOperationException();
        var stored = config.Aliases["cd"];
        await Assert.That(stored is NFunc nf && ReferenceEquals(nf, expected)).IsTrue();
    }

    [Test]
    public async Task Seed_BindsEchoToPrintln_TopLevelBuiltin()
    {
        var config = NinjaConfig.Empty();
        var env = BuiltinRegistry.CreateDefaultEnv();
        DefaultAliases.Seed(config, env);

        var println = env.Lookup("println");
        if (println is not NFunc expected) throw new InvalidOperationException();
        var stored = config.Aliases["echo"];
        await Assert.That(stored is NFunc nf && ReferenceEquals(nf, expected)).IsTrue();
    }

    [Test]
    public async Task Seed_SkipsMissingTargets_DoesNotThrow()
    {
        var config = NinjaConfig.Empty();
        // Env without any builtins — no fs, no println, nothing.
        var env = Env.Empty;
        DefaultAliases.Seed(config, env);
        // No aliases seeded, no exception.
        await Assert.That(config.Aliases.Count).IsEqualTo(0);
    }
}
