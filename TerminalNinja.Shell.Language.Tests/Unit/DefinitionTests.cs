using TerminalNinja.Shell.Language.Services;

namespace TerminalNinja.Shell.Language.Tests.Unit;

/// <summary>
/// Tests for <see cref="LanguageService.GetDefinition"/>. Resolves an identifier
/// under the cursor to the source range of its declaring top-level
/// <c>let NAME = VALUE</c> statement. Member accesses, unknown names, and
/// unparseable sources all return null.
/// </summary>
public class DefinitionTests
{
    [Test]
    public async Task GetDefinition_OnReferenceToLet_ReturnsLetNameRange()
    {
        // "let x = 42\nx + 1" — cursor on the `x` reference in line 1.
        const string src = "let x = 42\nx + 1";
        var def = LanguageService.GetDefinition(src, new Position(1, 0));

        await Assert.That(def).IsNotNull();
        await Assert.That(def!.NameRange.Start.Line).IsEqualTo(0);
        // "let " is 4 chars → identifier starts at column 4.
        await Assert.That(def.NameRange.Start.Character).IsEqualTo(4);
        await Assert.That(def.NameRange.End.Character).IsEqualTo(5);
    }

    [Test]
    public async Task GetDefinition_OnDeclarationItself_ReturnsItsOwnRange()
    {
        // Cursor sits ON the declared name — go-to-def should still resolve (no-op nav).
        const string src = "let answer = 42";
        var def = LanguageService.GetDefinition(src, new Position(0, 5));

        await Assert.That(def).IsNotNull();
        await Assert.That(def!.NameRange.Start.Character).IsEqualTo(4);
        await Assert.That(def.NameRange.End.Character).IsEqualTo(10);
    }

    [Test]
    public async Task GetDefinition_MultipleLetsWithSameName_PrefersMostRecentBeforeCursor()
    {
        // Shadowing: second `let x` redefines x. A reference after the second let
        // should resolve to the second declaration (matches evaluator semantics).
        const string src = "let x = 1\nlet x = 2\nx";
        var def = LanguageService.GetDefinition(src, new Position(2, 0));

        await Assert.That(def).IsNotNull();
        await Assert.That(def!.NameRange.Start.Line).IsEqualTo(1);
    }

    [Test]
    public async Task GetDefinition_ForwardReference_FallsBackToFirstMatch()
    {
        // No matching let precedes the cursor — but a forward declaration exists.
        // Return that one anyway so the editor can still navigate.
        const string src = "x\nlet x = 99";
        var def = LanguageService.GetDefinition(src, new Position(0, 0));

        await Assert.That(def).IsNotNull();
        await Assert.That(def!.NameRange.Start.Line).IsEqualTo(1);
    }

    [Test]
    public async Task GetDefinition_OnMemberAccess_ReturnsNull()
    {
        // `obj.x` — `x` is a member name, not a let binding. Even if a `let x`
        // exists in the file, member access shouldn't resolve to it.
        const string src = "let x = 42\nfs.x";
        var def = LanguageService.GetDefinition(src, new Position(1, 3));

        await Assert.That(def).IsNull();
    }

    [Test]
    public async Task GetDefinition_OnUnknownIdentifier_ReturnsNull()
    {
        const string src = "let x = 42\nunknownName";
        var def = LanguageService.GetDefinition(src, new Position(1, 5));

        await Assert.That(def).IsNull();
    }

    [Test]
    public async Task GetDefinition_OnUnparseableSource_ReturnsNull()
    {
        // Lexer error → no parse tree → no go-to-def.
        const string src = "@illegal\nlet x = 42";
        var def = LanguageService.GetDefinition(src, new Position(1, 4));

        await Assert.That(def).IsNull();
    }

    [Test]
    public async Task GetDefinition_NotOnAnyIdentifier_ReturnsNull()
    {
        // Cursor on whitespace.
        const string src = "let x = 42\n   ";
        var def = LanguageService.GetDefinition(src, new Position(1, 1));

        await Assert.That(def).IsNull();
    }

    [Test]
    public async Task GetDefinition_FullRangeCoversWholeLetStatement()
    {
        // The FullRange should span the whole `let NAME = VALUE`, useful for previews.
        const string src = "let x = 42\nx";
        var def = LanguageService.GetDefinition(src, new Position(1, 0));

        await Assert.That(def).IsNotNull();
        await Assert.That(def!.FullRange.Start.Line).IsEqualTo(0);
        await Assert.That(def.FullRange.Start.Character).IsEqualTo(0);
    }
}
