using TerminalNinja.Shell.Ast;
using TerminalNinja.Shell.Parser;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Language.Tests.Unit;

public class ParserTests
{
    private static string PrintParsed(string source) => AstPrinter.Print(NinjaParser.ParseExpression(source));

    [Test]
    public async Task Parse_IntLiteral_ProducesLit()
    {
        await Assert.That(PrintParsed("42")).IsEqualTo("42");
    }

    [Test]
    public async Task Parse_FloatLiteral_ProducesLit()
    {
        await Assert.That(PrintParsed("3.14")).IsEqualTo("3.14");
    }

    [Test]
    public async Task Parse_StringLiteral_ProducesLit()
    {
        await Assert.That(PrintParsed("\"hello\"")).IsEqualTo("\"hello\"");
    }

    [Test]
    public async Task Parse_BoolLiterals_Produce_Lit_NBool()
    {
        await Assert.That(PrintParsed("true")).IsEqualTo("true");
        await Assert.That(PrintParsed("false")).IsEqualTo("false");
    }

    [Test]
    public async Task Parse_Identifier_ProducesVar()
    {
        await Assert.That(PrintParsed("foo")).IsEqualTo("foo");
    }

    [Test]
    public async Task Parse_BinaryPrecedence_MulBindsTighterThanAdd()
    {
        await Assert.That(PrintParsed("1 + 2 * 3")).IsEqualTo("(1 + (2 * 3))");
    }

    [Test]
    public async Task Parse_Comparison_LooserThanArithmetic()
    {
        await Assert.That(PrintParsed("a + 1 < b * 2")).IsEqualTo("((a + 1) < (b * 2))");
    }

    [Test]
    public async Task Parse_LogicalOps_OrLooserThanAndLooserThanEquality()
    {
        await Assert.That(PrintParsed("a == 1 && b != 2 || c == 3"))
            .IsEqualTo("(((a == 1) && (b != 2)) || (c == 3))");
    }

    [Test]
    public async Task Parse_UnaryMinus_AppliedBeforeBinary()
    {
        await Assert.That(PrintParsed("-1 + 2")).IsEqualTo("((-1) + 2)");
    }

    [Test]
    public async Task Parse_LetIn_ProducesLet()
    {
        await Assert.That(PrintParsed("let x = 42 in x"))
            .IsEqualTo("(let x = 42 in x)");
    }

    [Test]
    public async Task Parse_TopLevelLetWithoutIn_ProducesLetStatement()
    {
        await Assert.That(PrintParsed("let x = 42"))
            .IsEqualTo("(let x = 42)");
    }

    [Test]
    public async Task Parse_SingleParamLambda_NoParens()
    {
        await Assert.That(PrintParsed("x => x * 2"))
            .IsEqualTo("((x) => (x * 2))");
    }

    [Test]
    public async Task Parse_MultiParamLambda_WithParens()
    {
        await Assert.That(PrintParsed("(a, b) => a + b"))
            .IsEqualTo("((a, b) => (a + b))");
    }

    [Test]
    public async Task Parse_Call_AssemblesArgs()
    {
        await Assert.That(PrintParsed("f(1, 2, 3)"))
            .IsEqualTo("f(1, 2, 3)");
    }

    [Test]
    public async Task Parse_ChainedCalls_PostfixAssociation()
    {
        await Assert.That(PrintParsed("f(1)(2)"))
            .IsEqualTo("f(1)(2)");
    }

    [Test]
    public async Task Parse_Pipe_DesugarsToCall_BareFunctionRef()
    {
        await Assert.That(PrintParsed("xs | length"))
            .IsEqualTo("length(xs)");
    }

    [Test]
    public async Task Parse_Pipe_DesugarsToCall_PrependsLhsAsFirstArg()
    {
        await Assert.That(PrintParsed("xs | where(p)"))
            .IsEqualTo("where(xs, p)");
    }

    [Test]
    public async Task Parse_ChainedPipes_LeftAssociative()
    {
        await Assert.That(PrintParsed("xs | where(p) | select(q)"))
            .IsEqualTo("select(where(xs, p), q)");
    }

    [Test]
    public async Task Parse_PipeWithLambdaArg_BindsRhsBeforePipe()
    {
        await Assert.That(PrintParsed("xs | where(x => x > 1)"))
            .IsEqualTo("where(xs, ((x) => (x > 1)))");
    }

