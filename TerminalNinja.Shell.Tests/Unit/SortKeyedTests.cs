using System.Collections.Immutable;
using TerminalNinja.Shell.Builtins;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Tests.Unit;

/// <summary>
/// Covers the options-record extension to <c>sort</c> (<c>{ by, desc }</c>) and the
/// new <c>reverse</c> sink. Backwards-compat for the zero-options form already lives
/// in PipelineTests.Pipe_Sort_AscendingNumeric.
/// </summary>
public class SortKeyedTests
{
    private static NValue Run(string source)
        => NinjaEvaluator.EvalSource(source, BuiltinRegistry.CreateDefaultEnv()).Value;

    private static NList AsList(NValue v) => v switch
    {
        NList l => l,
        NSeq s => new NList(ImmutableArray.CreateRange(s.Items)),
        _ => throw new InvalidOperationException($"expected NList or NSeq, got {v.GetType().Name}")
    };

    [Test]
    public async Task Sort_NaturalAscending_StillWorks()
    {
        var list = AsList(Run("[3, 1, 2] | sort"));
        await Assert.That(list.Items[0]).IsEqualTo((NValue)new NInt(1));
        await Assert.That(list.Items[2]).IsEqualTo((NValue)new NInt(3));
    }

    [Test]
    public async Task Sort_DescOption_NoKey_ReversesNaturalOrder()
    {
        var list = AsList(Run("[3, 1, 2] | sort({ desc: true })"));
        await Assert.That(list.Items[0]).IsEqualTo((NValue)new NInt(3));
        await Assert.That(list.Items[2]).IsEqualTo((NValue)new NInt(1));
    }

    [Test]
    public async Task Sort_ByKey_AscendingByRecordField()
    {
        var list = AsList(Run("[{ N: 3 }, { N: 1 }, { N: 2 }] | sort({ by: r => r.N })"));
        await Assert.That(list.Items.Length).IsEqualTo(3);
        if (list.Items[0] is not NRecord r0) throw new InvalidOperationException();
        if (list.Items[2] is not NRecord r2) throw new InvalidOperationException();
        await Assert.That(r0.Fields["N"]).IsEqualTo((NValue)new NInt(1));
        await Assert.That(r2.Fields["N"]).IsEqualTo((NValue)new NInt(3));
    }

    [Test]
    public async Task Sort_ByKey_Descending()
    {
        var list = AsList(Run("[{ N: 3 }, { N: 1 }, { N: 2 }] | sort({ by: r => r.N, desc: true })"));
        if (list.Items[0] is not NRecord r0) throw new InvalidOperationException();
        if (list.Items[2] is not NRecord r2) throw new InvalidOperationException();
        await Assert.That(r0.Fields["N"]).IsEqualTo((NValue)new NInt(3));
        await Assert.That(r2.Fields["N"]).IsEqualTo((NValue)new NInt(1));
    }

    [Test]
    public async Task Sort_ByKey_OnStringField()
    {
        var list = AsList(Run("[{ Name: \"charlie\" }, { Name: \"alpha\" }, { Name: \"bravo\" }] | sort({ by: r => r.Name })"));
        if (list.Items[0] is not NRecord r0) throw new InvalidOperationException();
        if (list.Items[2] is not NRecord r2) throw new InvalidOperationException();
        await Assert.That(r0.Fields["Name"]).IsEqualTo((NValue)new NString("alpha"));
        await Assert.That(r2.Fields["Name"]).IsEqualTo((NValue)new NString("charlie"));
    }

    [Test]
    public async Task Sort_ByKey_ComputedExpression()
    {
        // Sort by absolute value: -3 should come before 5.
        var list = AsList(Run("[5, -3, 2] | sort({ by: x => x switch { 0 => 0, n => n * n } })"));
        await Assert.That(list.Items[0]).IsEqualTo((NValue)new NInt(2));    // 4
        await Assert.That(list.Items[1]).IsEqualTo((NValue)new NInt(-3));   // 9
        await Assert.That(list.Items[2]).IsEqualTo((NValue)new NInt(5));    // 25
    }

    [Test]
    public async Task Sort_FromLazySeq_ConsumesAndMaterialises()
    {
        // sort is a sink: lazy NSeq input → eager NList output, contents sorted.
        var list = AsList(Run("1..5 | select(x => -x) | sort"));
        await Assert.That(list.Items.Length).IsEqualTo(5);
        await Assert.That(list.Items[0]).IsEqualTo((NValue)new NInt(-5));
        await Assert.That(list.Items[4]).IsEqualTo((NValue)new NInt(-1));
    }

    [Test]
    public async Task Sort_InvalidOptionType_Throws()
    {
        await Assert.That(() => Run("[1, 2] | sort({ by: 42 })")).ThrowsExactly<EvaluatorException>();
        await Assert.That(() => Run("[1, 2] | sort({ desc: \"true\" })")).ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task Reverse_OnList_ReturnsReversed()
    {
        var list = AsList(Run("[1, 2, 3] | reverse"));
        await Assert.That(list.Items[0]).IsEqualTo((NValue)new NInt(3));
        await Assert.That(list.Items[2]).IsEqualTo((NValue)new NInt(1));
    }

    [Test]
    public async Task Reverse_OnSeq_Materialises()
    {
        var list = AsList(Run("1..5 | reverse"));
        await Assert.That(list.Items[0]).IsEqualTo((NValue)new NInt(5));
        await Assert.That(list.Items[4]).IsEqualTo((NValue)new NInt(1));
    }
}
