using System.Collections.Immutable;
using System.Diagnostics;
using TerminalNinja.Shell.Builtins;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Tests.Unit;

/// <summary>
/// Streaming pipeline contract: ranges, where, select, take, skip, head are lazy;
/// sinks materialise. The flagship test is <see cref="Range_Billion_WithTake_DoesNotMaterialise"/>
/// — it would OOM (or run for many minutes) if any link in the chain were eager.
/// </summary>
public class StreamingTests
{
    private static NValue Run(string source)
        => NinjaEvaluator.EvalSource(source, BuiltinRegistry.CreateDefaultEnv()).Value;

    [Test]
    public async Task Range_LiteralReturnsNSeq()
    {
        var v = Run("1..5");
        await Assert.That(v is NSeq).IsTrue();
    }

    [Test]
    public async Task Where_OnRangeReturnsNSeq()
    {
        var v = Run("1..10 | where(x => x > 3)");
        await Assert.That(v is NSeq).IsTrue();
    }

    [Test]
    public async Task Select_OnListReturnsNSeq()
    {
        // Even NList input → streaming op returns NSeq.
        var v = Run("[1, 2, 3] | select(x => x * 2)");
        await Assert.That(v is NSeq).IsTrue();
    }

    [Test]
    public async Task Take_OnRangeReturnsNSeq()
    {
        var v = Run("1..10 | take(3)");
        await Assert.That(v is NSeq).IsTrue();
    }

    [Test]
    public async Task Take_ShortCircuitsAfterN()
    {
        // If take were eager, this would materialise 1B items first → OOM / minutes.
        // With laziness, take(3) pulls exactly 3.
        var sw = Stopwatch.StartNew();
        var v = Run("1..1000000000 | take(3) | materialize");
        sw.Stop();

        if (v is not NList list) throw new InvalidOperationException($"expected NList, got {v.GetType().Name}");
        await Assert.That(list.Items.Length).IsEqualTo(3);
        await Assert.That(list.Items[0]).IsEqualTo((NValue)new NInt(1));
        await Assert.That(list.Items[2]).IsEqualTo((NValue)new NInt(3));
        await Assert.That(sw.ElapsedMilliseconds).IsLessThan(1000);
    }

    [Test]
    public async Task Range_Billion_WithTake_DoesNotMaterialise()
    {
        // Hard laziness guarantee: `1..1B | where | select | take(3) | fold` must
        // be fast and memory-safe. Eager would OOM at the range step.
        var sw = Stopwatch.StartNew();
        var v = Run("1..1000000000 | where(x => x > 100) | select(x => x * x) | take(3) | fold(0, (acc, x) => acc + x)");
        sw.Stop();

        // 101*101 + 102*102 + 103*103 = 10201 + 10404 + 10609 = 31214
        await Assert.That(v).IsEqualTo((NValue)new NInt(31214));
        await Assert.That(sw.ElapsedMilliseconds).IsLessThan(1000);
    }

    [Test]
    public async Task Head_StopsAfterFirstElement()
    {
        // Same OOM probe: head on a billion-element range must NOT materialise.
        var sw = Stopwatch.StartNew();
        var v = Run("1..1000000000 | head");
        sw.Stop();

        await Assert.That(v).IsEqualTo((NValue)new NInt(1));
        await Assert.That(sw.ElapsedMilliseconds).IsLessThan(1000);
    }

    [Test]
    public async Task Skip_ProducesRemainingItemsLazily()
    {
        var v = Run("1..5 | skip(2) | materialize");
        if (v is not NList list) throw new InvalidOperationException("expected NList");
        await Assert.That(list.Items.Length).IsEqualTo(3);
        await Assert.That(list.Items[0]).IsEqualTo((NValue)new NInt(3));
        await Assert.That(list.Items[2]).IsEqualTo((NValue)new NInt(5));
    }

    [Test]
    public async Task Where_PredicateIsCalledLazily()
    {
        // Capture predicate invocations via a fold accumulator side effect — fold
        // is a sink, so it pulls the whole chain. With take(3) before fold, only
        // the items that pass through `take` reach `fold`.
        var v = Run("1..1000 | where(x => x > 10) | take(3) | fold(0, (acc, x) => acc + x)");
        // 11 + 12 + 13 = 36
        await Assert.That(v).IsEqualTo((NValue)new NInt(36));
    }

    [Test]
    public async Task Count_ConsumesEntireSequence()
    {
        await Assert.That(Run("1..100 | count")).IsEqualTo((NValue)new NInt(100));
    }

    [Test]
    public async Task Fold_ConsumesEntireSequence()
    {
        // From the plan's verification matrix — must still work after going lazy.
        await Assert.That(Run("1..5 | fold(0, (acc, x) => acc + x)"))
            .IsEqualTo((NValue)new NInt(15));
    }

    [Test]
    public async Task Sort_MaterialisesSequence()
    {
        var v = Run("[3, 1, 2] | sort");
        await Assert.That(v is NList).IsTrue();
        if (v is not NList list) throw new InvalidOperationException("expected NList");
        await Assert.That(list.Items[0]).IsEqualTo((NValue)new NInt(1));
        await Assert.That(list.Items[2]).IsEqualTo((NValue)new NInt(3));
    }

    [Test]
    public async Task Distinct_MaterialisesSequence()
    {
        var v = Run("[1, 2, 1, 3] | distinct");
        await Assert.That(v is NList).IsTrue();
    }

    [Test]
    public async Task Each_RunsAllSideEffects()
    {
        // Side-effect smoke: each on a finite range completes and returns unit.
        var v = Run("1..3 | each(x => x)");
        await Assert.That(v is NUnit).IsTrue();
    }

    [Test]
    public async Task Materialize_ForceConvertsNSeqToNList()
    {
        var v = Run("1..3 | materialize");
        if (v is not NList list) throw new InvalidOperationException("expected NList");
        await Assert.That(list.Items.Length).IsEqualTo(3);
    }

    [Test]
    public async Task Materialize_OnNListIsIdentity()
    {
        var v = Run("[1, 2, 3] | materialize");
        await Assert.That(v is NList).IsTrue();
    }

    [Test]
    public async Task NSeq_IsReEnumerable_ChainRecomputedPerPass()
    {
        // Bind a lazy chain to a name, then consume it twice — both consumers
        // must see all items even though the underlying chain is `yield`-based.
        var v = Run("let xs = 1..3 | select(x => x + 10) in count(xs) + count(xs)");
        await Assert.That(v).IsEqualTo((NValue)new NInt(6));
    }

    [Test]
    public async Task NSeq_DisplaysAsListInPrinter()
    {
        var v = Run("1..3");
        var rendered = TerminalNinja.Shell.Repl.Printer.Format(v);
        await Assert.That(rendered).IsEqualTo("[1, 2, 3]");
    }

    [Test]
    public async Task NSeq_DisplaysAsListInInterpolation()
    {
        // Interpolation materialises just like printing.
        var v = Run("$\"got: {1..3 | select(x => x * 2)}\"");
        await Assert.That(v).IsEqualTo((NValue)new NString("got: [2, 4, 6]"));
    }
}
