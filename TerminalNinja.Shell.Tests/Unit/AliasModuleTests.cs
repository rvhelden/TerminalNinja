using TerminalNinja.Shell.Builtins;
using TerminalNinja.Shell.Config;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Tests.Unit;

public class AliasModuleTests
{
    private static (NinjaConfig config, NValue result) Run(string source, NinjaConfig? config = null)
    {
        config ??= NinjaConfig.Empty();
        var env = BuiltinRegistry.CreateDefaultEnvWith(config);
        var result = NinjaEvaluator.EvalScript(source, env).Value;
        return (config, result);
    }

    [Test]
    public async Task AliasSet_FsCd_BindsCallableReferenceIntoConfig()
    {
        var (config, _) = Run("alias.set(\"foo\", fs.cd)");
        await Assert.That(config.Aliases.ContainsKey("foo")).IsTrue();

        var env = BuiltinRegistry.CreateDefaultEnv();
        var fs = env.Lookup("fs");
        if (fs is not NRecord r) throw new InvalidOperationException();
        var expected = r.Fields["cd"];
        var stored = config.Aliases["foo"];
        await Assert.That(stored is NFunc nf && expected is NFunc en && ReferenceEquals(nf, en)).IsTrue();
    }

    [Test]
    public async Task AliasSet_Lambda_BindsAndInvokesCorrectly()
    {
        // Bind a lambda alias, then call it directly via the stored callable
        // — this is the path Task 4's AliasInterceptor invokes through.
        var (config, _) = Run("alias.set(\"ll\", path => fs.is_dir(path))");
        var stored = config.Aliases["ll"];
        if (stored is not NFunc nf) throw new InvalidOperationException();
        // Invoke the lambda with the project root — fs.is_dir should return true.
        var result = nf.Apply([new NString(Directory.GetCurrentDirectory())]);
        await Assert.That(result is NBool b && b.Value).IsTrue();
    }

    [Test]
    public async Task AliasSet_NonCallableValue_ThrowsEvaluatorException()
    {
        await Assert.That(() => Run("alias.set(\"bad\", \"not a function\")"))
            .ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task AliasSet_NonStringName_ThrowsEvaluatorException()
    {
        await Assert.That(() => Run("alias.set(42, fs.cd)"))
            .ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task AliasUnset_ReturnsTrue_AfterSet()
    {
        var config = NinjaConfig.Empty();
        Run("alias.set(\"x\", fs.cd)", config);
        var (_, result) = Run("alias.unset(\"x\")", config);
        await Assert.That(result is NBool b && b.Value).IsTrue();
        await Assert.That(config.Aliases.ContainsKey("x")).IsFalse();
    }

    [Test]
    public async Task AliasUnset_ReturnsFalse_WhenAbsent()
    {
        var (_, result) = Run("alias.unset(\"never_set\")");
        await Assert.That(result is NBool b && !b.Value).IsTrue();
    }

    [Test]
    public async Task AliasList_ReturnsRecord_WithCurrentBindings()
    {
        var (_, result) = Run("alias.set(\"a\", fs.cd)\nalias.set(\"b\", fs.ls)\nalias.list()");
        if (result is not NRecord r) throw new InvalidOperationException("expected NRecord");
        await Assert.That(r.Fields.ContainsKey("a")).IsTrue();
        await Assert.That(r.Fields.ContainsKey("b")).IsTrue();
    }

    [Test]
    public async Task AliasGet_ReturnsCallable_ForKnown()
    {
        var (_, result) = Run("alias.set(\"x\", fs.cd)\nalias.get(\"x\")");
        await Assert.That(result is NFunc).IsTrue();
    }

    [Test]
    public async Task AliasGet_ReturnsUnit_ForUnknown()
    {
        var (_, result) = Run("alias.get(\"never_set\")");
        await Assert.That(result is NUnit).IsTrue();
    }
}
