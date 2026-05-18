using TerminalNinja.Shell.Language.Services;

namespace TerminalNinja.Tests.Unit.LanguageServer;

public class CompletionTests
{
    private static IReadOnlyList<CompletionItem> At(string source, int line, int character)
        => LanguageService.GetCompletions(source, new Position(line, character));

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
        // Module items report CompletionKind.Module.
        var fs = items.Single(i => i.Label == "fs");
        await Assert.That(fs.Kind).IsEqualTo(CompletionKind.Module);
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
}
