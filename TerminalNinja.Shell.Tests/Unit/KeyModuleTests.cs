using TerminalNinja.Shell.Builtins;
using TerminalNinja.Shell.Config;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Tests.Unit;

public class KeyModuleTests
{
    private static (NinjaConfig config, NValue result) Run(string source, NinjaConfig? config = null)
    {
        config ??= NinjaConfig.Empty();
        var env = BuiltinRegistry.CreateDefaultEnvWith(config);
        var result = NinjaEvaluator.EvalScript(source, env).Value;
        return (config, result);
    }

    [Test]
    public async Task KeyBind_ValidChordAndAction_StoresInConfig()
    {
        var (config, _) = Run("key.bind(\"Ctrl+L\", \"clear\")");
        await Assert.That(config.Keybindings.ContainsKey("Ctrl+L")).IsTrue();
        await Assert.That(config.Keybindings["Ctrl+L"]).IsEqualTo("clear");
    }

    [Test]
    public async Task KeyBind_UnknownAction_ThrowsEvaluatorException()
    {
        await Assert.That(() => Run("key.bind(\"Ctrl+L\", \"do-the-thing\")"))
            .ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task KeyBind_InvalidChord_ThrowsEvaluatorException()
    {
        await Assert.That(() => Run("key.bind(\"Foo+L\", \"clear\")"))
            .ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task KeyBind_TrailingPlusChord_ThrowsEvaluatorException()
    {
        await Assert.That(() => Run("key.bind(\"Ctrl+\", \"clear\")"))
            .ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task KeyBind_NonStringArgs_ThrowsEvaluatorException()
    {
        await Assert.That(() => Run("key.bind(42, \"clear\")"))
            .ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task KeyUnbind_ReturnsTrue_AfterBind()
    {
        var config = NinjaConfig.Empty();
        Run("key.bind(\"Ctrl+L\", \"clear\")", config);
        var (_, result) = Run("key.unbind(\"Ctrl+L\")", config);
        await Assert.That(result is NBool b && b.Value).IsTrue();
        await Assert.That(config.Keybindings.ContainsKey("Ctrl+L")).IsFalse();
    }

    [Test]
    public async Task KeyUnbind_ReturnsFalse_WhenAbsent()
    {
        var (_, result) = Run("key.unbind(\"Ctrl+Q\")");
        await Assert.That(result is NBool b && !b.Value).IsTrue();
    }

    [Test]
    public async Task KeyList_ReturnsRecord_OfBindings()
    {
        var (_, result) = Run("key.bind(\"Ctrl+L\", \"clear\")\nkey.bind(\"Ctrl+R\", \"history-prev\")\nkey.list()");
        if (result is not NRecord r) throw new InvalidOperationException();
        await Assert.That(r.Fields.ContainsKey("Ctrl+L")).IsTrue();
        await Assert.That(r.Fields.ContainsKey("Ctrl+R")).IsTrue();
    }

    [Test]
    public async Task KeyBind_AllSupportedActions_Accepted()
    {
        foreach (var action in new[] { "clear", "history-prev", "history-next", "abort", "submit", "complete", "edit-config" })
        {
            var config = NinjaConfig.Empty();
            Run($"key.bind(\"Ctrl+A\", \"{action}\")", config);
            await Assert.That(config.Keybindings["Ctrl+A"]).IsEqualTo(action);
        }
    }
}