    [Test]
    public async Task Parse_RecordLiteral_BareKeys()
    {
        await Assert.That(PrintParsed("{ Name: \"a\", Age: 1 }"))
            .IsEqualTo("{ Name: \"a\", Age: 1 }");
    }

    [Test]
    public async Task Parse_RecordLiteral_QuotedKey()
    {
        await Assert.That(PrintParsed("{ \"first name\": \"Ronald\" }"))
            .IsEqualTo("{ \"first name\": \"Ronald\" }");
    }

    [Test]
    public async Task Parse_RecordLiteral_NewlineSeparator()
    {
        await Assert.That(PrintParsed("{\n  Name: \"a\"\n  Age: 1\n}"))
            .IsEqualTo("{ Name: \"a\", Age: 1 }");
    }

    [Test]
    public async Task Parse_RecordLiteral_DuplicateKeysAreParseError()
    {
        var ex = await Assert.That(() => NinjaParser.ParseExpression("{ x: 1, x: 2 }"))
            .ThrowsExactly<ParserException>();
        await Assert.That(ex!.IsIncomplete).IsFalse();
    }

    [Test]
    public async Task Parse_ListLiteral_BasicAndNewlineSeparated()
    {
        await Assert.That(PrintParsed("[1, 2, 3]")).IsEqualTo("[1, 2, 3]");
        await Assert.That(PrintParsed("[1\n2\n3]")).IsEqualTo("[1, 2, 3]");
    }

    [Test]
    public async Task Parse_RangeLiteral_Inclusive()
    {
        await Assert.That(PrintParsed("1..5")).IsEqualTo("(1..5)");
    }

    [Test]
    public async Task Parse_Range_TighterThanComparison_LooserThanAdd()
    {
        await Assert.That(PrintParsed("a..b + 1")).IsEqualTo("(a..(b + 1))");
        await Assert.That(PrintParsed("a < b..c")).IsEqualTo("(a < (b..c))");
    }

    [Test]
    public async Task Parse_Switch_LiteralArmsAndBindingArm()
    {
        var src = "x switch { 0 => \"zero\", 1 => \"one\", n => \"many\" }";
        await Assert.That(PrintParsed(src))
            .IsEqualTo("x switch { 0 => \"zero\", 1 => \"one\", n => \"many\" }");
    }

    [Test]
    public async Task Parse_Switch_WildcardArm()
    {
        await Assert.That(PrintParsed("x switch { 0 => \"a\", _ => \"b\" }"))
            .IsEqualTo("x switch { 0 => \"a\", _ => \"b\" }");
    }

    [Test]
    public async Task Parse_Switch_NewlineSeparatorBetweenArms()
    {
        await Assert.That(PrintParsed("x switch {\n  0 => \"a\"\n  _ => \"b\"\n}"))
            .IsEqualTo("x switch { 0 => \"a\", _ => \"b\" }");
    }

    [Test]
    public async Task Parse_SwitchArmBodyUsesPipe_ArmsStillBalance()
    {
        // Per plan: pipe inside an arm body must not be confused with arm separators.
        var src = "n switch { 0 => xs, m => xs | select(x => x + m) }";
        await Assert.That(PrintParsed(src))
            .IsEqualTo("n switch { 0 => xs, m => select(xs, ((x) => (x + m))) }");
    }

    [Test]
    public async Task Parse_NestedSwitchInArmBody_Works()
    {
        var src = "x switch { 0 => y switch { 1 => \"a\", _ => \"b\" }, _ => \"c\" }";
        await Assert.That(PrintParsed(src))
            .IsEqualTo("x switch { 0 => y switch { 1 => \"a\", _ => \"b\" }, _ => \"c\" }");
    }

    [Test]
    public async Task Parse_MemberAccess_DotChain()
    {
        await Assert.That(PrintParsed("p.Name")).IsEqualTo("p.Name");
        await Assert.That(PrintParsed("p.Address.City")).IsEqualTo("p.Address.City");
    }

    [Test]
    public async Task Parse_IndexAccess_StringKey()
    {
        await Assert.That(PrintParsed("r[\"first name\"]"))
            .IsEqualTo("r[\"first name\"]");
    }

    [Test]
    public async Task Parse_PostfixChain_MemberThenIndexThenCall()
    {
        await Assert.That(PrintParsed("r.parts[0].split(\" \")"))
            .IsEqualTo("r.parts[0].split(\" \")");
    }

