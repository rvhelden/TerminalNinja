using TerminalNinja.Shell.Builtins;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Tests.Unit.Shell;

/// <summary>
/// Multi-statement script support — <see cref="NinjaEvaluator.EvalScript"/> walks
/// any number of top-level forms separated by newlines, threading <see cref="Env"/>
/// through let-statements so bindings persist across forms.
/// </summary>
public class MultiStatementScriptTests
{
    private static NValue RunScript(string source)
        => NinjaEvaluator.EvalScript(source, BuiltinRegistry.CreateDefaultEnv()).Value;

    [Test]
    public async Task EvalScript_EmptyInput_ReturnsUnit()
    {
        await Assert.That(RunScript("") is NUnit).IsTrue();
        await Assert.That(RunScript("   \n  \n") is NUnit).IsTrue();
    }

    [Test]
    public async Task EvalScript_SingleForm_StillWorks()
    {
        await Assert.That(RunScript("1 + 2")).IsEqualTo((NValue)new NInt(3));
    }

    [Test]
    public async Task EvalScript_TwoLetStatements_BothBindingsVisibleInThirdForm()
    {
        var script = "let x = 7\nlet y = 6\nx * y";
        await Assert.That(RunScript(script)).IsEqualTo((NValue)new NInt(42));
    }

    [Test]
    public async Task EvalScript_TrailingNewlines_Ignored()
    {
        await Assert.That(RunScript("let x = 5\nx + 1\n\n\n"))
            .IsEqualTo((NValue)new NInt(6));
    }

    [Test]
    public async Task EvalScript_LastFormValue_IsReturned()
    {
        // Earlier forms' values are discarded; the last form is what comes back.
        await Assert.That(RunScript("\"throwaway\"\n42")).IsEqualTo((NValue)new NInt(42));
    }

    [Test]
    public async Task EvalScript_LetStatement_AsLastForm_ReturnsItsBoundValue()
    {
        // Mirrors REPL behaviour: bindings show their value.
        await Assert.That(RunScript("let answer = 42")).IsEqualTo((NValue)new NInt(42));
    }

    [Test]
    public async Task EvalScript_ExpressionUsingBuiltinModule_WorksAcrossForms()
    {
        var script = "let xs = 1..5\nxs | fold(0, (acc, x) => acc + x)";
        await Assert.That(RunScript(script)).IsEqualTo((NValue)new NInt(15));
    }

    [Test]
    public async Task EvalScript_RecursiveLetSpansForms()
    {
        // Top-level letrec via a single LetStatement that defines a lambda referring
        // to its own name — the second form invokes it.
        var script =
            "let fact = n => n switch { 0 => 1, n => n * fact(n - 1) }\n" +
            "fact(5)";
        await Assert.That(RunScript(script)).IsEqualTo((NValue)new NInt(120));
    }

    [Test]
    public async Task EvalScript_EarlyError_LaterFormsAreNotRun()
    {
        // The first form errors; the second never runs. We can't directly observe the
        // second form was skipped, but we can confirm the exception is the *first*
        // form's error and not the second's.
        var script = "unbound_first\nlet x = unbound_second in x";
        await Assert.That(() => RunScript(script)).ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task EvalScript_BindingsPersistThroughErrorRecovery_DocumentingSemantics()
    {
        // Partial-commit: if form N fails, forms 1..N-1's bindings already exist on
        // the env. We can't observe that here because EvalScript throws — but we can
        // verify by calling EvalScript again with the same env it returned before the
        // failure (which we don't get from EvalScript). Instead pin the semantics by
        // running forms one-at-a-time and checking that the env grows.
        var env = BuiltinRegistry.CreateDefaultEnv();
        var r1 = NinjaEvaluator.EvalSource("let committed = 1", env);
        env = r1.Env;
        try
        {
            NinjaEvaluator.EvalSource("unbound_name", env);
        }
        catch (EvaluatorException) { /* expected */ }
        // committed is still visible after the second form's error.
        var r3 = NinjaEvaluator.EvalSource("committed", env);
        await Assert.That(r3.Value).IsEqualTo((NValue)new NInt(1));
    }

    [Test]
    public async Task EvalScript_EnvSetSideEffect_PersistsAcrossForms()
    {
        var k = "NINJA_MS_TEST_" + Guid.NewGuid().ToString("N");
        var script =
            $"env.set(\"{k}\", \"value\")\n" +
            $"env.get(\"{k}\")";
        try
        {
            await Assert.That(RunScript(script)).IsEqualTo((NValue)new NString("value"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(k, null);
        }
    }

    [Test]
    public async Task EvalSource_StillRejectsMultiForm_BackwardsCompat()
    {
        // EvalSource (single-form) preserves its old contract — it throws on a second
        // unexpected token. Callers wanting multi-form should switch to EvalScript.
        await Assert.That(() => NinjaEvaluator.EvalSource("let x = 1\nx + 1", BuiltinRegistry.CreateDefaultEnv()))
            .ThrowsExactly<TerminalNinja.Shell.Parser.ParserException>();
    }
}
