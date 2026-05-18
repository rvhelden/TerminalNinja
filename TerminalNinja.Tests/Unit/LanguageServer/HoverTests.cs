using TerminalNinja.Shell.Language.Services;

namespace TerminalNinja.Tests.Unit.LanguageServer;

/// <summary>
/// Tests for <see cref="LanguageService.GetHover"/>. The hover service resolves
/// identifiers (bare names) and <c>module.member</c> paths to their <c>BuiltinCatalog</c>
/// entry — user-defined bindings get no hover because the pure service doesn't have
/// access to the evaluator's runtime <c>Env</c>.
/// </summary>
public class HoverTests
{
    [Test]
    public async Task GetHover_OnTopLevelBuiltin_ReturnsSignature()
    {
        // Cursor at column 5 (inside "where"). Should match `where(seq, predicate)`.
        var hover = LanguageService.GetHover("where", new Position(0, 5));

        await Assert.That(hover).IsNotNull();
        await Assert.That(hover!.Contents).Contains("where");
        await Assert.That(hover.Contents).Contains("seq");
        await Assert.That(hover.Range.Start.Character).IsEqualTo(0);
        await Assert.That(hover.Range.End.Character).IsEqualTo(5);
    }

    [Test]
    public async Task GetHover_OnModuleMember_ReturnsMemberSignature()
    {
        // Cursor at column 7 (inside "ls" of "fs.ls"). Should resolve fs.ls.
        var hover = LanguageService.GetHover("fs.ls(.)", new Position(0, 5));

        await Assert.That(hover).IsNotNull();
        await Assert.That(hover!.Contents).Contains("ls");
        // Range covers just "ls", not the whole "fs.ls" path.
        await Assert.That(hover.Range.Start.Character).IsEqualTo(3);
        await Assert.That(hover.Range.End.Character).IsEqualTo(5);
    }

    [Test]
    public async Task GetHover_OnModuleName_ReturnsModuleSummary()
    {
        // Cursor on "fs" (bare module name).
        var hover = LanguageService.GetHover("fs", new Position(0, 1));

        await Assert.That(hover).IsNotNull();
        await Assert.That(hover!.Contents).Contains("module fs");
        await Assert.That(hover.Contents).Contains("members:");
    }

    [Test]
    public async Task GetHover_OnUnknownIdentifier_ReturnsNull()
    {
        var hover = LanguageService.GetHover("xyzdoesnotexist", new Position(0, 5));
        await Assert.That(hover).IsNull();
    }

    [Test]
    public async Task GetHover_CursorAtEndOfWord_StillResolves()
    {
        // Editor convention: cursor immediately after the last char of a token still
        // hovers that token.
        var hover = LanguageService.GetHover("where", new Position(0, 5));
        await Assert.That(hover).IsNotNull();
        await Assert.That(hover!.Contents).Contains("where");
    }

    [Test]
    public async Task GetHover_OnNumericLiteral_ReturnsNull()
    {
        // Pure-numeric tokens aren't named symbols.
        var hover = LanguageService.GetHover("42", new Position(0, 1));
        await Assert.That(hover).IsNull();
    }

    [Test]
    public async Task GetHover_OnWhitespace_ReturnsNull()
    {
        var hover = LanguageService.GetHover("  where", new Position(0, 1));
        await Assert.That(hover).IsNull();
    }

    [Test]
    public async Task GetHover_OnKeyword_ReturnsKeywordDetail()
    {
        // Pick a real keyword from the catalog: "let".
        var hover = LanguageService.GetHover("let x = 1", new Position(0, 1));
        await Assert.That(hover).IsNotNull();
        await Assert.That(hover!.Contents).Contains("let");
    }
}
