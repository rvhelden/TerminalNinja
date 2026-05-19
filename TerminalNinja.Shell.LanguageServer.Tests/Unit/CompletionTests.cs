using TerminalNinja.Shell.Language.Services;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.LanguageServer.Tests.Unit;

public class CompletionTests
{
    private static IReadOnlyList<CompletionItem> At(string source, int line, int character)
        => LanguageService.GetCompletions(source, new Position(line, character));

    private static IReadOnlyList<CompletionItem> At(
        string source, int line, int character,
        IReadOnlyDictionary<string, NValue> scope)
        => LanguageService.GetCompletions(source, new Position(line, character), scope);

    // ─── top-level completion ───────────────────────────────────────────────

    [Test]
    public async Task TopLevel_EmptyPrefix_IncludesBuiltinsAndKeywords()
    {
        var items = At("", 0, 0);
        var labels = items.Select(i => i.Label).ToHashSet();
        await Assert.That(labels.Contains("where")).IsTrue();
        await Assert.That(labels.Contains("let")).IsTrue();
        await Assert.That(labels.Contains("println")).IsTrue();
    }

    [Test]
    public async Task TopLevel_IncludesModulesAsValues()
    {
        var items = At("", 0, 0);
        var labels = items.Select(i => i.Label).ToHashSet();
        await Assert.That(labels.Contains("obj")).IsTrue();
        await Assert.That(labels.Contains("fs")).IsTrue();
        await Assert.That(labels.Contains("env")).IsTrue();
        await Assert.That(labels.Contains("http")).IsTrue();
        // Module items report CompletionKind.Module.
        var fs = items.Single(i => i.Label == "fs");
        await Assert.That(fs.Kind).IsEqualTo(CompletionKind.Module);
    }

    [Test]
    public async Task Member_OnHttpModule_ListsVerbs()
    {
        var items = At("http.", 0, 5);
        var labels = items.Select(i => i.Label).ToHashSet();
        await Assert.That(labels.Contains("get")).IsTrue();
        await Assert.That(labels.Contains("post")).IsTrue();
        await Assert.That(labels.Contains("download")).IsTrue();
        await Assert.That(labels.Contains("stream")).IsTrue();
    }

    [Test]
    public async Task TopLevel_IdentifierPrefix_FiltersAndPreservesPrefixOrder()
    {
        var items = At("se", 0, 2);
        var labels = items.Select(i => i.Label).ToList();
        // All results start with "se".
        await Assert.That(labels.All(l => l.StartsWith("se", StringComparison.Ordinal))).IsTrue();
        await Assert.That(labels).Contains("select");
    }

    [Test]
    public async Task TopLevel_NoSuchPrefix_ReturnsEmpty()
    {
        var items = At("zzz_no_match", 0, 12);
        await Assert.That(items).IsEmpty();
    }

    // ─── member-access completion ───────────────────────────────────────────

    [Test]
    public async Task Member_OnDotWithNothingAfter_ListsAllMembers()
    {
        var items = At("obj.", 0, 4);
        var labels = items.Select(i => i.Label).ToHashSet();
        await Assert.That(labels.Contains("type")).IsTrue();
        await Assert.That(labels.Contains("dump")).IsTrue();
        await Assert.That(labels.Contains("normalize")).IsTrue();
        // None of the top-level builtins should leak in via member completion.
        await Assert.That(labels.Contains("where")).IsFalse();
    }

    [Test]
    public async Task Member_WithPartialPrefix_FiltersMembers()
    {
        var items = At("fs.l", 0, 4);
        var labels = items.Select(i => i.Label).ToList();
        await Assert.That(labels.All(l => l.StartsWith("l", StringComparison.Ordinal))).IsTrue();
        await Assert.That(labels).Contains("ls");
    }

    [Test]
    public async Task Member_OnUnknownModule_ReturnsEmpty()
    {
        var items = At("notamodule.", 0, 11);
        await Assert.That(items).IsEmpty();
    }

    [Test]
    public async Task Member_MidExpression_StillDetected()
    {
        // `xs | obj.du` — cursor at end of "du", should suggest obj.dump.
        var items = At("xs | obj.du", 0, 11);
        var labels = items.Select(i => i.Label).ToHashSet();
        await Assert.That(labels.Contains("dump")).IsTrue();
    }

    // ─── positioning ────────────────────────────────────────────────────────

    [Test]
    public async Task Cursor_BeyondEol_ClampsToLineEnd()
    {
        var items = At("se\nlet x = 1", 0, 100);
        // Should still see top-level completions for the prefix "se".
        var labels = items.Select(i => i.Label).ToList();
        await Assert.That(labels).Contains("select");
    }

