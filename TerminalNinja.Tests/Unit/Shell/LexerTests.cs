using TerminalNinja.Shell.Lexer;

namespace TerminalNinja.Tests.Unit.Shell;

public class LexerTests
{
    private static List<Token> Tokenize(string source)
        => NinjaLexer.Tokenize(source).ToList();

    private static List<(TokenKind, string)> Pairs(string source)
        => Tokenize(source).Select(t => (t.Kind, t.Text)).ToList();

    [Test]
    public async Task Tokenize_EmptyString_EmitsEof()
    {
        var tokens = Tokenize("");
        await Assert.That(tokens.Count).IsEqualTo(1);
        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.Eof);
    }

    [Test]
    public async Task Tokenize_IntegerLiteral_EmitsIntLiteral()
    {
        var pairs = Pairs("42");
        await Assert.That(pairs).IsEquivalentTo(new[]
        {
            (TokenKind.IntLiteral, "42"),
            (TokenKind.Eof, ""),
        });
    }

    [Test]
    public async Task Tokenize_FloatLiteral_EmitsFloatLiteral()
    {
        var pairs = Pairs("3.14");
        await Assert.That(pairs).IsEquivalentTo(new[]
        {
            (TokenKind.FloatLiteral, "3.14"),
            (TokenKind.Eof, ""),
        });
    }

    [Test]
    public async Task Tokenize_RangeBetweenIntegers_NotFloat()
    {
        // `1..5` is a range — two IntLiterals separated by DotDot, NOT a malformed float.
        var pairs = Pairs("1..5");
        await Assert.That(pairs).IsEquivalentTo(new[]
        {
            (TokenKind.IntLiteral, "1"),
            (TokenKind.DotDot, ".."),
            (TokenKind.IntLiteral, "5"),
            (TokenKind.Eof, ""),
        });
    }

    [Test]
    public async Task Tokenize_StringLiteralWithEscapes_UnescapesText()
    {
        var pairs = Pairs("\"hello\\n\\\"world\\\"\"");
        await Assert.That(pairs).IsEquivalentTo(new[]
        {
            (TokenKind.StringLiteral, "hello\n\"world\""),
            (TokenKind.Eof, ""),
        });
    }

    [Test]
    public async Task Tokenize_UnterminatedString_IsIncomplete()
    {
        var ex = await Assert.That(() => Tokenize("\"unterminated"))
            .ThrowsExactly<LexerException>();
        await Assert.That(ex!.IsIncomplete).IsTrue();
    }

    [Test]
    public async Task Tokenize_Identifier_EmitsIdentifier()
    {
        var pairs = Pairs("foo_bar42");
        await Assert.That(pairs).IsEquivalentTo(new[]
        {
            (TokenKind.Identifier, "foo_bar42"),
            (TokenKind.Eof, ""),
        });
    }

    [Test]
    public async Task Tokenize_Keywords_EmitKeywordKinds()
    {
        var pairs = Pairs("let in switch pwsh true false");
        var kinds = pairs.Select(p => p.Item1).ToList();
        await Assert.That(kinds).IsEquivalentTo(new[]
        {
            TokenKind.KwLet, TokenKind.KwIn, TokenKind.KwSwitch,
            TokenKind.KwPwsh, TokenKind.KwTrue, TokenKind.KwFalse,
            TokenKind.Eof,
        });
    }

    [Test]
    public async Task Tokenize_LetBinding_ProducesExpectedStream()
    {
        var pairs = Pairs("let n = 42");
        await Assert.That(pairs).IsEquivalentTo(new[]
        {
            (TokenKind.KwLet, "let"),
            (TokenKind.Identifier, "n"),
            (TokenKind.Assign, "="),
            (TokenKind.IntLiteral, "42"),
            (TokenKind.Eof, ""),
        });
    }

    [Test]
    public async Task Tokenize_AllOperators_RecognisedDistinctly()
    {
        var pairs = Pairs("=> == != <= >= < > && || + - * / .. = | . , : ( ) [ ] { }");
        var kinds = pairs.Select(p => p.Item1).ToList();
        await Assert.That(kinds).IsEquivalentTo(new[]
        {
            TokenKind.FatArrow, TokenKind.EqEq, TokenKind.NotEq,
            TokenKind.LessEq, TokenKind.GreaterEq, TokenKind.Less, TokenKind.Greater,
            TokenKind.AndAnd, TokenKind.OrOr,
            TokenKind.Plus, TokenKind.Minus, TokenKind.Star, TokenKind.Slash,
            TokenKind.DotDot, TokenKind.Assign, TokenKind.Pipe, TokenKind.Dot,
            TokenKind.Comma, TokenKind.Colon,
            TokenKind.LParen, TokenKind.RParen,
            TokenKind.LBracket, TokenKind.RBracket,
            TokenKind.LBrace, TokenKind.RBrace,
            TokenKind.Eof,
        });
    }

    [Test]
    public async Task Tokenize_LineComment_Discarded()
    {
        var pairs = Pairs("let x = 1 // a trailing comment\nlet y = 2");
        await Assert.That(pairs).IsEquivalentTo(new[]
        {
            (TokenKind.KwLet, "let"),
            (TokenKind.Identifier, "x"),
            (TokenKind.Assign, "="),
            (TokenKind.IntLiteral, "1"),
            (TokenKind.Newline, "\n"),
            (TokenKind.KwLet, "let"),
            (TokenKind.Identifier, "y"),
            (TokenKind.Assign, "="),
            (TokenKind.IntLiteral, "2"),
            (TokenKind.Eof, ""),
        });
    }

    [Test]
    public async Task Tokenize_Newline_EmittedAsToken()
    {
        var pairs = Pairs("a\nb");
        await Assert.That(pairs).IsEquivalentTo(new[]
        {
            (TokenKind.Identifier, "a"),
            (TokenKind.Newline, "\n"),
            (TokenKind.Identifier, "b"),
            (TokenKind.Eof, ""),
        });
    }

    [Test]
    public async Task Tokenize_InterpolatedString_EmitsStartTextHoleEnd()
    {
        var pairs = Pairs("$\"hello, {name}!\"");
        await Assert.That(pairs).IsEquivalentTo(new[]
        {
            (TokenKind.InterpStart, "$\""),
            (TokenKind.InterpText, "hello, "),
            (TokenKind.InterpHole, "name"),
            (TokenKind.InterpText, "!"),
            (TokenKind.InterpEnd, "\""),
            (TokenKind.Eof, ""),
        });
    }

    [Test]
    public async Task Tokenize_InterpolatedStringWithExpression_CapturesHoleVerbatim()
    {
        var pairs = Pairs("$\"x = {p.Age + 1}\"");
        await Assert.That(pairs).IsEquivalentTo(new[]
        {
            (TokenKind.InterpStart, "$\""),
            (TokenKind.InterpText, "x = "),
            (TokenKind.InterpHole, "p.Age + 1"),
            (TokenKind.InterpEnd, "\""),
            (TokenKind.Eof, ""),
        });
    }

    [Test]
    public async Task Tokenize_InterpolatedStringWithDoubledBraces_EscapesToLiteralBraces()
    {
        // `{{` and `}}` are escapes for literal braces (matching C# interpolated strings).
        var pairs = Pairs("$\"obj={{ a: 1 }}\"");
        await Assert.That(pairs).IsEquivalentTo(new[]
        {
            (TokenKind.InterpStart, "$\""),
            (TokenKind.InterpText, "obj={ a: 1 }"),
            (TokenKind.InterpEnd, "\""),
            (TokenKind.Eof, ""),
        });
    }

    [Test]
    public async Task Tokenize_InterpolatedStringWithHoleContainingRecordLiteral_BalancesBraces()
    {
        // A hole whose content is itself a brace-balanced expression: `{ { a: 1 } }`.
        // The outer `{` opens the hole; the lexer must count brace depth in the hole body
        // so the inner `}` doesn't close the hole prematurely.
        var pairs = Pairs("$\"obj={ { a: 1 } }\"");
        await Assert.That(pairs.Count).IsEqualTo(5);
        await Assert.That(pairs[0]).IsEqualTo((TokenKind.InterpStart, "$\""));
        await Assert.That(pairs[1]).IsEqualTo((TokenKind.InterpText, "obj="));
        await Assert.That(pairs[2].Item1).IsEqualTo(TokenKind.InterpHole);
        await Assert.That(pairs[2].Item2).IsEqualTo(" { a: 1 } ");
        await Assert.That(pairs[3]).IsEqualTo((TokenKind.InterpEnd, "\""));
        await Assert.That(pairs[4].Item1).IsEqualTo(TokenKind.Eof);
    }

    [Test]
    public async Task Tokenize_UnterminatedInterpolation_IsIncomplete()
    {
        var ex = await Assert.That(() => Tokenize("$\"hello, {name"))
            .ThrowsExactly<LexerException>();
        await Assert.That(ex!.IsIncomplete).IsTrue();
    }

    [Test]
    public async Task Tokenize_PwshBlock_CapturesPayloadVerbatim()
    {
        var pairs = Pairs("pwsh { Get-Date | Select-Object Year }");
        await Assert.That(pairs.Count).IsEqualTo(3);
        await Assert.That(pairs[0]).IsEqualTo((TokenKind.KwPwsh, "pwsh"));
        await Assert.That(pairs[1].Item1).IsEqualTo(TokenKind.PwshBlock);
        await Assert.That(pairs[1].Item2).IsEqualTo(" Get-Date | Select-Object Year ");
        await Assert.That(pairs[2].Item1).IsEqualTo(TokenKind.Eof);
    }

    [Test]
    public async Task Tokenize_PwshBlockWithStringContainingBrace_DoesNotUnbalance()
    {
        // Single-quoted PS string contains `}` — it must not close the block early.
        var pairs = Pairs("pwsh { Write-Host '}' }");
        await Assert.That(pairs[1].Item1).IsEqualTo(TokenKind.PwshBlock);
        await Assert.That(pairs[1].Item2).IsEqualTo(" Write-Host '}' ");
    }

    [Test]
    public async Task Tokenize_PwshBlockWithDoubleQuoteAndBacktickEscape_BalancesCorrectly()
    {
        var pairs = Pairs("pwsh { Write-Host \"a `\"b\" }");
        await Assert.That(pairs[1].Item1).IsEqualTo(TokenKind.PwshBlock);
        await Assert.That(pairs[1].Item2).IsEqualTo(" Write-Host \"a `\"b\" ");
    }

    [Test]
    public async Task Tokenize_PwshBlockWithLineComment_BalancesCorrectly()
    {
        var pairs = Pairs("pwsh { # } a comment }\n Get-Date }");
        await Assert.That(pairs[1].Item1).IsEqualTo(TokenKind.PwshBlock);
        // The `}` inside the `#` comment must not close the block.
        await Assert.That(pairs[1].Item2.Contains("a comment")).IsTrue();
        await Assert.That(pairs[1].Item2.Contains("Get-Date")).IsTrue();
    }

    [Test]
    public async Task Tokenize_PwshBlockWithBlockComment_BalancesCorrectly()
    {
        var pairs = Pairs("pwsh { <# nested } #> Get-Date }");
        await Assert.That(pairs[1].Item1).IsEqualTo(TokenKind.PwshBlock);
        await Assert.That(pairs[1].Item2).IsEqualTo(" <# nested } #> Get-Date ");
    }

    [Test]
    public async Task Tokenize_PwshBlockWithHereString_BalancesCorrectly()
    {
        var pairs = Pairs("pwsh { @'\nhello } world\n'@ }");
        await Assert.That(pairs[1].Item1).IsEqualTo(TokenKind.PwshBlock);
        await Assert.That(pairs[1].Item2.Contains("hello } world")).IsTrue();
    }

    [Test]
    public async Task Tokenize_UnterminatedPwshBlock_IsIncomplete()
    {
        var ex = await Assert.That(() => Tokenize("pwsh { Get-Date"))
            .ThrowsExactly<LexerException>();
        await Assert.That(ex!.IsIncomplete).IsTrue();
    }

    [Test]
    public async Task Tokenize_PositionTracking_LineAndColumnsAreOneBased()
    {
        var tokens = Tokenize("foo\n  bar");
        await Assert.That(tokens[0].Line).IsEqualTo(1);
        await Assert.That(tokens[0].Column).IsEqualTo(1);
        await Assert.That(tokens[2].Line).IsEqualTo(2);
        await Assert.That(tokens[2].Column).IsEqualTo(3);
    }

    [Test]
    public async Task Tokenize_Pipeline_PipeIsAlwaysASingleToken()
    {
        var pairs = Pairs("xs | where(x => x > 1)");
        var kinds = pairs.Select(p => p.Item1).ToList();
        await Assert.That(kinds).IsEquivalentTo(new[]
        {
            TokenKind.Identifier, TokenKind.Pipe, TokenKind.Identifier,
            TokenKind.LParen, TokenKind.Identifier, TokenKind.FatArrow,
            TokenKind.Identifier, TokenKind.Greater, TokenKind.IntLiteral,
            TokenKind.RParen, TokenKind.Eof,
        });
    }

    [Test]
    public async Task Tokenize_RecordLiteralWithQuotedKey_LexedAsStringPlusColon()
    {
        var pairs = Pairs("{ \"first name\": \"Ronald\" }");
        await Assert.That(pairs).IsEquivalentTo(new[]
        {
            (TokenKind.LBrace, "{"),
            (TokenKind.StringLiteral, "first name"),
            (TokenKind.Colon, ":"),
            (TokenKind.StringLiteral, "Ronald"),
            (TokenKind.RBrace, "}"),
            (TokenKind.Eof, ""),
        });
    }
}
