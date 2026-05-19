using System.Collections.Immutable;
using TerminalNinja.Shell.Builtins;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Tests.Unit;

/// <summary>
/// Coverage for the conversion family in the <c>obj</c> module:
/// record ↔ pairs, record ↔ keys/values, and table ↔ rows/columns. Failure
/// modes (non-uniform tables, duplicate headers, shape mismatches) get the
/// same EvaluatorException-with-clear-message treatment.
/// </summary>
public class ObjConversionTests
{
    private static NValue Run(string source)
        => NinjaEvaluator.EvalSource(source, BuiltinRegistry.CreateDefaultEnv()).Value;

    private static NList AsList(NValue v) => v switch
    {
        NList l => l,
        NSeq s => new NList(ImmutableArray.CreateRange(s.Items)),
        _ => throw new InvalidOperationException($"expected NList/NSeq, got {v.GetType().Name}")
    };

    // ─── pairs / from_pairs ─────────────────────────────────────────────────

    [Test]
    public async Task ObjPairs_OnRecord_ReturnsListOfKeyValueRecords()
    {
        var v = Run("obj.pairs({ Name: \"a\", Age: 40 })");
        var list = AsList(v);
        await Assert.That(list.Items.Length).IsEqualTo(2);
        if (list.Items[0] is not NRecord r0) throw new InvalidOperationException();
        await Assert.That(r0.Fields["Key"]).IsEqualTo((NValue)new NString("Age"));
        await Assert.That(r0.Fields["Value"]).IsEqualTo((NValue)new NInt(40));
    }

    [Test]
    public async Task ObjPairs_OnEmptyRecord_ReturnsEmptyList()
    {
        var list = AsList(Run("obj.pairs({ })"));
        await Assert.That(list.Items.Length).IsEqualTo(0);
    }

    [Test]
    public async Task ObjPairs_RoundTripsViaFromPairs()
    {
        var v = Run("obj.from_pairs(obj.pairs({ Name: \"a\", Age: 40 }))");
        if (v is not NRecord rec) throw new InvalidOperationException();
        await Assert.That(rec.Fields["Name"]).IsEqualTo((NValue)new NString("a"));
        await Assert.That(rec.Fields["Age"]).IsEqualTo((NValue)new NInt(40));
    }

