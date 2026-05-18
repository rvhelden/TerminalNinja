using System.Collections.Immutable;
using TerminalNinja.Shell.Repl;

namespace TerminalNinja.Shell.Tests.Unit;

public class ShellArgTokenizerTests
{
    private static ImmutableArray<ShellArgTokenizer.Token> Tokenize(string s)
    {
        var ok = ShellArgTokenizer.TryTokenize(s, out var toks);
        if (!ok) throw new InvalidOperationException("expected tokenizer to succeed");
        return toks;
    }

    [Test]
    public async Task Empty_ReturnsZeroTokens()
    {
        await Assert.That(Tokenize("").Length).IsEqualTo(0);
    }

    [Test]
    public async Task Whitespace_ReturnsZeroTokens()
    {
        await Assert.That(Tokenize("   \t  ").Length).IsEqualTo(0);
    }

    [Test]
    public async Task ThreeBareWords_ReturnsThreeTokens()
    {
        var toks = Tokenize("a b c");
        await Assert.That(toks.Length).IsEqualTo(3);
        await Assert.That(toks[0].Value).IsEqualTo("a");
        await Assert.That(toks[1].Value).IsEqualTo("b");
        await Assert.That(toks[2].Value).IsEqualTo("c");
        await Assert.That(toks[0].WasQuoted).IsFalse();
    }

    [Test]
    public async Task QuotedString_StaysAsSingleToken()
    {
        var toks = Tokenize("\"a b\"");
        await Assert.That(toks.Length).IsEqualTo(1);
        await Assert.That(toks[0].Value).IsEqualTo("a b");
        await Assert.That(toks[0].WasQuoted).IsTrue();
    }

    [Test]
    public async Task MixedBareAndQuoted_RespectsBoundaries()
    {
        var toks = Tokenize("a \"b c\" d");
        await Assert.That(toks.Length).IsEqualTo(3);
        await Assert.That(toks[0].Value).IsEqualTo("a");
        await Assert.That(toks[0].WasQuoted).IsFalse();
        await Assert.That(toks[1].Value).IsEqualTo("b c");
        await Assert.That(toks[1].WasQuoted).IsTrue();
        await Assert.That(toks[2].Value).IsEqualTo("d");
    }

    [Test]
    public async Task EscapedQuoteInsideQuotes_ProducesLiteralQuote()
    {
        var toks = Tokenize("\"a\\\"b\"");
        await Assert.That(toks.Length).IsEqualTo(1);
        await Assert.That(toks[0].Value).IsEqualTo("a\"b");
        await Assert.That(toks[0].WasQuoted).IsTrue();
    }

    [Test]
    public async Task EscapedBackslashInsideQuotes_ProducesLiteralBackslash()
    {
        var toks = Tokenize("\"a\\\\b\"");
        await Assert.That(toks.Length).IsEqualTo(1);
        await Assert.That(toks[0].Value).IsEqualTo("a\\b");
    }

    [Test]
    public async Task LeadingWhitespace_Stripped()
    {
        var toks = Tokenize("   foo  bar");
        await Assert.That(toks.Length).IsEqualTo(2);
        await Assert.That(toks[0].Value).IsEqualTo("foo");
        await Assert.That(toks[1].Value).IsEqualTo("bar");
    }

    [Test]
    public async Task UnterminatedQuote_ReturnsFalse()
    {
        var ok = ShellArgTokenizer.TryTokenize("\"abc", out _);
        await Assert.That(ok).IsFalse();
    }

    [Test]
    public async Task EmptyQuotedString_ProducesEmptyToken()
    {
        var toks = Tokenize("\"\"");
        await Assert.That(toks.Length).IsEqualTo(1);
        await Assert.That(toks[0].Value).IsEqualTo("");
        await Assert.That(toks[0].WasQuoted).IsTrue();
    }

    [Test]
    public async Task QuotedTokenWithPipe_Allowed()
    {
        var toks = Tokenize("\"a | b\"");
        await Assert.That(toks.Length).IsEqualTo(1);
        await Assert.That(toks[0].Value).IsEqualTo("a | b");
        await Assert.That(toks[0].WasQuoted).IsTrue();
    }
}
