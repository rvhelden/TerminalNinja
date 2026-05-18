using TerminalNinja.Highlighting;

namespace TerminalNinja.Tests.Unit.Highlighting;

/// <summary>
/// Smoke tests for the built-in highlighters. The framework guarantees:
/// <list type="bullet">
///   <item><description>Tokens are non-overlapping and ordered by start offset.</description></item>
///   <item><description>Partial / malformed input never throws.</description></item>
///   <item><description>Token start + length stay within the source range.</description></item>
/// </list>
/// </summary>
public class HighlighterTests
{
    private static void AssertWellFormed(string source, IReadOnlyList<SyntaxToken> tokens)
    {
        var lastEnd = 0;
        foreach (var t in tokens)
        {
            if (t.Start < lastEnd)
            {
                throw new InvalidOperationException($"Tokens out of order or overlapping at {t.Start} (last end {lastEnd})");
            }
            if (t.Start < 0 || t.Length < 0 || t.Start + t.Length > source.Length)
            {
                throw new InvalidOperationException($"Token range [{t.Start}, {t.Start + t.Length}) escapes source length {source.Length}");
            }
            lastEnd = t.Start + t.Length;
        }
    }

    // ─── NinjaShell ─────────────────────────────────────────────────────────

    [Test]
    public async Task Ninja_Keywords_AreClassifiedAsKeyword()
    {
        var hl = SyntaxHighlighterRegistry.Get("ninja");
        await Assert.That(hl).IsNotNull();
        var tokens = hl!.Tokenize("let x = 42");
        AssertWellFormed("let x = 42", tokens);

        var letToken = tokens[0];
        await Assert.That(letToken.Kind).IsEqualTo(SyntaxTokenKind.Keyword);
        await Assert.That(letToken.Start).IsEqualTo(0);
        await Assert.That(letToken.Length).IsEqualTo(3);
    }

    [Test]
    public async Task Ninja_NumbersAndStrings_AreClassified()
    {
        var hl = SyntaxHighlighterRegistry.Get("ninja")!;
        var src = """let s = "hi" + 3.14""";
        var tokens = hl.Tokenize(src);
        AssertWellFormed(src, tokens);

        await Assert.That(tokens.Any(t => t.Kind == SyntaxTokenKind.StringLiteral)).IsTrue();
        await Assert.That(tokens.Any(t => t.Kind == SyntaxTokenKind.NumberLiteral)).IsTrue();
        // 3.14 is a single number token (length 4), not two pieces split by '.'
        var number = tokens.First(t => t.Kind == SyntaxTokenKind.NumberLiteral);
        await Assert.That(number.Length).IsEqualTo(4);
    }

    [Test]
    public async Task Ninja_ModulePathHead_IsModuleName()
    {
        var hl = SyntaxHighlighterRegistry.Get("ninja")!;
        var tokens = hl.Tokenize("fs.ls()");

        // First token "fs" classified as ModuleName because it's immediately followed by '.'
        await Assert.That(tokens[0].Kind).IsEqualTo(SyntaxTokenKind.ModuleName);
        // Second token "." is Punctuation
        await Assert.That(tokens[1].Kind).IsEqualTo(SyntaxTokenKind.Punctuation);
        // Third token "ls" — no following dot → plain Identifier
        await Assert.That(tokens[2].Kind).IsEqualTo(SyntaxTokenKind.Identifier);
    }

    [Test]
    public async Task Ninja_UnterminatedString_IsError()
    {
        var hl = SyntaxHighlighterRegistry.Get("ninja")!;
        var src = "let x = \"hello";
        var tokens = hl.Tokenize(src);
        AssertWellFormed(src, tokens);
        await Assert.That(tokens.Any(t => t.Kind == SyntaxTokenKind.Error)).IsTrue();
    }

