using TerminalNinja.Shell.Builtins;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Tests.Unit.Shell;

public class EvaluatorTests
{
    private static NValue Run(string source, Env? env = null)
        => NinjaEvaluator.EvalSource(source, env ?? BuiltinRegistry.CreateDefaultEnv()).Value;

    [Test]
    public async Task Eval_IntLiteral_ReturnsNInt()
    {
        await Assert.That(Run("42")).IsEqualTo((NValue)new NInt(42));
    }

    [Test]
    public async Task Eval_FloatLiteral_ReturnsNFloat()
    {
        await Assert.That(Run("3.14")).IsEqualTo((NValue)new NFloat(3.14));
    }

    [Test]
    public async Task Eval_StringLiteral_ReturnsNString()
    {
        await Assert.That(Run("\"hello\"")).IsEqualTo((NValue)new NString("hello"));
    }

    [Test]
    public async Task Eval_BoolLiterals_ReturnNBool()
    {
        await Assert.That(Run("true")).IsEqualTo((NValue)new NBool(true));
        await Assert.That(Run("false")).IsEqualTo((NValue)new NBool(false));
    }

    [Test]
    public async Task Eval_LetIn_BindsAndEvaluatesBody()
    {
        await Assert.That(Run("let x = 42 in x + 1")).IsEqualTo((NValue)new NInt(43));
    }

    [Test]
    public async Task Eval_TopLevelLetStatement_ExtendsEnv()
    {
        var env = BuiltinRegistry.CreateDefaultEnv();
        var r1 = NinjaEvaluator.EvalSource("let n = 7", env);
        var r2 = NinjaEvaluator.EvalSource("n * 6", r1.Env);
        await Assert.That(r2.Value).IsEqualTo((NValue)new NInt(42));
    }

    [Test]
    public async Task Eval_LambdaAndCall_AppliesArgsInOrder()
    {
        await Assert.That(Run("let add = (a, b) => a - b in add(10, 3)"))
            .IsEqualTo((NValue)new NInt(7));
    }

    [Test]
    public async Task Eval_RecursiveLetFactorial_Works()
    {
        var v = Run("let fact = n => n switch { 0 => 1, n => n * fact(n - 1) } in fact(5)");
        await Assert.That(v).IsEqualTo((NValue)new NInt(120));
    }

    [Test]
    public async Task Eval_BinaryAdd_NumericPromotion()
    {
        await Assert.That(Run("1 + 2")).IsEqualTo((NValue)new NInt(3));
        await Assert.That(Run("1 + 2.5")).IsEqualTo((NValue)new NFloat(3.5));
    }

    [Test]
    public async Task Eval_StringConcatenation_WithPlus()
    {
        await Assert.That(Run("\"hello, \" + \"world\""))
            .IsEqualTo((NValue)new NString("hello, world"));
    }

    [Test]
    public async Task Eval_BinaryOps_FullCoverage()
    {
        await Assert.That(Run("10 - 4")).IsEqualTo((NValue)new NInt(6));
        await Assert.That(Run("3 * 7")).IsEqualTo((NValue)new NInt(21));
        await Assert.That(Run("20 / 4")).IsEqualTo((NValue)new NInt(5));
        await Assert.That(Run("3 == 3")).IsEqualTo((NValue)new NBool(true));
        await Assert.That(Run("3 != 4")).IsEqualTo((NValue)new NBool(true));
        await Assert.That(Run("1 < 2")).IsEqualTo((NValue)new NBool(true));
        await Assert.That(Run("2 <= 2")).IsEqualTo((NValue)new NBool(true));
        await Assert.That(Run("3 > 2")).IsEqualTo((NValue)new NBool(true));
        await Assert.That(Run("3 >= 3")).IsEqualTo((NValue)new NBool(true));
        await Assert.That(Run("true && false")).IsEqualTo((NValue)new NBool(false));
        await Assert.That(Run("true || false")).IsEqualTo((NValue)new NBool(true));
    }

    [Test]
    public async Task Eval_LogicalShortCircuit_DoesNotEvaluateRhs()
    {
        // `false && (1 / 0)` would throw if the rhs were evaluated.
        await Assert.That(Run("false && (1 / 0 == 0)")).IsEqualTo((NValue)new NBool(false));
        await Assert.That(Run("true || (1 / 0 == 0)")).IsEqualTo((NValue)new NBool(true));
    }

    [Test]
    public async Task Eval_UnaryMinus_Numeric()
    {
        await Assert.That(Run("-5")).IsEqualTo((NValue)new NInt(-5));
        await Assert.That(Run("-3.5")).IsEqualTo((NValue)new NFloat(-3.5));
    }

