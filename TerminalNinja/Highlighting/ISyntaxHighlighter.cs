namespace TerminalNinja.Highlighting;

/// <summary>
/// Classifies source ranges into <see cref="SyntaxToken"/>s for rendering. Highlighters
/// are pure functions of the input string — no document lifecycle, no diagnostics, no
/// parser state. Implementations live alongside the languages they describe.
/// </summary>
/// <remarks>
/// Highlighters must tolerate partial / malformed input — an editor calls
/// <see cref="Tokenize"/> on every keystroke. Bailing on an unterminated string is fine
/// (emit an <see cref="SyntaxTokenKind.Error"/> spanning the unclosed run); throwing is
/// not.
/// </remarks>
public interface ISyntaxHighlighter
{
    /// <summary>The language identifier this highlighter registers under (e.g.
    /// <c>"ninja"</c>, <c>"json"</c>, <c>"xml"</c>). Lookups are case-sensitive.</summary>
    string Language { get; }

    /// <summary>Classifies <paramref name="source"/> into non-overlapping tokens ordered
    /// by start offset. Regions the highlighter doesn't recognise can be omitted (the
    /// consumer falls back to the theme's default foreground) or returned with
    /// <see cref="SyntaxTokenKind.Default"/>.</summary>
    IReadOnlyList<SyntaxToken> Tokenize(string source);
}
