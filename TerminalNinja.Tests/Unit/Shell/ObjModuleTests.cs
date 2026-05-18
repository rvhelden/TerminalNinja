using TerminalNinja.Shell.Builtins;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Tests.Unit.Shell;

public class ObjModuleTests
{
    private static NValue Run(string source)
        => NinjaEvaluator.EvalSource(source, BuiltinRegistry.CreateDefaultEnv()).Value;

    [Test]
    public async Task ObjType_ReportsCanonicalNames()
    {
        await Assert.That(Run("obj.type(42)")).IsEqualTo((NValue)new NString("int"));
        await Assert.That(Run("obj.type(3.14)")).IsEqualTo((NValue)new NString("float"));
        await Assert.That(Run("obj.type(\"hi\")")).IsEqualTo((NValue)new NString("string"));
        await Assert.That(Run("obj.type(true)")).IsEqualTo((NValue)new NString("bool"));
        await Assert.That(Run("obj.type([1, 2])")).IsEqualTo((NValue)new NString("list"));
        await Assert.That(Run("obj.type({ a: 1 })")).IsEqualTo((NValue)new NString("record"));
        await Assert.That(Run("obj.type(1..3)")).IsEqualTo((NValue)new NString("seq"));
        await Assert.That(Run("obj.type(x => x)")).IsEqualTo((NValue)new NString("fn"));
    }

    [Test]
    public async Task ObjType_OnUnit_ReportsUnit()
    {
        await Assert.That(Run("obj.type(println(1))"))
            .IsEqualTo((NValue)new NString("unit"));
    }

    [Test]
    public async Task ObjSize_OnPrimitivesAndContainers()
    {
        await Assert.That(Run("obj.size(\"hello\")")).IsEqualTo((NValue)new NInt(5));
        await Assert.That(Run("obj.size([10, 20, 30])")).IsEqualTo((NValue)new NInt(3));
        await Assert.That(Run("obj.size({ a: 1, b: 2 })")).IsEqualTo((NValue)new NInt(2));
        await Assert.That(Run("obj.size(1..5)")).IsEqualTo((NValue)new NInt(5));
        await Assert.That(Run("obj.size(println(1))")).IsEqualTo((NValue)new NInt(0));
    }

