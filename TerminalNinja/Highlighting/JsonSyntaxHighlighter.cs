namespace TerminalNinja.Highlighting;

/// <summary>
/// Single-pass JSON tokeniser for highlighting. Recognises strings (with escape support),
/// numbers, the three literal keywords (<c>true</c> / <c>false</c> / <c>null</c>),
/// punctuation, and emits an <see cref="SyntaxTokenKind.Error"/> token for runs the
/// grammar doesn't accept (e.g. an unterminated string runs to end-of-source as Error).
/// Tolerant of partial input — callers can re-tokenise on every keystroke.
/// </summary>
public sealed class JsonSyntaxHighlighter : ISyntaxHighlighter
{
    /// <inheritdoc />
    public string Language => "json";

    /// <inheritdoc />
    public IReadOnlyList<SyntaxToken> Tokenize(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var tokens = new List<SyntaxToken>();
        var i = 0;
        var n = source.Length;

        while (i < n)
        {
            var c = source[i];

            // Whitespace — skip without emitting; consumers fall back to default fg.
            if (c is ' ' or '\t' or '\r' or '\n')
            {
                i++;
                continue;
            }

            // String literal — supports the standard JSON escape set; emits Error if the
            // closing quote is missing.
            if (c == '"')
            {
                var start = i++;
                while (i < n && source[i] != '"')
                {
                    if (source[i] == '\\' && i + 1 < n) i += 2; else i++;
                }
                if (i < n) i++; // consume the closing quote
                var kind = i <= n && start < n && source[i - 1] == '"' && i - 1 != start
                    ? SyntaxTokenKind.StringLiteral
                    : SyntaxTokenKind.Error;
                tokens.Add(new SyntaxToken(start, i - start, kind));
                continue;
            }

            // Number — JSON allows leading `-`, digits, fractional part, and exponent.
            if (c == '-' || (c >= '0' && c <= '9'))
            {
                var start = i;
                if (source[i] == '-') i++;
                while (i < n && source[i] >= '0' && source[i] <= '9') i++;
                if (i < n && source[i] == '.')
                {
                    i++;
                    while (i < n && source[i] >= '0' && source[i] <= '9') i++;
                }
                if (i < n && (source[i] == 'e' || source[i] == 'E'))
                {
                    i++;
                    if (i < n && (source[i] == '+' || source[i] == '-')) i++;
                    while (i < n && source[i] >= '0' && source[i] <= '9') i++;
                }
                tokens.Add(new SyntaxToken(start, i - start, SyntaxTokenKind.NumberLiteral));
                continue;
            }

            // Literals: true / false / null.
            if (MatchKeyword(source, i, "true") || MatchKeyword(source, i, "false"))
            {
                var len = source[i] == 't' ? 4 : 5;
                tokens.Add(new SyntaxToken(i, len, SyntaxTokenKind.BoolLiteral));
                i += len;
                continue;
            }
            if (MatchKeyword(source, i, "null"))
            {
                tokens.Add(new SyntaxToken(i, 4, SyntaxTokenKind.Keyword));
                i += 4;
                continue;
            }

            // Structural punctuation.
            if (c is '{' or '}' or '[' or ']' or ',' or ':')
            {
                tokens.Add(new SyntaxToken(i, 1, SyntaxTokenKind.Punctuation));
                i++;
                continue;
            }

            // Anything else is junk — emit a single-char error so the caller can render it
            // red without dropping subsequent input.
            tokens.Add(new SyntaxToken(i, 1, SyntaxTokenKind.Error));
            i++;
        }

        return tokens;
    }

    private static bool MatchKeyword(string source, int i, string word)
    {
        if (i + word.Length > source.Length) return false;
        for (var k = 0; k < word.Length; k++)
        {
            if (source[i + k] != word[k]) return false;
        }
        // The next char must not continue the identifier.
        if (i + word.Length < source.Length)
        {
            var next = source[i + word.Length];
            if ((next >= 'a' && next <= 'z') || (next >= 'A' && next <= 'Z') || (next >= '0' && next <= '9') || next == '_')
                return false;
        }
        return true;
    }
}
