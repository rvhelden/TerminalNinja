using TerminalNinja.Shell.Ast;
using TerminalNinja.Shell.Parser;

namespace TerminalNinja.Shell.Language.Tests.Unit;

/// <summary>
/// Pin the contract that every parsed AST node carries a real <see cref="Span"/>
/// reaching from its first to last consumed token. We don't enforce exact
/// numbers (those are sensitive to grammar details), only that spans are
/// non-empty and roughly point at the right region of source.
/// </summary>
public class ParserSpanTests
{
    [Test]
    public async Task ParsedExpression_HasNonEmptySpan()
    {
        var ast = NinjaParser.ParseExpression("42");
        await Assert.That(ast.Span.IsNone).IsFalse();
        await Assert.That(ast.Span.StartLine).IsEqualTo(1);
        await Assert.That(ast.Span.StartColumn).IsEqualTo(1);
    }

    [Test]
    public async Task LetStatement_SpanCoversLetThroughValue()
    {
        // `let x = 42` — span starts at the `let` keyword (line 1, col 1).
        var script = NinjaParser.ParseScript("let x = 42");
        await Assert.That(script.Length).IsEqualTo(1);
        var let = script[0] as LetStatement;
        await Assert.That(let).IsNotNull();
        await Assert.That(let!.Span.StartLine).IsEqualTo(1);
        await Assert.That(let.Span.StartColumn).IsEqualTo(1);
        await Assert.That(let.Span.EndColumn).IsGreaterThan(let.Span.StartColumn);
    }

    [Test]
    public async Task NestedExpr_SpansPointAtTheirSourceRegions()
    {
        // `let x = 1 + 2` — the inner BinOp should start at column 9 (1-based),
        // i.e. after `let x = `.
        var script = NinjaParser.ParseScript("let x = 1 + 2");
        var let = (LetStatement)script[0];
        var bin = let.Value as BinOp;
        await Assert.That(bin).IsNotNull();
        await Assert.That(bin!.Span.StartLine).IsEqualTo(1);
        await Assert.That(bin.Span.StartColumn).IsEqualTo(9);
    }

    [Test]
    public async Task MultiLineScript_SpansTrackLineNumbers()
    {
        var script = NinjaParser.ParseScript("let a = 1\nlet b = 2");
        await Assert.That(script.Length).IsEqualTo(2);
        var letA = (LetStatement)script[0];
        var letB = (LetStatement)script[1];
        await Assert.That(letA.Span.StartLine).IsEqualTo(1);
        await Assert.That(letB.Span.StartLine).IsEqualTo(2);
    }

    [Test]
    public async Task ListLiteral_SpanCoversBrackets()
    {
        var ast = NinjaParser.ParseExpression("[1, 2, 3]");
        var list = ast as ListLit;
        await Assert.That(list).IsNotNull();
        await Assert.That(list!.Span.StartColumn).IsEqualTo(1);
        // End should be past the closing ']' — i.e. at column 10 or so.
        await Assert.That(list.Span.EndColumn).IsGreaterThan(9);
    }

    [Test]
    public async Task HandConstructedNode_HasSpanNone()
    {
        // Sanity: a record constructed without a span has Span.None.
        var v = new Var("foo", TerminalNinja.Shell.Ast.Span.None);
        await Assert.That(v.Span.IsNone).IsTrue();
    }
}
