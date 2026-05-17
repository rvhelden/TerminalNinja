using TerminalNinja.Shell.Repl;

namespace TerminalNinja.Tests.Unit.Shell;

public class LineAccumulatorTests
{
    [Test]
    public async Task Feed_CompleteExpression_ReturnsCompleteAndResetsBuffer()
    {
        var acc = new LineAccumulator();
        var r = acc.Feed("1 + 2");
        await Assert.That(r.State).IsEqualTo(AccumulatorState.Complete);
        await Assert.That(r.Expression).IsNotNull();
        await Assert.That(acc.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Feed_EmptyLine_ReturnsEmpty()
    {
        var acc = new LineAccumulator();
        var r = acc.Feed("");
        await Assert.That(r.State).IsEqualTo(AccumulatorState.Empty);
        await Assert.That(acc.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Feed_UnclosedRecord_AsksForMoreAndPreservesBuffer()
    {
        var acc = new LineAccumulator();
        var r = acc.Feed("{ a: 1");
        await Assert.That(r.State).IsEqualTo(AccumulatorState.NeedMore);
        await Assert.That(acc.IsEmpty).IsFalse();

        var r2 = acc.Feed(", b: 2 }");
        await Assert.That(r2.State).IsEqualTo(AccumulatorState.Complete);
        await Assert.That(acc.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Feed_PartialLambda_AsksForMore()
    {
        var acc = new LineAccumulator();
        var r = acc.Feed("let f =");
        await Assert.That(r.State).IsEqualTo(AccumulatorState.NeedMore);
        var r2 = acc.Feed("  x => x * 2");
        await Assert.That(r2.State).IsEqualTo(AccumulatorState.Complete);
    }

    [Test]
    public async Task Feed_UnterminatedString_AsksForMore()
    {
        var acc = new LineAccumulator();
        var r = acc.Feed("\"hello");
        await Assert.That(r.State).IsEqualTo(AccumulatorState.NeedMore);
    }

    [Test]
    public async Task Feed_FatalSyntaxError_ReturnsErrorAndResetsBuffer()
    {
        var acc = new LineAccumulator();
        var r = acc.Feed("@ illegal");
        await Assert.That(r.State).IsEqualTo(AccumulatorState.Error);
        await Assert.That(r.ErrorMessage).IsNotNull();
        await Assert.That(acc.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Feed_UnclosedPwshBlock_AsksForMoreUntilClosed()
    {
        var acc = new LineAccumulator();
        var r1 = acc.Feed("pwsh {");
        await Assert.That(r1.State).IsEqualTo(AccumulatorState.NeedMore);
        var r2 = acc.Feed("  Get-Date");
        await Assert.That(r2.State).IsEqualTo(AccumulatorState.NeedMore);
        var r3 = acc.Feed("}");
        await Assert.That(r3.State).IsEqualTo(AccumulatorState.Complete);
    }

    [Test]
    public async Task Feed_TopLevelLetWithoutIn_ParsesAsLetStatement()
    {
        var acc = new LineAccumulator();
        var r = acc.Feed("let x = 42");
        await Assert.That(r.State).IsEqualTo(AccumulatorState.Complete);
        await Assert.That(r.Expression).IsTypeOf<TerminalNinja.Shell.Ast.LetStatement>();
    }

    [Test]
    public async Task Reset_ClearsAccumulatedBuffer()
    {
        var acc = new LineAccumulator();
        acc.Feed("{ a:");
        await Assert.That(acc.IsEmpty).IsFalse();
        acc.Reset();
        await Assert.That(acc.IsEmpty).IsTrue();
    }
}
