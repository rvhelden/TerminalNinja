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
    public async Task ObjDump_OnList_RecursesWithIndentation()
    {
        var v = Run("obj.dump([1, 2])");
        if (v is not NString s) throw new InvalidOperationException();
        await Assert.That(s.Value.Contains("1 :: int")).IsTrue();
        await Assert.That(s.Value.Contains("2 :: int")).IsTrue();
        await Assert.That(s.Value.EndsWith(":: list")).IsTrue();
    }

    [Test]
    public async Task ObjDump_OnRecord_ShowsKeysAndValueTypes()
    {
        var v = Run("obj.dump({ Name: \"a\", Age: 40 })");
        if (v is not NString s) throw new InvalidOperationException();
        await Assert.That(s.Value.Contains("Name: \"a\" :: string")).IsTrue();
        await Assert.That(s.Value.Contains("Age: 40 :: int")).IsTrue();
        await Assert.That(s.Value.EndsWith(":: record")).IsTrue();
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
        await Assert.That(s.Value.EndsWith(":: list")).IsTrue();
    }
}
