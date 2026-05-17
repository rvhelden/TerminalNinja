namespace TerminalNinja.Shell.Lexer;

/// <summary>The tokens NinjaShell's lexer can produce.</summary>
public enum TokenKind
{
    // Literals
    /// <summary>A 64-bit integer literal, e.g. <c>42</c>.</summary>
    IntLiteral,
    /// <summary>A double-precision floating-point literal, e.g. <c>3.14</c>.</summary>
    FloatLiteral,
    /// <summary>A non-interpolated string literal, e.g. <c>"hello"</c>. The token text is the unquoted, unescaped value.</summary>
    StringLiteral,

    // Identifiers + keywords
    /// <summary>An identifier — <c>[A-Za-z_][A-Za-z0-9_]*</c>.</summary>
    Identifier,
    /// <summary>The <c>let</c> keyword.</summary>
    KwLet,
    /// <summary>The <c>in</c> keyword, used as <c>let NAME = EXPR in EXPR</c>.</summary>
    KwIn,
    /// <summary>The <c>switch</c> keyword (C#-style switch expression).</summary>
    KwSwitch,
    /// <summary>The <c>pwsh</c> keyword that introduces a <c>pwsh { ... }</c> block.</summary>
    KwPwsh,
    /// <summary>The <c>true</c> boolean literal.</summary>
    KwTrue,
    /// <summary>The <c>false</c> boolean literal.</summary>
    KwFalse,
    /// <summary>The <c>source</c> keyword that introduces a <c>source(path)</c> top-level statement.</summary>
    KwSource,

    // String interpolation — $"prefix{hole1}mid{hole2}suffix"
    /// <summary>Opening of an interpolated string — the literal <c>$"</c>.</summary>
    InterpStart,
    /// <summary>A literal text segment inside an interpolated string. Token text is the unescaped value.</summary>
    InterpText,
    /// <summary>The verbatim source of a single <c>{ ... }</c> hole inside an interpolated string, without the braces.</summary>
    InterpHole,
    /// <summary>Closing of an interpolated string — the literal <c>"</c>.</summary>
    InterpEnd,

    // PowerShell escape hatch — `pwsh { <verbatim PS source> }`
    /// <summary>Verbatim PowerShell source between the matched braces after a <c>pwsh</c> keyword.</summary>
    PwshBlock,

    // Punctuation
    LParen, RParen,
    LBracket, RBracket,
    LBrace, RBrace,
    Comma,
    Colon,
    Dot,
    Pipe,

    // Operators
    /// <summary>The <c>=&gt;</c> token — used for lambdas and switch arms.</summary>
    FatArrow,
    Assign,
    Plus, Minus, Star, Slash,
    EqEq, NotEq,
    Less, Greater, LessEq, GreaterEq,
    AndAnd, OrOr,
    DotDot,

    /// <summary>A line break. Significant as a soft separator inside <c>[ ]</c>, <c>{ }</c>, and switch arms; consumed elsewhere by the parser.</summary>
    Newline,
    /// <summary>End of input.</summary>
    Eof,
}

/// <summary>A single token emitted by <see cref="Lexer"/>.</summary>
/// <param name="Kind">What kind of token this is.</param>
/// <param name="Text">The semantic text of the token (unescaped, unquoted). For non-text tokens this is the matched source.</param>
/// <param name="Line">1-based line number of the token's start.</param>
/// <param name="Column">1-based column number of the token's start.</param>
public readonly record struct Token(TokenKind Kind, string Text, int Line, int Column)
{
    /// <summary>Debug-friendly formatting: <c>Kind(text)@line:col</c>.</summary>
    public override string ToString() => $"{Kind}({Text})@{Line}:{Column}";
}
