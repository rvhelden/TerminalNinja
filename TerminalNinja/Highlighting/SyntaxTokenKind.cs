namespace TerminalNinja.Highlighting;

/// <summary>
/// Language-agnostic classifications a <see cref="ISyntaxHighlighter"/> assigns to source
/// ranges. Highlighters map their own grammar to this small palette; <see cref="SyntaxTheme"/>
/// maps the palette to colors. Adding a new kind here is a deliberate breaking change —
/// prefer reusing an existing kind (e.g. JSON's strings are <see cref="StringLiteral"/>,
/// XML's element names are <see cref="Tag"/>, both grammars share <see cref="Comment"/>).
/// </summary>
public enum SyntaxTokenKind
{
    /// <summary>Unclassified text. Rendered with the theme's default foreground.</summary>
    Default = 0,

    /// <summary>Language keyword (<c>let</c>, <c>in</c>, <c>switch</c>, <c>true</c>, …).</summary>
    Keyword,

    /// <summary>A user-named identifier or function reference.</summary>
    Identifier,

    /// <summary>A string literal, including the surrounding quotes.</summary>
    StringLiteral,

    /// <summary>An integer or float literal.</summary>
    NumberLiteral,

    /// <summary>A boolean literal (<c>true</c> / <c>false</c>). Distinguished from
    /// <see cref="Keyword"/> so themes can colour them like numbers if they prefer.</summary>
    BoolLiteral,

    /// <summary>A line or block comment.</summary>
    Comment,

    /// <summary>Brackets, parens, braces, commas, semicolons.</summary>
    Punctuation,

    /// <summary>Binary or unary operator (<c>+</c>, <c>-</c>, <c>==</c>, <c>&amp;&amp;</c>, …).</summary>
    Operator,

    /// <summary>A module / namespace name (<c>fs</c> in <c>fs.ls()</c>).</summary>
    ModuleName,

    /// <summary>A type name (reserved for grammars that surface types).</summary>
    TypeName,

    /// <summary>An attribute name (XML, future tooltip tags, etc.).</summary>
    AttributeName,

    /// <summary>An attribute value (XML).</summary>
    AttributeValue,

    /// <summary>An XML element / tag name.</summary>
    Tag,

    /// <summary>A range the highlighter knows is malformed; themes typically render this
    /// red. <see cref="LanguageService.GetDiagnostics"/> is the source of truth for
    /// errors — this kind exists for highlighters that catch them inline (e.g. an
    /// unterminated string).</summary>
    Error,

    /// <summary>A known function name — language builtin, library callable, or any
    /// identifier the highlighter has resolved to "this is callable". Distinguished
    /// from <see cref="Identifier"/> so themes can colour <c>where</c>, <c>select</c>,
    /// <c>print</c> differently from unknown / user-defined names.</summary>
    Function,
}
