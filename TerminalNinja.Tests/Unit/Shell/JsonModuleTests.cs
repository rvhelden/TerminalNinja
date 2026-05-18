using TerminalNinja.Shell.Builtins;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Tests.Unit.Shell;

public class JsonModuleTests
{
    private static NValue Run(string source)
        => NinjaEvaluator.EvalSource(source, BuiltinRegistry.CreateDefaultEnv()).Value;

    // ─── parse ──────────────────────────────────────────────────────────────

    [Test]
    public async Task JsonParse_Primitives()
    {
        await Assert.That(Run("json.parse(\"null\")") is NUnit).IsTrue();
        await Assert.That(Run("json.parse(\"true\")")).IsEqualTo((NValue)new NBool(true));
        await Assert.That(Run("json.parse(\"false\")")).IsEqualTo((NValue)new NBool(false));
        await Assert.That(Run("json.parse(\"42\")")).IsEqualTo((NValue)new NInt(42));
        await Assert.That(Run("json.parse(\"3.14\")")).IsEqualTo((NValue)new NFloat(3.14));
        await Assert.That(Run("json.parse(\"\\\"hello\\\"\")")).IsEqualTo((NValue)new NString("hello"));
    }

    [Test]
    public async Task JsonParse_Array_ProducesNList()
    {
        var v = Run("json.parse(\"[1, 2, 3]\")");
        if (v is not NList list) throw new InvalidOperationException();
        await Assert.That(list.Items.Length).IsEqualTo(3);
    }

    [Test]
    public async Task JsonParse_Object_ProducesNRecord()
    {
        var v = Run("json.parse(\"{\\\"Name\\\":\\\"a\\\",\\\"Age\\\":40}\")");
        if (v is not NRecord rec) throw new InvalidOperationException();
        await Assert.That(rec.Fields["Name"]).IsEqualTo((NValue)new NString("a"));
        await Assert.That(rec.Fields["Age"]).IsEqualTo((NValue)new NInt(40));
    }

    [Test]
    public async Task JsonParse_Malformed_Throws()
    {
        await Assert.That(() => Run("json.parse(\"{not valid}\")"))
            .ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task JsonParse_NonString_Throws()
    {
        await Assert.That(() => Run("json.parse(42)"))
            .ThrowsExactly<EvaluatorException>();
    }

    // ─── stringify ──────────────────────────────────────────────────────────

    [Test]
    public async Task JsonStringify_Primitives_Compact()
    {
        await Assert.That(Run("json.stringify(42)")).IsEqualTo((NValue)new NString("42"));
        await Assert.That(Run("json.stringify(true)")).IsEqualTo((NValue)new NString("true"));
        await Assert.That(Run("json.stringify(false)")).IsEqualTo((NValue)new NString("false"));
        await Assert.That(Run("json.stringify(\"hello\")"))
            .IsEqualTo((NValue)new NString("\"hello\""));
        // NUnit (e.g. from an unused side-effect) → JSON null.
        await Assert.That(Run("json.stringify(println(\"\"))"))
            .IsEqualTo((NValue)new NString("null"));
    }

    [Test]
    public async Task JsonStringify_Array()
    {
        await Assert.That(Run("json.stringify([1, 2, 3])"))
            .IsEqualTo((NValue)new NString("[1,2,3]"));
    }

    [Test]
    public async Task JsonStringify_Object_SortedByKey()
    {
        // NRecord stores keys sorted, so the JSON object keys come out sorted.
        await Assert.That(Run("json.stringify({ Name: \"a\", Age: 40 })"))
            .IsEqualTo((NValue)new NString("{\"Age\":40,\"Name\":\"a\"}"));
    }

    [Test]
    public async Task JsonStringify_Pretty_IndentTwo()
    {
        var v = Run("json.stringify({ a: 1, b: [2, 3] }, { indent: 2 })");
        if (v is not NString s) throw new InvalidOperationException();
        // Indented output should include newlines and 2-space indentation.
        await Assert.That(s.Value.Contains("\n")).IsTrue();
        await Assert.That(s.Value.Contains("  ")).IsTrue();
    }

    [Test]
    public async Task JsonStringify_NSeq_MaterialisesToArray()
    {
        var v = Run("json.stringify(1..3)");
        await Assert.That(v).IsEqualTo((NValue)new NString("[1,2,3]"));
    }

    [Test]
    public async Task JsonStringify_NestedShape()
    {
        var v = Run("json.stringify([{ a: [1, 2] }, { a: [3, 4] }])");
        await Assert.That(v).IsEqualTo((NValue)new NString("[{\"a\":[1,2]},{\"a\":[3,4]}]"));
    }

    [Test]
    public async Task JsonStringify_FloatNaN_Throws()
    {
        // 0/0 in our model isn't easy to produce — use a function value as a stand-in
        // for "unserializable". (NFunc is the documented unserializable case.)
        await Assert.That(() => Run("json.stringify(x => x)"))
            .ThrowsExactly<EvaluatorException>();
    }

    // ─── round trip ─────────────────────────────────────────────────────────

    [Test]
    public async Task JsonParse_ThenStringify_RoundTrip()
    {
        var v = Run("json.stringify(json.parse(\"{\\\"x\\\":[1,2,3],\\\"y\\\":\\\"hi\\\"}\"))");
        await Assert.That(v).IsEqualTo((NValue)new NString("{\"x\":[1,2,3],\"y\":\"hi\"}"));
    }

    [Test]
    public async Task JsonStringify_ThenParse_RoundTrip()
    {
        var v = Run("json.parse(json.stringify({ Name: \"a\", Age: 40 }))");
        if (v is not NRecord rec) throw new InvalidOperationException();
        await Assert.That(rec.Fields["Name"]).IsEqualTo((NValue)new NString("a"));
        await Assert.That(rec.Fields["Age"]).IsEqualTo((NValue)new NInt(40));
    }
}
