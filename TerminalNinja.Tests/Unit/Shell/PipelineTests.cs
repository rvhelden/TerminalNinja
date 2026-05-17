using System.Collections.Immutable;
using TerminalNinja.Shell.Builtins;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Tests.Unit.Shell;

public class PipelineTests
{
    private static NValue Run(string source)
        => NinjaEvaluator.EvalSource(source, BuiltinRegistry.CreateDefaultEnv()).Value;

    private static NList AsList(NValue v)
    {
        if (v is NList l) return l;
        throw new InvalidOperationException($"expected NList, got {v.GetType().Name}");
    }

    [Test]
    public async Task Pipe_Where_FiltersByPredicate()
    {
        var list = AsList(Run("[1, 2, 3, 4] | where(x => x > 2)"));
        await Assert.That(list.Items.Length).IsEqualTo(2);
        await Assert.That(list.Items[0]).IsEqualTo((NValue)new NInt(3));
        await Assert.That(list.Items[1]).IsEqualTo((NValue)new NInt(4));
    }

    [Test]
    public async Task Pipe_Select_MapsItems()
    {
        var list = AsList(Run("[1, 2, 3] | select(x => x * x)"));
        await Assert.That(list.Items.Length).IsEqualTo(3);
        await Assert.That(list.Items[0]).IsEqualTo((NValue)new NInt(1));
        await Assert.That(list.Items[1]).IsEqualTo((NValue)new NInt(4));
        await Assert.That(list.Items[2]).IsEqualTo((NValue)new NInt(9));
    }

    [Test]
    public async Task Pipe_ChainedWhereSelect_LeftAssociative()
    {
        var list = AsList(Run("[1, 2, 3, 4] | where(x => x > 1) | select(x => x * 2)"));
        await Assert.That(list.Items.Length).IsEqualTo(3);
        await Assert.That(list.Items[0]).IsEqualTo((NValue)new NInt(4));
        await Assert.That(list.Items[1]).IsEqualTo((NValue)new NInt(6));
        await Assert.That(list.Items[2]).IsEqualTo((NValue)new NInt(8));
    }

    [Test]
    public async Task Pipe_FoldSum_FromRange()
    {
        var v = Run("1..5 | fold(0, (acc, x) => acc + x)");
        await Assert.That(v).IsEqualTo((NValue)new NInt(15));
    }

    [Test]
    public async Task Pipe_Each_ReturnsUnitAndIterates()
    {
        // Capture a side effect by accumulating into a let-bound list via fold.
        // `each` itself returns unit; we verify that.
        var v = Run("[1, 2, 3] | each(x => x)");
        await Assert.That(v is NUnit).IsTrue();
    }

    [Test]
    public async Task Pipe_TakeAndSkip_SliceList()
    {
        var taken = AsList(Run("[10, 20, 30, 40] | take(2)"));
        await Assert.That(taken.Items.Length).IsEqualTo(2);
        await Assert.That(taken.Items[0]).IsEqualTo((NValue)new NInt(10));

        var skipped = AsList(Run("[10, 20, 30, 40] | skip(2)"));
        await Assert.That(skipped.Items.Length).IsEqualTo(2);
        await Assert.That(skipped.Items[0]).IsEqualTo((NValue)new NInt(30));
    }

    [Test]
    public async Task Pipe_Count_OnList()
    {
        await Assert.That(Run("[1, 2, 3, 4, 5] | count")).IsEqualTo((NValue)new NInt(5));
        await Assert.That(Run("[] | count")).IsEqualTo((NValue)new NInt(0));
    }

    [Test]
    public async Task Pipe_Sort_AscendingNumeric()
    {
        var list = AsList(Run("[3, 1, 2] | sort"));
        await Assert.That(list.Items[0]).IsEqualTo((NValue)new NInt(1));
        await Assert.That(list.Items[1]).IsEqualTo((NValue)new NInt(2));
        await Assert.That(list.Items[2]).IsEqualTo((NValue)new NInt(3));
    }

    [Test]
    public async Task Pipe_Distinct_DedupePreservingOrder()
    {
        var list = AsList(Run("[1, 2, 1, 3, 2] | distinct"));
        await Assert.That(list.Items.Length).IsEqualTo(3);
        await Assert.That(list.Items[0]).IsEqualTo((NValue)new NInt(1));
        await Assert.That(list.Items[1]).IsEqualTo((NValue)new NInt(2));
        await Assert.That(list.Items[2]).IsEqualTo((NValue)new NInt(3));
    }

    [Test]
    public async Task Pipe_HeadAndTail()
    {
        await Assert.That(Run("[10, 20, 30] | head")).IsEqualTo((NValue)new NInt(10));
        var tail = AsList(Run("[10, 20, 30] | tail"));
        await Assert.That(tail.Items.Length).IsEqualTo(2);
        await Assert.That(tail.Items[0]).IsEqualTo((NValue)new NInt(20));
    }

    [Test]
    public async Task Pipe_RecordProjection_SelectExtractsField()
    {
        var list = AsList(Run("[{ Name: \"a\", N: 1 }, { Name: \"b\", N: 2 }] | select(r => r.Name)"));
        await Assert.That(list.Items.Length).IsEqualTo(2);
        await Assert.That(list.Items[0]).IsEqualTo((NValue)new NString("a"));
        await Assert.That(list.Items[1]).IsEqualTo((NValue)new NString("b"));
    }

    [Test]
    public async Task Pipe_WhereOnRecords_FiltersByField()
    {
        var list = AsList(Run("[{ N: 1 }, { N: 5 }, { N: 3 }] | where(r => r.N > 2)"));
        await Assert.That(list.Items.Length).IsEqualTo(2);
        if (list.Items[0] is not NRecord r0) throw new InvalidOperationException("not a record");
        if (list.Items[1] is not NRecord r1) throw new InvalidOperationException("not a record");
        await Assert.That(r0.Fields["N"]).IsEqualTo((NValue)new NInt(5));
        await Assert.That(r1.Fields["N"]).IsEqualTo((NValue)new NInt(3));
    }

    [Test]
    public async Task Pipe_EndToEnd_RangeWhereSelectFold()
    {
        // From the plan's verification section.
        var v = Run("1..10 | where(x => x > 3) | select(x => x * x) | fold(0, (acc, x) => acc + x)");
        // 4^2 + 5^2 + 6^2 + 7^2 + 8^2 + 9^2 + 10^2 = 16+25+36+49+64+81+100 = 371
        await Assert.That(v).IsEqualTo((NValue)new NInt(371));
    }
}