    [Test]
    public async Task Ninja_Comment_RunsToEndOfLine()
    {
        var hl = SyntaxHighlighterRegistry.Get("ninja")!;
        var src = "let x = 1 # trailing comment\nlet y = 2";
        var tokens = hl.Tokenize(src);
        AssertWellFormed(src, tokens);
        var comment = tokens.First(t => t.Kind == SyntaxTokenKind.Comment);
        // Comment runs from '#' through end-of-line (exclusive of the '\n').
        await Assert.That(src.Substring(comment.Start, comment.Length)).StartsWith("#");
        await Assert.That(src.Substring(comment.Start, comment.Length)).EndsWith("comment");
    }

    // ─── JSON ───────────────────────────────────────────────────────────────

    [Test]
    public async Task Json_Strings_AreClassified()
    {
        var hl = SyntaxHighlighterRegistry.Get("json")!;
        var src = """{"name":"alice"}""";
        var tokens = hl.Tokenize(src);
        AssertWellFormed(src, tokens);
        var strings = tokens.Where(t => t.Kind == SyntaxTokenKind.StringLiteral).ToArray();
        await Assert.That(strings.Length).IsEqualTo(2); // "name" and "alice"
    }

    [Test]
    public async Task Json_NumberLiteral_HandlesFloatAndExponent()
    {
        var hl = SyntaxHighlighterRegistry.Get("json")!;
        var src = "-1.5e10";
        var tokens = hl.Tokenize(src);
        AssertWellFormed(src, tokens);
        await Assert.That(tokens.Count).IsEqualTo(1);
        await Assert.That(tokens[0].Kind).IsEqualTo(SyntaxTokenKind.NumberLiteral);
        await Assert.That(tokens[0].Length).IsEqualTo(src.Length);
    }

    [Test]
    public async Task Json_TrueFalseNull_AreClassified()
    {
        var hl = SyntaxHighlighterRegistry.Get("json")!;
        var src = "[true, false, null]";
        var tokens = hl.Tokenize(src);
        AssertWellFormed(src, tokens);
        await Assert.That(tokens.Any(t => t.Kind == SyntaxTokenKind.BoolLiteral)).IsTrue();
        await Assert.That(tokens.Any(t => t.Kind == SyntaxTokenKind.Keyword)).IsTrue(); // null
    }

    // ─── XML ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Xml_TagAndAttributes_AreClassified()
    {
        var hl = SyntaxHighlighterRegistry.Get("xml")!;
        var src = """<book title="hi" />""";
        var tokens = hl.Tokenize(src);
        AssertWellFormed(src, tokens);
        await Assert.That(tokens.Any(t => t.Kind == SyntaxTokenKind.Tag)).IsTrue();
        await Assert.That(tokens.Any(t => t.Kind == SyntaxTokenKind.AttributeName)).IsTrue();
        await Assert.That(tokens.Any(t => t.Kind == SyntaxTokenKind.AttributeValue)).IsTrue();

        var tag = tokens.First(t => t.Kind == SyntaxTokenKind.Tag);
        await Assert.That(src.Substring(tag.Start, tag.Length)).IsEqualTo("book");
    }

    [Test]
    public async Task Xml_Comment_IsClassified()
    {
        var hl = SyntaxHighlighterRegistry.Get("xml")!;
        var src = "<!-- hello --><a/>";
        var tokens = hl.Tokenize(src);
        AssertWellFormed(src, tokens);
        var comment = tokens.First(t => t.Kind == SyntaxTokenKind.Comment);
        await Assert.That(src.Substring(comment.Start, comment.Length)).IsEqualTo("<!-- hello -->");
    }

    [Test]
    public async Task Registry_KnowsAboutBuiltinsAndNinjaHighlighter()
    {
        var langs = SyntaxHighlighterRegistry.Languages;
        await Assert.That(langs).Contains("json");
        await Assert.That(langs).Contains("xml");
        await Assert.That(langs).Contains("ninja"); // registered by module initializer
    }
}
