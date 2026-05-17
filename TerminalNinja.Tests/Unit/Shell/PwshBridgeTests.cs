using TerminalNinja.Shell.Builtins;
using TerminalNinja.Shell.PowerShell;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Tests.Unit.Shell;

public class PwshBridgeTests
{
    private static Env EnvWithBridge() => PwshBridge.Install(BuiltinRegistry.CreateDefaultEnv());

    [Test]
    public async Task JsonToNValue_Null_BecomesUnit()
    {
        var v = JsonToNValue.Parse("null");
        await Assert.That(v is NUnit).IsTrue();
    }

    [Test]
    public async Task JsonToNValue_BooleansAndNumbers()
    {
        await Assert.That(JsonToNValue.Parse("true")).IsEqualTo((NValue)new NBool(true));
        await Assert.That(JsonToNValue.Parse("false")).IsEqualTo((NValue)new NBool(false));
        await Assert.That(JsonToNValue.Parse("42")).IsEqualTo((NValue)new NInt(42));
        await Assert.That(JsonToNValue.Parse("3.14")).IsEqualTo((NValue)new NFloat(3.14));
    }

    [Test]
    public async Task JsonToNValue_StringWithEscapes()
    {
        var v = JsonToNValue.Parse("\"hello\\nworld\"");
        await Assert.That(v).IsEqualTo((NValue)new NString("hello\nworld"));
    }

    [Test]
    public async Task JsonToNValue_Array_BecomesList()
    {
        var v = JsonToNValue.Parse("[1, 2, 3]");
        if (v is not NList list) throw new InvalidOperationException("expected NList");
        await Assert.That(list.Items.Length).IsEqualTo(3);
        await Assert.That(list.Items[0]).IsEqualTo((NValue)new NInt(1));
        await Assert.That(list.Items[2]).IsEqualTo((NValue)new NInt(3));
    }

    [Test]
    public async Task JsonToNValue_Object_BecomesRecord_SortedByKey()
    {
        var v = JsonToNValue.Parse("{\"Name\":\"a\",\"Age\":40}");
        if (v is not NRecord rec) throw new InvalidOperationException("expected NRecord");
        await Assert.That(rec.Fields.Count).IsEqualTo(2);
        await Assert.That(rec.Fields["Name"]).IsEqualTo((NValue)new NString("a"));
        await Assert.That(rec.Fields["Age"]).IsEqualTo((NValue)new NInt(40));
    }

    [Test]
    public async Task JsonToNValue_NestedShape_RecursivelyConverts()
    {
        var v = JsonToNValue.Parse("[{\"k\":[1,{\"deep\":true}]}]");
        if (v is not NList outer) throw new InvalidOperationException("expected NList");
        if (outer.Items[0] is not NRecord r0) throw new InvalidOperationException("expected NRecord");
        if (r0.Fields["k"] is not NList inner) throw new InvalidOperationException("expected nested NList");
        if (inner.Items[1] is not NRecord deepRec) throw new InvalidOperationException("expected nested NRecord");
        await Assert.That(deepRec.Fields["deep"]).IsEqualTo((NValue)new NBool(true));
    }

    [Test]
    public async Task Bridge_ScalarReturn_RoundTripsAsNInt()
    {
        if (!PwshBridge.IsAvailable) return; // no pwsh host — skip
        var v = NinjaEvaluator.EvalSource("pwsh { 42 }", EnvWithBridge()).Value;
        await Assert.That(v).IsEqualTo((NValue)new NInt(42));
    }

    [Test]
    public async Task Bridge_StringReturn_RoundTripsAsNString()
    {
        if (!PwshBridge.IsAvailable) return;
        var v = NinjaEvaluator.EvalSource("pwsh { \"hello\" }", EnvWithBridge()).Value;
        await Assert.That(v).IsEqualTo((NValue)new NString("hello"));
    }

    [Test]
    public async Task Bridge_GetDateSelectObject_ReturnsRecordWithIntFields()
    {
        if (!PwshBridge.IsAvailable) return;
        var v = NinjaEvaluator.EvalSource(
            "pwsh { Get-Date | Select-Object Year, Month, Day }",
            EnvWithBridge()).Value;

        if (v is not NRecord rec) throw new InvalidOperationException($"expected NRecord, got {v.GetType().Name}");
        await Assert.That(rec.Fields.ContainsKey("Year")).IsTrue();
        await Assert.That(rec.Fields.ContainsKey("Month")).IsTrue();
        await Assert.That(rec.Fields.ContainsKey("Day")).IsTrue();
        await Assert.That(rec.Fields["Year"] is NInt).IsTrue();
        await Assert.That(rec.Fields["Month"] is NInt).IsTrue();
        await Assert.That(rec.Fields["Day"] is NInt).IsTrue();
    }

    [Test]
    public async Task Bridge_EndToEndPipeline_PwshIntoNinjaShellWhere()
    {
        if (!PwshBridge.IsAvailable) return;
        // Pull a handful of records from pwsh, then filter and project with NinjaShell.
        var v = NinjaEvaluator.EvalSource(
            "pwsh { @( @{N=1; Name='a'}, @{N=5; Name='b'}, @{N=3; Name='c'} ) | ForEach-Object { [pscustomobject]$_ } } | where(r => r.N > 2) | select(r => r.Name)",
            EnvWithBridge()).Value;

        // The pipeline `pwsh { ... } | where(...) | select(...)` produces an NSeq —
        // materialise to inspect.
        var items = v switch
        {
            NList l => l.Items,
            NSeq s => System.Collections.Immutable.ImmutableArray.CreateRange(s.Items),
            _ => throw new InvalidOperationException($"expected NList or NSeq, got {v.GetType().Name}")
        };
        await Assert.That(items.Length).IsEqualTo(2);
        await Assert.That(items).Contains((NValue)new NString("b"));
        await Assert.That(items).Contains((NValue)new NString("c"));
    }

    [Test]
    public async Task Bridge_ErrorScript_SurfacedAsErrorVariant()
    {
        if (!PwshBridge.IsAvailable) return;
        var v = NinjaEvaluator.EvalSource(
            "pwsh { throw 'boom' }",
            EnvWithBridge()).Value;

        if (v is not NVariant variant) throw new InvalidOperationException($"expected NVariant, got {v.GetType().Name}");
        await Assert.That(variant.Tag).IsEqualTo("Error");
        await Assert.That(variant.Items.Length).IsEqualTo(1);
    }
}