    [Test]
    public async Task Parse_Interpolation_TextAndHole()
    {
        await Assert.That(PrintParsed("$\"hi, {name}!\""))
            .IsEqualTo("$\"hi, {name}!\"");
    }

    [Test]
    public async Task Parse_InterpolationHole_ParsedAsSubExpression()
    {
        // Hole content is re-lexed and parsed; we should see the dot access.
        await Assert.That(PrintParsed("$\"age={p.Age + 1}\""))
            .IsEqualTo("$\"age={(p.Age + 1)}\"");
    }

    [Test]
    public async Task Parse_PwshBlock_PreservesPayloadVerbatim()
    {
        await Assert.That(PrintParsed("pwsh { Get-Date | Select-Object Year }"))
            .IsEqualTo("pwsh { Get-Date | Select-Object Year }");
    }

    [Test]
    public async Task Parse_PwshBlockInsidePipeline_Works()
    {
        await Assert.That(PrintParsed("pwsh { Get-Process } | where(p)"))
            .IsEqualTo("where(pwsh { Get-Process }, p)");
    }

    [Test]
    public async Task Parse_Incomplete_Lambda_RaisesIncompleteParserException()
    {
        // `(a, b) =>` with no body — the parser should mark this as incomplete.
        var ex = await Assert.That(() => NinjaParser.ParseExpression("(a, b) =>"))
            .ThrowsExactly<ParserException>();
        await Assert.That(ex!.IsIncomplete).IsTrue();
    }

    [Test]
    public async Task Parse_Incomplete_OpenBrace_RaisesIncomplete()
    {
        var ex = await Assert.That(() => NinjaParser.ParseExpression("{ a: 1"))
            .ThrowsExactly<ParserException>();
        await Assert.That(ex!.IsIncomplete).IsTrue();
    }

    [Test]
    public async Task Parse_ParenExpression_NotMisidentifiedAsLambda()
    {
        await Assert.That(PrintParsed("(1 + 2)")).IsEqualTo("(1 + 2)");
    }

    [Test]
    public async Task Parse_PipeIntoBareFunctionRef_OneArg()
    {
        // Direct AST inspection — make sure the bare-function form is exactly Call(Var, [lhs]).
        // Compare by Var.Name (records also carry a Span that the parser populates;
        // pattern matching on the case + field is the equality we actually mean).
        var ast = NinjaParser.ParseExpression("xs | println");
        var call = ast as Call;
        await Assert.That(call).IsNotNull();
        await Assert.That(call!.Function is Var { Name: "println" }).IsTrue();
        await Assert.That(call.Args.Length).IsEqualTo(1);
        await Assert.That(call.Args[0] is Var { Name: "xs" }).IsTrue();
    }

    [Test]
    public async Task Parse_LetWithRecursiveLambda_BodyReferencesName()
    {
        // The parser doesn't enforce recursion semantics — that's the evaluator's job —
        // but the AST must contain a Lambda whose body refers to the bound name.
        var src = "let fact = n => n switch { 0 => 1, n => n * fact(n - 1) } in fact(5)";
        var printed = PrintParsed(src);
        // The body of the recursive arm should contain a self-call. The pretty-printer
        // parenthesises binary ops, so we look for `fact(` and `(n - 1)` rather than
        // demanding a specific tokenisation.
        await Assert.That(printed.Contains("fact(")).IsTrue();
        await Assert.That(printed.Contains("(n - 1)")).IsTrue();
        await Assert.That(printed.Contains("in fact(5)")).IsTrue();
    }

    [Test]
    public async Task Parse_TrailingCommaInList_Tolerated()
    {
        await Assert.That(PrintParsed("[1, 2, 3,]")).IsEqualTo("[1, 2, 3]");
    }

    [Test]
    public async Task Parse_TrailingCommaInRecord_Tolerated()
    {
        await Assert.That(PrintParsed("{ a: 1, b: 2, }")).IsEqualTo("{ a: 1, b: 2 }");
    }

    [Test]
    public async Task Parse_TrailingCommaInSwitch_Tolerated()
    {
        await Assert.That(PrintParsed("x switch { 0 => \"a\", _ => \"b\", }"))
            .IsEqualTo("x switch { 0 => \"a\", _ => \"b\" }");
    }
}