    [Test]
    public async Task Cursor_OnLaterLine_AppliesPrefixFromThatLine()
    {
        var items = At("let x = 1\nobj.", 1, 4);
        var labels = items.Select(i => i.Label).ToHashSet();
        await Assert.That(labels.Contains("type")).IsTrue();
    }

    [Test]
    public async Task Cursor_AtZeroZero_NoPrefix_ReturnsTopLevel()
    {
        var items = At("anything", 0, 0);
        // No prefix taken (cursor at column 0), so all top-level entries return.
        var labels = items.Select(i => i.Label).ToHashSet();
        await Assert.That(labels.Contains("where")).IsTrue();
        await Assert.That(labels.Contains("let")).IsTrue();
    }

    // ─── shape ──────────────────────────────────────────────────────────────

    [Test]
    public async Task CompletionItem_HasNonEmptyDetailForBuiltins()
    {
        var items = At("se", 0, 2);
        var select = items.Single(i => i.Label == "select");
        await Assert.That(select.Detail).IsNotNull();
        await Assert.That(select.Detail!.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task CompletionItem_BuiltinCarriesDocumentation()
    {
        var items = At("se", 0, 2);
        var select = items.Single(i => i.Label == "select");
        // Documentation is the longer human-readable description from the catalog.
        await Assert.That(select.Documentation).IsNotNull();
        await Assert.That(select.Documentation!.Length).IsGreaterThan(select.Detail!.Length);
    }

    [Test]
    public async Task CompletionItem_ScopeVariableCarriesShapeAndData()
    {
        var scope = new Dictionary<string, NValue>
        {
            ["greeting"] = new NString("hello"),
        };
        var items = At("gre", 0, 3, scope);
        var greeting = items.Single(i => i.Label == "greeting");
        await Assert.That(greeting.Documentation).IsNotNull();
        await Assert.That(greeting.Documentation!).Contains("shape:");
        await Assert.That(greeting.Documentation!).Contains("data:");
    }

    [Test]
    public async Task CompletionItem_ModuleCarriesMemberListDocumentation()
    {
        var items = At("fs", 0, 2);
        var fs = items.Single(i => i.Label == "fs");
        await Assert.That(fs.Documentation).IsNotNull();
        await Assert.That(fs.Documentation!).Contains("ls");
        await Assert.That(fs.Documentation!).Contains("pwd");
    }

    // ─── scope-aware completion ─────────────────────────────────────────────

    [Test]
    public async Task Scope_UserBinding_AppearsInTopLevelResults()
    {
        var scope = new Dictionary<string, NValue>
        {
            ["greeting"] = new NString("hello"),
        };
        var items = At("gre", 0, 3, scope);
        var greeting = items.SingleOrDefault(i => i.Label == "greeting");
        await Assert.That(greeting).IsNotNull();
        await Assert.That(greeting!.Kind).IsEqualTo(CompletionKind.Variable);
    }

    [Test]
    public async Task Scope_LambdaBinding_KindIsFunction()
    {
        var scope = new Dictionary<string, NValue>
        {
            ["double"] = new NFunc(args => args[0], 1),
        };
        var items = At("d", 0, 1, scope);
        var dbl = items.SingleOrDefault(i => i.Label == "double");
        await Assert.That(dbl).IsNotNull();
        await Assert.That(dbl!.Kind).IsEqualTo(CompletionKind.Function);
    }

    [Test]
    public async Task Scope_BindingShadowingBuiltin_SuppressesBuiltin()
    {
        // User has rebound `where` — only the user version should appear, not the
        // builtin definition. Otherwise the popup misleads users into thinking
        // the original is still in play.
        var scope = new Dictionary<string, NValue>
        {
            ["where"] = new NString("user override"),
        };
        var items = At("where", 0, 5, scope);
        var whereItems = items.Where(i => i.Label == "where").ToList();
        await Assert.That(whereItems.Count).IsEqualTo(1);
        await Assert.That(whereItems[0].Kind).IsEqualTo(CompletionKind.Variable);
    }

    [Test]
    public async Task Scope_NoMatch_FallsThroughToBuiltinsOnly()
    {
        var scope = new Dictionary<string, NValue>
        {
            ["xyz"] = new NInt(1),
        };
        var items = At("se", 0, 2, scope);
        var labels = items.Select(i => i.Label).ToList();
        await Assert.That(labels).Contains("select");
        await Assert.That(labels).DoesNotContain("xyz");
    }

    [Test]
    public async Task Scope_EmptyDictionary_BehavesLikeNullScope()
    {
        var items = At("se", 0, 2, new Dictionary<string, NValue>());
        var labels = items.Select(i => i.Label).ToList();
        await Assert.That(labels).Contains("select");
    }

    // ─── record-field completion ────────────────────────────────────────────

    [Test]
    public async Task Member_OnScopeRecord_ReturnsItsFieldKeys()
    {
        var rec = new NRecord(
            System.Collections.Immutable.ImmutableSortedDictionary
                .CreateRange(StringComparer.Ordinal,
                    new[]
                    {
                        new KeyValuePair<string, NValue>("Name", new NString("Ronald")),
                        new KeyValuePair<string, NValue>("Age", new NInt(40)),
                    }));
        var scope = new Dictionary<string, NValue> { ["p"] = rec };
        var items = At("p.", 0, 2, scope);
        var labels = items.Select(i => i.Label).ToHashSet();
        await Assert.That(labels.Contains("Name")).IsTrue();
        await Assert.That(labels.Contains("Age")).IsTrue();
        // Each field item carries shape + data documentation.
        var name = items.Single(i => i.Label == "Name");
        await Assert.That(name.Kind).IsEqualTo(CompletionKind.Field);
        await Assert.That(name.Documentation!).Contains("Ronald");
    }

    [Test]
    public async Task Member_OnScopeRecordWithPrefix_FiltersFields()
    {
        var rec = new NRecord(
            System.Collections.Immutable.ImmutableSortedDictionary
                .CreateRange(StringComparer.Ordinal,
                    new[]
                    {
                        new KeyValuePair<string, NValue>("Name", new NString("x")),
                        new KeyValuePair<string, NValue>("Age", new NInt(1)),
                    }));
        var scope = new Dictionary<string, NValue> { ["p"] = rec };
        var items = At("p.N", 0, 3, scope);
        await Assert.That(items.Count).IsEqualTo(1);
        await Assert.That(items[0].Label).IsEqualTo("Name");
    }

    [Test]
    public async Task Member_OnScopeNonRecord_ReturnsEmpty()
    {
        var scope = new Dictionary<string, NValue> { ["x"] = new NInt(42) };
        var items = At("x.", 0, 2, scope);
        await Assert.That(items).IsEmpty();
    }

    [Test]
    public async Task Member_BuiltinModuleStillWinsOverShadowingScope()
    {
        // User has a `fs` binding to a record — builtin fs module still wins
        // for `fs.<TAB>` so module behaviour stays predictable.
        var rec = new NRecord(System.Collections.Immutable.ImmutableSortedDictionary
            .Create<string, NValue>(StringComparer.Ordinal)
            .Add("custom", new NString("override")));
        var scope = new Dictionary<string, NValue> { ["fs"] = rec };
        var items = At("fs.", 0, 3, scope);
        var labels = items.Select(i => i.Label).ToHashSet();
        await Assert.That(labels.Contains("ls")).IsTrue();
        await Assert.That(labels.Contains("custom")).IsFalse();
    }

    // ─── interpolation-hole completion ──────────────────────────────────────

    [Test]
    public async Task InterpolationHole_OffersTopLevelCompletionsForBareIdentifier()
    {
        // Cursor inside `$"hello {gre"` — should suggest scope name `greeting`.
        var scope = new Dictionary<string, NValue> { ["greeting"] = new NString("hi") };
        var source = "$\"hello {gre";
        var items = At(source, 0, source.Length, scope);
        var labels = items.Select(i => i.Label).ToHashSet();
        await Assert.That(labels.Contains("greeting")).IsTrue();
    }

    [Test]
    public async Task InterpolationHole_OffersMemberCompletion()
    {
        // Cursor inside `$"x = {fs."` — should offer fs module members.
        var source = "$\"x = {fs.";
        var items = At(source, 0, source.Length);
        var labels = items.Select(i => i.Label).ToHashSet();
        await Assert.That(labels.Contains("ls")).IsTrue();
        await Assert.That(labels.Contains("pwd")).IsTrue();
    }

    [Test]
    public async Task InterpolationHole_OnlyTriggersInsideUnmatchedHole()
    {
        // Closed hole — cursor is back in the literal portion of the string.
        // We don't fire interpolation completion there; the cursor is just
        // inside a string literal which currently has no completion handling
        // (so it falls through to top-level completion of an empty prefix).
        var source = "$\"hello {x} world ";
        var items = At(source, 0, source.Length);
        var labels = items.Select(i => i.Label).ToList();
        await Assert.That(labels).Contains("let");
    }
}