    [Test]
    public async Task Eval_ListLiteral_ProducesNList()
    {
        var v = Run("[1, 2, 3]");
        if (v is not NList list) throw new InvalidOperationException($"expected NList, got {v.GetType().Name}");
        await Assert.That(list.Items.Length).IsEqualTo(3);
        await Assert.That(list.Items[0]).IsEqualTo((NValue)new NInt(1));
        await Assert.That(list.Items[2]).IsEqualTo((NValue)new NInt(3));
    }

    [Test]
    public async Task Eval_RangeLiteral_Inclusive()
    {
        // Ranges are lazy — they evaluate to NSeq. Materialise here to inspect.
        var v = Run("1..5");
        if (v is not NSeq seq) throw new InvalidOperationException($"expected NSeq, got {v.GetType().Name}");
        var items = seq.Items.ToList();
        await Assert.That(items.Count).IsEqualTo(5);
        await Assert.That(items[0]).IsEqualTo((NValue)new NInt(1));
        await Assert.That(items[4]).IsEqualTo((NValue)new NInt(5));
    }

    [Test]
    public async Task Eval_RangeLiteral_EmptyWhenLoGreaterThanHi()
    {
        var v = Run("5..1");
        if (v is not NSeq seq) throw new InvalidOperationException($"expected NSeq, got {v.GetType().Name}");
        await Assert.That(seq.Items.Any()).IsFalse();
    }

    [Test]
    public async Task Eval_RecordLiteral_BareAndQuotedKeysEqual()
    {
        var v1 = Run("{ Name: \"a\", Age: 40 }");
        var v2 = Run("{ \"Name\": \"a\", \"Age\": 40 }");
        await Assert.That(v1 is NRecord).IsTrue();
        await Assert.That(v2 is NRecord).IsTrue();
        await Assert.That(NValueOps.Equals(v1, v2)).IsTrue();
    }

    [Test]
    public async Task Eval_RecordMemberAccess_DotAndIndexer()
    {
        await Assert.That(Run("{ Name: \"a\", Age: 40 }.Age"))
            .IsEqualTo((NValue)new NInt(40));
        await Assert.That(Run("{ \"first name\": \"Ronald\" }[\"first name\"]"))
            .IsEqualTo((NValue)new NString("Ronald"));
    }

    [Test]
    public async Task Eval_MissingMemberAccess_Throws()
    {
        await Assert.That(() => Run("{ a: 1 }.b"))
            .ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task Eval_ListIndexer_Int()
    {
        await Assert.That(Run("[10, 20, 30][1]"))
            .IsEqualTo((NValue)new NInt(20));
    }

    [Test]
    public async Task Eval_StringInterpolation_RendersHoles()
    {
        await Assert.That(Run("let name = \"Ronald\" in $\"hello, {name}!\""))
            .IsEqualTo((NValue)new NString("hello, Ronald!"));
    }

    [Test]
    public async Task Eval_StringInterpolation_RendersComputedExpression()
    {
        await Assert.That(Run("let p = { Age: 40 } in $\"in 5: {p.Age + 5}\""))
            .IsEqualTo((NValue)new NString("in 5: 45"));
    }

    [Test]
    public async Task Eval_Switch_LiteralArmMatches()
    {
        await Assert.That(Run("1 switch { 0 => \"zero\", 1 => \"one\", _ => \"other\" }"))
            .IsEqualTo((NValue)new NString("one"));
    }

    [Test]
    public async Task Eval_Switch_BindingArmShadowsScrutinee()
    {
        await Assert.That(Run("7 switch { 0 => \"zero\", n => $\"got {n}\" }"))
            .IsEqualTo((NValue)new NString("got 7"));
    }

    [Test]
    public async Task Eval_Switch_WildcardArmCatchesAll()
    {
        await Assert.That(Run("\"hello\" switch { \"hi\" => 1, _ => 2 }"))
            .IsEqualTo((NValue)new NInt(2));
    }

    [Test]
    public async Task Eval_Switch_NoArmMatches_Throws()
    {
        await Assert.That(() => Run("5 switch { 0 => \"zero\" }"))
            .ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task Eval_PipeIntoBareFunctionRef_OneArgInvocation()
    {
        await Assert.That(Run("[1, 2, 3] | count")).IsEqualTo((NValue)new NInt(3));
    }

    [Test]
    public async Task Eval_DivideByZero_Throws()
    {
        await Assert.That(() => Run("1 / 0")).ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task Eval_UnboundIdentifier_Throws()
    {
        await Assert.That(() => Run("unbound_name")).ThrowsExactly<EvaluatorException>();
    }
}