    [Test]
    public async Task ObjSize_OnUnsupportedType_Throws()
    {
        await Assert.That(() => Run("obj.size(42)")).ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task ObjDump_AnnotatesScalarsWithType()
    {
        await Assert.That(Run("obj.dump(42)")).IsEqualTo((NValue)new NString("42 :: int"));
        await Assert.That(Run("obj.dump(\"hi\")")).IsEqualTo((NValue)new NString("\"hi\" :: string"));
        await Assert.That(Run("obj.dump(true)")).IsEqualTo((NValue)new NString("true :: bool"));
    }

    [Test]
    public async Task ObjDump_OnEmptyContainers_StillShowsType()
    {
        await Assert.That(Run("obj.dump([])")).IsEqualTo((NValue)new NString("[] :: list"));
        await Assert.That(Run("obj.dump({ })"))
            .IsEqualTo((NValue)new NString("{} :: record"));
    }

    [Test]
    public async Task ObjDump_OnList_RendersInlineWithItemCount()
    {
        var v = Run("obj.dump([1, 2])");
        if (v is not NString s) throw new InvalidOperationException();
        await Assert.That(s.Value).Contains("1");
        await Assert.That(s.Value).Contains("2");
        await Assert.That(s.Value).Contains("list (2 items)");
    }

    [Test]
    public async Task ObjDump_OnRecord_RendersAsPropertyTable()
    {
        var v = Run("obj.dump({ Name: \"a\", Age: 40 })");
        if (v is not NString s) throw new InvalidOperationException();
        // Two key|value lines, aligned. Keys are sorted (Age < Name).
        var lines = s.Value.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        await Assert.That(lines.Length).IsEqualTo(2);
        await Assert.That(lines[0]).Contains("Age");
        await Assert.That(lines[0]).Contains("|");
        await Assert.That(lines[0]).Contains("40");
        await Assert.That(lines[1]).Contains("Name");
        await Assert.That(lines[1]).Contains("\"a\"");
        // The property table is pre-formatted text — no trailing type tag.
        await Assert.That(s.Value).DoesNotContain(":: record");
    }

    [Test]
    public async Task ObjDump_DepthLimit_CollapsesNestedRecordsToType()
    {
        // Depth 1 stops expansion at the nested record — should show its type + count.
        var v = Run("obj.dump({ Outer: { Inner: 1 } }, 1)");
        if (v is not NString s) throw new InvalidOperationException();
        await Assert.That(s.Value).Contains("Outer");
        await Assert.That(s.Value).Contains("record (1 fields)");
        // Crucially the inner value did NOT leak through.
        await Assert.That(s.Value.Contains("Inner")).IsFalse();
        await Assert.That(s.Value.Contains(": 1")).IsFalse();
    }

    [Test]
    public async Task ObjDump_DefaultDepth_ExpandsOneNestedLevel()
    {
        // Default depth (2) expands the outer record and its inner record once.
        var v = Run("obj.dump({ Outer: { Inner: 1 } })");
        if (v is not NString s) throw new InvalidOperationException();
        await Assert.That(s.Value).Contains("Outer");
        await Assert.That(s.Value).Contains("Inner");
        await Assert.That(s.Value).Contains("1");
    }

    [Test]
    public async Task ObjTable_OnListOfRecords_ReturnsAlignedTableString()
    {
        var v = Run("obj.table([{ a: 1, b: 2 }, { a: 3, b: 4 }])");
        if (v is not NString s) throw new InvalidOperationException();
        var lines = s.Value.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        // Header + separator + 2 data rows.
        await Assert.That(lines.Length).IsGreaterThanOrEqualTo(4);
        await Assert.That(lines[0]).Contains("a");
        await Assert.That(lines[0]).Contains("b");
    }

    [Test]
    public async Task ObjTable_OnSingleRecord_RendersOneRowTable()
    {
        var v = Run("obj.table({ name: \"alpha\", n: 1 })");
        if (v is not NString s) throw new InvalidOperationException();
        await Assert.That(s.Value).Contains("name");
        await Assert.That(s.Value).Contains("alpha");
    }

    [Test]
    public async Task ObjTable_OnNonContainer_Throws()
    {
        await Assert.That(() => Run("obj.table(42)")).ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task ObjDef_OnRecord_ListsKeysAndTypes_NotData()
    {
        var v = Run("obj.def({ Name: \"a\", Age: 40 })");
        if (v is not NString s) throw new InvalidOperationException();
        await Assert.That(s.Value).Contains("Name: string");
        await Assert.That(s.Value).Contains("Age: int");
        // Crucially the data shouldn't leak into the schema view.
        await Assert.That(s.Value.Contains("\"a\"")).IsFalse();
        await Assert.That(s.Value.Contains("40")).IsFalse();
    }

    [Test]
    public async Task ObjDef_OnUniformList_ReportsElementType()
    {
        await Assert.That(Run("obj.def([1, 2, 3])"))
            .IsEqualTo((NValue)new NString("list[int]"));
    }

    [Test]
    public async Task ObjDef_OnMixedList_ReportsMixed()
    {
        await Assert.That(Run("obj.def([1, \"a\"])"))
            .IsEqualTo((NValue)new NString("list[mixed]"));
    }

    [Test]
    public async Task ObjDef_OnFunction_ReportsArity()
    {
        await Assert.That(Run("obj.def((a, b) => a + b)"))
            .IsEqualTo((NValue)new NString("fn(arity=2)"));
    }

    [Test]
    public async Task ObjModule_IsPipeFriendly()
    {
        // The canonical use: pipe a value into obj.dump.
        var v = Run("[1, 2, 3] | obj.dump");
        if (v is not NString s) throw new InvalidOperationException();
        await Assert.That(s.Value).Contains(":: list");
    }
}