    [Test]
    public async Task ObjFromPairs_DuplicateKey_Throws()
    {
        await Assert.That(() => Run(
            "obj.from_pairs([{ Key: \"x\", Value: 1 }, { Key: \"x\", Value: 2 }])"))
            .ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task ObjFromPairs_MissingKeyOrValue_Throws()
    {
        await Assert.That(() => Run("obj.from_pairs([{ Key: \"x\" }])")).ThrowsExactly<EvaluatorException>();
        await Assert.That(() => Run("obj.from_pairs([{ Value: 1 }])")).ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task ObjFromPairs_NonStringKey_Throws()
    {
        await Assert.That(() => Run("obj.from_pairs([{ Key: 42, Value: 1 }])")).ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task ObjPairs_OnNonRecord_Throws()
    {
        await Assert.That(() => Run("obj.pairs([1, 2])")).ThrowsExactly<EvaluatorException>();
    }

    // ─── keys / values ──────────────────────────────────────────────────────

    [Test]
    public async Task ObjKeys_OnRecord_ReturnsStringList()
    {
        var list = AsList(Run("obj.keys({ b: 2, a: 1, c: 3 })"));
        // NRecord sorts keys, so order is a, b, c.
        await Assert.That(list.Items[0]).IsEqualTo((NValue)new NString("a"));
        await Assert.That(list.Items[1]).IsEqualTo((NValue)new NString("b"));
        await Assert.That(list.Items[2]).IsEqualTo((NValue)new NString("c"));
    }

    [Test]
    public async Task ObjValues_OnRecord_ReturnsValueList()
    {
        var list = AsList(Run("obj.values({ a: 1, b: 2 })"));
        await Assert.That(list.Items[0]).IsEqualTo((NValue)new NInt(1));
        await Assert.That(list.Items[1]).IsEqualTo((NValue)new NInt(2));
    }

    // ─── from_rows / to_rows ────────────────────────────────────────────────

    [Test]
    public async Task ObjFromRows_HeadersAndData_ProducesTable()
    {
        var v = Run(
            "obj.from_rows([" +
            "  [\"Name\", \"Age\"]," +
            "  [\"Alice\", 30]," +
            "  [\"Bob\", 25]" +
            "])");
        var list = AsList(v);
        await Assert.That(list.Items.Length).IsEqualTo(2);
        if (list.Items[0] is not NRecord r0) throw new InvalidOperationException();
        if (list.Items[1] is not NRecord r1) throw new InvalidOperationException();
        await Assert.That(r0.Fields["Name"]).IsEqualTo((NValue)new NString("Alice"));
        await Assert.That(r0.Fields["Age"]).IsEqualTo((NValue)new NInt(30));
        await Assert.That(r1.Fields["Name"]).IsEqualTo((NValue)new NString("Bob"));
        await Assert.That(r1.Fields["Age"]).IsEqualTo((NValue)new NInt(25));
    }

    [Test]
    public async Task ObjFromRows_HeadersOnly_ReturnsEmptyList()
    {
        var list = AsList(Run("obj.from_rows([[\"a\", \"b\"]])"));
        await Assert.That(list.Items.Length).IsEqualTo(0);
    }

    [Test]
    public async Task ObjFromRows_EmptyInput_ReturnsEmptyList()
    {
        var list = AsList(Run("obj.from_rows([])"));
        await Assert.That(list.Items.Length).IsEqualTo(0);
    }

    [Test]
    public async Task ObjFromRows_NonStringHeader_Throws()
    {
        await Assert.That(() => Run("obj.from_rows([[1, 2], [3, 4]])"))
            .ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task ObjFromRows_DuplicateHeader_Throws()
    {
        await Assert.That(() => Run("obj.from_rows([[\"a\", \"a\"], [1, 2]])"))
            .ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task ObjFromRows_RowWidthMismatch_Throws()
    {
        await Assert.That(() => Run("obj.from_rows([[\"a\", \"b\"], [1, 2, 3]])"))
            .ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task ObjToRows_OnTable_ReturnsHeadersPlusRows()
    {
        var v = Run("obj.to_rows([{ Name: \"a\", Age: 1 }, { Name: \"b\", Age: 2 }])");
        var list = AsList(v);
        await Assert.That(list.Items.Length).IsEqualTo(3);
        // First row = headers (sorted, since NRecord stores keys sorted).
        if (list.Items[0] is not NList headers) throw new InvalidOperationException();
        await Assert.That(headers.Items[0]).IsEqualTo((NValue)new NString("Age"));
        await Assert.That(headers.Items[1]).IsEqualTo((NValue)new NString("Name"));
    }

    [Test]
    public async Task ObjToRows_RaggedTable_FillsMissingWithNUnit()
    {
        // Loosened semantics: union of keys becomes the header row, missing cells
        // get NUnit. The first record contributes `a`; the second contributes `b`.
        var v = Run("obj.to_rows([{ a: 1 }, { b: 2 }])");
        var list = AsList(v);
        await Assert.That(list.Items.Length).IsEqualTo(3);
        if (list.Items[0] is not NList headers) throw new InvalidOperationException();
        await Assert.That(headers.Items.Length).IsEqualTo(2);
        if (list.Items[1] is not NList row0) throw new InvalidOperationException();
        if (list.Items[2] is not NList row1) throw new InvalidOperationException();
        // Row 0 has a=1 and b is NUnit; row 1 has b=2 and a is NUnit.
        // (Header order is union, first-seen — { a: 1 } sorts to "a", { b: 2 } to "b".)
        await Assert.That(row0.Items[0]).IsEqualTo((NValue)new NInt(1));
        await Assert.That(row0.Items[1] is NUnit).IsTrue();
        await Assert.That(row1.Items[0] is NUnit).IsTrue();
        await Assert.That(row1.Items[1]).IsEqualTo((NValue)new NInt(2));
    }

    [Test]
    public async Task ObjFromRows_RoundTripsViaToRows()
    {
        var v = Run(
            "obj.from_rows(obj.to_rows([{ Name: \"a\", Age: 1 }, { Name: \"b\", Age: 2 }]))");
        var list = AsList(v);
        await Assert.That(list.Items.Length).IsEqualTo(2);
    }

    // ─── columns / from_columns ─────────────────────────────────────────────

    [Test]
    public async Task ObjColumns_TableToColumnMajor()
    {
        var v = Run("obj.columns([{ Name: \"a\", N: 1 }, { Name: \"b\", N: 2 }])");
        if (v is not NRecord rec) throw new InvalidOperationException();
        if (rec.Fields["Name"] is not NList names) throw new InvalidOperationException();
        if (rec.Fields["N"] is not NList ns) throw new InvalidOperationException();
        await Assert.That(names.Items[0]).IsEqualTo((NValue)new NString("a"));
        await Assert.That(names.Items[1]).IsEqualTo((NValue)new NString("b"));
        await Assert.That(ns.Items[0]).IsEqualTo((NValue)new NInt(1));
        await Assert.That(ns.Items[1]).IsEqualTo((NValue)new NInt(2));
    }

    [Test]
    public async Task ObjFromColumns_ColumnMajorToTable()
    {
        var v = Run("obj.from_columns({ Name: [\"a\", \"b\"], N: [1, 2] })");
        var list = AsList(v);
        await Assert.That(list.Items.Length).IsEqualTo(2);
        if (list.Items[0] is not NRecord r0) throw new InvalidOperationException();
        await Assert.That(r0.Fields["Name"]).IsEqualTo((NValue)new NString("a"));
        await Assert.That(r0.Fields["N"]).IsEqualTo((NValue)new NInt(1));
    }

    [Test]
    public async Task ObjFromColumns_MismatchedLengths_Throws()
    {
        await Assert.That(() => Run("obj.from_columns({ a: [1, 2], b: [3] })"))
            .ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task ObjFromColumns_NonListValue_Throws()
    {
        await Assert.That(() => Run("obj.from_columns({ a: 42 })"))
            .ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task ObjColumns_FromColumns_RoundTrip()
    {
        var v = Run(
            "obj.from_columns(obj.columns([{ Name: \"a\", N: 1 }, { Name: \"b\", N: 2 }]))");
        var list = AsList(v);
        await Assert.That(list.Items.Length).IsEqualTo(2);
        if (list.Items[0] is not NRecord r0) throw new InvalidOperationException();
        await Assert.That(r0.Fields["N"]).IsEqualTo((NValue)new NInt(1));
    }

    [Test]
    public async Task ObjColumns_AcceptsSequenceInput()
    {
        // The table-flavoured ops should accept either NList or NSeq input.
        var v = Run("[{ N: 1 }, { N: 2 }] | select(r => r) | obj.columns");
        if (v is not NRecord rec) throw new InvalidOperationException();
        if (rec.Fields["N"] is not NList ns) throw new InvalidOperationException();
        await Assert.That(ns.Items.Length).IsEqualTo(2);
    }

    // ─── normalize ──────────────────────────────────────────────────────────

    [Test]
    public async Task ObjNormalize_NoDefaults_FillsMissingWithUnit()
    {
        var list = AsList(Run("obj.normalize([{ Name: \"a\", Age: 30 }, { Name: \"b\" }])"));
        await Assert.That(list.Items.Length).IsEqualTo(2);
        if (list.Items[1] is not NRecord r1) throw new InvalidOperationException();
        await Assert.That(r1.Fields.ContainsKey("Age")).IsTrue();
        await Assert.That(r1.Fields["Age"] is NUnit).IsTrue();
    }

    [Test]
    public async Task ObjNormalize_PerColumnDefault_AppliedOnlyWhereMissing()
    {
        var list = AsList(Run("obj.normalize([{ Name: \"a\", Age: 30 }, { Name: \"b\" }], { Age: 0 })"));
        if (list.Items[0] is not NRecord r0) throw new InvalidOperationException();
        if (list.Items[1] is not NRecord r1) throw new InvalidOperationException();
        // Row 0 had a real Age — defaults don't overwrite it.
        await Assert.That(r0.Fields["Age"]).IsEqualTo((NValue)new NInt(30));
        // Row 1 was missing Age — default applied.
        await Assert.That(r1.Fields["Age"]).IsEqualTo((NValue)new NInt(0));
    }

    [Test]
    public async Task ObjNormalize_ExplicitUnitCell_TreatedAsMissing()
    {
        // A cell whose value is explicitly NUnit is "missing" for normalize purposes.
        var list = AsList(Run("obj.normalize([{ a: 1 }, { a: println(\"\") }], { a: 999 })"));
        if (list.Items[1] is not NRecord r1) throw new InvalidOperationException();
        await Assert.That(r1.Fields["a"]).IsEqualTo((NValue)new NInt(999));
    }

    [Test]
    public async Task ObjNormalize_DefaultsSynthesiseAbsentColumn()
    {
        // A column that doesn't appear in any input row but is named in defaults
        // becomes part of the output schema, filled with the default for every row.
        var list = AsList(Run("obj.normalize([{ Name: \"a\" }, { Name: \"b\" }], { Email: \"n/a\" })"));
        await Assert.That(list.Items.Length).IsEqualTo(2);
        if (list.Items[0] is not NRecord r0) throw new InvalidOperationException();
        if (list.Items[1] is not NRecord r1) throw new InvalidOperationException();
        await Assert.That(r0.Fields["Email"]).IsEqualTo((NValue)new NString("n/a"));
        await Assert.That(r1.Fields["Email"]).IsEqualTo((NValue)new NString("n/a"));
    }

    [Test]
    public async Task ObjNormalize_EmptyTable_StaysEmpty()
    {
        var list = AsList(Run("obj.normalize([])"));
        await Assert.That(list.Items.Length).IsEqualTo(0);

        var listWithDefaults = AsList(Run("obj.normalize([], { x: 0 })"));
        await Assert.That(listWithDefaults.Items.Length).IsEqualTo(0);
    }

    [Test]
    public async Task ObjNormalize_AlreadyUniform_IsIdempotent()
    {
        var v1 = Run("[{ a: 1, b: 2 }, { a: 3, b: 4 }] | obj.normalize");
        var v2 = Run("[{ a: 1, b: 2 }, { a: 3, b: 4 }] | obj.normalize | obj.normalize");
        await Assert.That(NValueOps.Equals(v1, v2)).IsTrue();
    }

    [Test]
    public async Task ObjNormalize_NonRecordItem_Throws()
    {
        await Assert.That(() => Run("obj.normalize([{ a: 1 }, 42])"))
            .ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task ObjNormalize_DefaultsNotRecord_Throws()
    {
        await Assert.That(() => Run("obj.normalize([{ a: 1 }], 42)"))
            .ThrowsExactly<EvaluatorException>();
    }
}
