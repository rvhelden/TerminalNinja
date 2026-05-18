using System.Collections.Immutable;
using TerminalNinja.Shell.Language.Services;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Tests.Unit.LanguageServer;

/// <summary>
/// Covers the scope-aware overload of <see cref="LanguageService.GetHover(string, Position, IReadOnlyDictionary{string, NValue}?)"/>.
/// Static-catalog behaviour is exercised by the existing HoverTests; here we
/// confirm that providing a scope dictionary enriches the hover with shape +
/// data lines for live bindings.
/// </summary>
public class HoverWithScopeTests
{
    private static IReadOnlyDictionary<string, NValue> Scope(params (string Name, NValue Value)[] entries)
    {
        var d = new Dictionary<string, NValue>(StringComparer.Ordinal);
        foreach (var (n, v) in entries) d[n] = v;
        return d;
    }

    [Test]
    public async Task GetHover_NoScope_FallsBackToStaticBehaviour()
    {
        // The two-arg overload (no scope) keeps its existing contract.
        var h = LanguageService.GetHover("println", new Position(0, 3));
        await Assert.That(h).IsNotNull();
        await Assert.That(h!.Contents).Contains("println");
    }

    [Test]
    public async Task GetHover_WithScope_LiveBinding_AddsShapeAndDataLines()
    {
        var scope = Scope(("xs", new NList(ImmutableArray.Create<NValue>(
            new NInt(1), new NInt(2), new NInt(3)))));
        var h = LanguageService.GetHover("xs", new Position(0, 1), scope);
        await Assert.That(h).IsNotNull();
        await Assert.That(h!.Contents).Contains("shape:");
        await Assert.That(h.Contents).Contains("data:");
        await Assert.That(h.Contents).Contains("list[int]");
    }

    [Test]
    public async Task GetHover_WithScope_NotInScope_FallsBackToStatic()
    {
        var scope = Scope(("xs", new NInt(1)));
        // `println` isn't in scope but is a top-level builtin.
        var h = LanguageService.GetHover("println", new Position(0, 3), scope);
        await Assert.That(h).IsNotNull();
        await Assert.That(h!.Contents).Contains("println");
        await Assert.That(h.Contents.Contains("shape:")).IsFalse();
    }

    [Test]
    public async Task GetHover_WithScope_MemberAccess_NotEnriched()
    {
        // `obj.dump` is a module member — scope shouldn't change the result.
        var scope = Scope(("obj", new NInt(0))); // even if a name happens to shadow the module
        var h = LanguageService.GetHover("obj.dump", new Position(0, 6), scope);
        await Assert.That(h).IsNotNull();
        await Assert.That(h!.Contents).Contains("obj.dump");
        await Assert.That(h.Contents.Contains("shape:")).IsFalse();
    }

    [Test]
    public async Task GetHover_WithScope_RecordValue_DefShowsKeysAndTypes()
    {
        var rec = new NRecord(ImmutableSortedDictionary<string, NValue>.Empty
            .Add("Name", new NString("Alice"))
            .Add("Age", new NInt(30)));
        var scope = Scope(("person", rec));
        var h = LanguageService.GetHover("person", new Position(0, 4), scope);
        await Assert.That(h).IsNotNull();
        // Def output is "record { Name: string, Age: int }"
        await Assert.That(h!.Contents).Contains("record");
        await Assert.That(h.Contents).Contains("Name");
        await Assert.That(h.Contents).Contains("Age");
    }

    [Test]
    public async Task GetHover_WithScope_NameShadowingBuiltin_ScopeWins()
    {
        // A user binds `where` to an int. The scope should win — describe what
        // the evaluator would actually use.
        var scope = Scope(("where", new NInt(42)));
        var h = LanguageService.GetHover("where", new Position(0, 4), scope);
        await Assert.That(h).IsNotNull();
        // Includes both the static signature (top line) AND shape/data.
        await Assert.That(h!.Contents).Contains("where");
        await Assert.That(h.Contents).Contains("shape:");
        await Assert.That(h.Contents).Contains("data:");
        await Assert.That(h.Contents).Contains("42");
    }

    [Test]
    public async Task GetHover_WithScope_FunctionValue_DefShowsArity()
    {
        NValue fnValue = new NFunc(args => args[0], 1);
        var scope = Scope(("identity", fnValue));
        var h = LanguageService.GetHover("identity", new Position(0, 5), scope);
        await Assert.That(h).IsNotNull();
        await Assert.That(h!.Contents).Contains("fn(arity=1)");
    }

    [Test]
    public async Task ValueFormatter_Dump_AnnotatesValuesWithTypes()
    {
        var v = new NList(ImmutableArray.Create<NValue>(new NInt(1), new NString("two")));
        var dump = ValueFormatter.Dump(v);
        await Assert.That(dump).Contains("1 :: int");
        await Assert.That(dump).Contains("\"two\" :: string");
        await Assert.That(dump.EndsWith(":: list")).IsTrue();
    }

    [Test]
    public async Task ValueFormatter_Def_ListOfRecords_ReportsListOfRecord()
    {
        var rec = new NRecord(ImmutableSortedDictionary<string, NValue>.Empty.Add("k", new NInt(1)));
        var v = new NList(ImmutableArray.Create<NValue>(rec, rec));
        await Assert.That(ValueFormatter.Def(v)).IsEqualTo("list[record]");
    }
}
