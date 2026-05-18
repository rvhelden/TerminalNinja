namespace TerminalNinja.Highlighting;

/// <summary>
/// Highlighter for NinjaShell source. Self-contained scanner — does NOT call into
/// <c>TerminalNinja.Shell.Language.Lexer.NinjaLexer</c>, both because that lexer emits
/// <c>(Kind, Text, Line, Column)</c> tokens without byte offsets and because the
/// framework can't take a hard dependency on the Shell language project. Keywords are
/// hand-maintained here to match the lexer's set; the keyword list is small and rarely
/// changes.
/// </summary>
/// <remarks>
/// Grammar must tolerate partial / malformed input — an editor calls this on every
/// keystroke. Unterminated strings emit <see cref="SyntaxTokenKind.Error"/>; stray
/// punctuation gets single-char <see cref="SyntaxTokenKind.Error"/>; mid-token cursor
/// states never throw.
/// </remarks>
public sealed class NinjaSyntaxHighlighter : ISyntaxHighlighter
{
    /// <inheritdoc />
    public string Language => "ninja";

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "let", "in", "switch", "pwsh", "source",
    };

    private static readonly HashSet<string> BoolLiterals = new(StringComparer.Ordinal)
    {
        "true", "false",
    };

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

            if (c is ' ' or '\t' or '\r' or '\n')
            {
                i++;
                continue;
            }

            // Line comment: `# rest of line`
            if (c == '#')
            {
                var start = i++;
                while (i < n && source[i] != '\n') i++;
                tokens.Add(new SyntaxToken(start, i - start, SyntaxTokenKind.Comment));
                continue;
            }

            // String literal: "…" (with backslash escapes) or $"…{hole}…" — for highlighting
            // we treat the whole thing as a string and don't recursively classify holes; a
            // future improvement could carve out the {…} regions as Identifier ranges.
            if (c == '"' || (c == '$' && i + 1 < n && source[i + 1] == '"'))
            {
                var start = i;
                if (c == '$') i++; // consume $
                i++; // consume opening "
                while (i < n && source[i] != '"')
                {
                    if (source[i] == '\\' && i + 1 < n) i += 2;
                    else if (source[i] == '\n') break; // unterminated
                    else i++;
                }
                if (i < n && source[i] == '"')
                {
                    i++;
                    tokens.Add(new SyntaxToken(start, i - start, SyntaxTokenKind.StringLiteral));
                }
                else
                {
                    tokens.Add(new SyntaxToken(start, i - start, SyntaxTokenKind.Error));
                }
                continue;
            }

            // Numeric literal: ints + floats with digits, '.', and exponent. We don't anchor
            // a number on '-' here — that lets `x-1` colour the '-' as an operator (and the
            // negative-literal case `let x = -1` still works because the parser is happy with
            // a unary-minus operator in front of a positive number).
            if (c >= '0' && c <= '9')
            {
                var start = i;
                while (i < n && source[i] >= '0' && source[i] <= '9') i++;
                if (i < n && source[i] == '.' && i + 1 < n && source[i + 1] >= '0' && source[i + 1] <= '9')
                {
                    i++;
                    while (i < n && source[i] >= '0' && source[i] <= '9') i++;
                }
                tokens.Add(new SyntaxToken(start, i - start, SyntaxTokenKind.NumberLiteral));
                continue;
            }

            // Identifier or keyword.
            if (IsIdentifierStart(c))
            {
                var start = i++;
                while (i < n && IsIdentifierPart(source[i])) i++;
                var word = source.Substring(start, i - start);
                SyntaxTokenKind kind;
                if (Keywords.Contains(word))
                {
                    kind = SyntaxTokenKind.Keyword;
                }
                else if (BoolLiterals.Contains(word))
                {
                    kind = SyntaxTokenKind.BoolLiteral;
                }
                else
                {
                    // Heuristic: if a '.' follows this identifier we're looking at a
                    // module/object access — paint the head with the module colour.
                    kind = (i < n && source[i] == '.')
                        ? SyntaxTokenKind.ModuleName
                        : SyntaxTokenKind.Identifier;
                }
                tokens.Add(new SyntaxToken(start, i - start, kind));
                continue;
            }

            // Multi-char operators.
            if (TryMatch(source, i, "=>", out var len)
                || TryMatch(source, i, "==", out len) || TryMatch(source, i, "!=", out len)
                || TryMatch(source, i, "<=", out len) || TryMatch(source, i, ">=", out len)
                || TryMatch(source, i, "&&", out len) || TryMatch(source, i, "||", out len)
                || TryMatch(source, i, "..", out len))
            {
                tokens.Add(new SyntaxToken(i, len, SyntaxTokenKind.Operator));
                i += len;
                continue;
            }

            // Single-char operators.
            if (c is '+' or '-' or '*' or '/' or '=' or '<' or '>' or '!' or '|')
            {
                tokens.Add(new SyntaxToken(i, 1, SyntaxTokenKind.Operator));
                i++;
                continue;
            }

            // Punctuation.
            if (c is '(' or ')' or '[' or ']' or '{' or '}' or ',' or ':' or '.' or ';')
            {
                tokens.Add(new SyntaxToken(i, 1, SyntaxTokenKind.Punctuation));
                i++;
                continue;
            }

            // Unrecognised — single-char Error so consumers can render it red.
            tokens.Add(new SyntaxToken(i, 1, SyntaxTokenKind.Error));
            i++;
        }

        return tokens;
    }

    private static bool IsIdentifierStart(char c)
        => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';

    private static bool IsIdentifierPart(char c)
        => IsIdentifierStart(c) || (c >= '0' && c <= '9');

    private static bool TryMatch(string source, int i, string token, out int length)
    {
        length = token.Length;
        if (i + token.Length > source.Length) return false;
        for (var k = 0; k < token.Length; k++)
        {
            if (source[i + k] != token[k]) return false;
        }
        return true;
    }
}
