namespace TerminalNinja.Highlighting;

/// <summary>
/// Highlighter for NinjaShell record output. Recognises two distinct shapes the
/// REPL surfaces:
/// <list type="bullet">
///   <item><b>Record literals</b> — <c>{ Name: "alpha", Age: 40 }</c>. Keys
///   (identifiers followed by <c>:</c>) get the
///   <see cref="SyntaxTokenKind.AttributeName"/> kind so themes color them
///   distinctly from regular identifiers; values inherit their literal-typed
///   kind (string, number, bool).</item>
///   <item><b>Property tables</b> — the vertical <c>key | value</c> layout
///   produced by <c>obj.dump(rec)</c>. The key (any identifier-shaped word at
///   the start of a line) is marked <see cref="SyntaxTokenKind.AttributeName"/>
///   and the <c>|</c> separator gets <see cref="SyntaxTokenKind.Punctuation"/>.</item>
/// </list>
/// </summary>
/// <remarks>
/// Grammar is intentionally permissive: anything that doesn't match a literal,
/// punctuation, or identifier-key falls through to <see cref="SyntaxTokenKind.Default"/>
/// (no token emitted, so the consumer paints with its fallback foreground).
/// This keeps the highlighter useful on lines that mix record output with
/// surrounding prose (e.g. <c>"data:  { Name: \"x\" }"</c>).
/// </remarks>
public sealed class RecordSyntaxHighlighter : ISyntaxHighlighter
{
    /// <inheritdoc />
    public string Language => "record";

    /// <inheritdoc />
    public IReadOnlyList<SyntaxToken> Tokenize(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var tokens = new List<SyntaxToken>();
        var i = 0;
        var n = source.Length;
        var atLineStart = true;

        while (i < n)
        {
            var c = source[i];

            if (c == '\n')
            {
                i++;
                atLineStart = true;
                continue;
            }
            if (c is ' ' or '\t' or '\r')
            {
                i++;
                continue;
            }

            // String literal.
            if (c == '"')
            {
                var start = i++;
                while (i < n && source[i] != '"')
                {
                    if (source[i] == '\\' && i + 1 < n) i += 2;
                    else if (source[i] == '\n') break;
                    else i++;
                }
                var kind = SyntaxTokenKind.StringLiteral;
                if (i < n && source[i] == '"') i++;
                else kind = SyntaxTokenKind.Error;
                tokens.Add(new SyntaxToken(start, i - start, kind));
                atLineStart = false;
                continue;
            }

            // Number literal — optional minus + digits + fraction.
            if (c == '-' || (c >= '0' && c <= '9'))
            {
                var start = i;
                if (source[i] == '-') i++;
                bool sawDigit = false;
                while (i < n && source[i] >= '0' && source[i] <= '9') { i++; sawDigit = true; }
                if (i < n && source[i] == '.' && i + 1 < n && source[i + 1] >= '0' && source[i + 1] <= '9')
                {
                    i++;
                    while (i < n && source[i] >= '0' && source[i] <= '9') i++;
                }
                if (sawDigit)
                    tokens.Add(new SyntaxToken(start, i - start, SyntaxTokenKind.NumberLiteral));
                else
                    tokens.Add(new SyntaxToken(start, 1, SyntaxTokenKind.Operator));
                atLineStart = false;
                continue;
            }

            // Identifier (or bool literal). Followed-by-':' or first-on-line +
            // followed-by-'|' makes it a record key.
            if (IsIdentStart(c))
            {
                var start = i++;
                while (i < n && IsIdentPart(source[i])) i++;
                var word = source.Substring(start, i - start);
                SyntaxTokenKind kind;
                if (word is "true" or "false") kind = SyntaxTokenKind.BoolLiteral;
                else if (i < n && source[i] == ':') kind = SyntaxTokenKind.AttributeName;
                else if (atLineStart && LooksLikePropertyTableKey(source, i)) kind = SyntaxTokenKind.AttributeName;
                else kind = SyntaxTokenKind.Identifier;
                tokens.Add(new SyntaxToken(start, i - start, kind));
                atLineStart = false;
                continue;
            }

            if (c is '{' or '}' or '[' or ']' or ',' or ':' or '|')
            {
                tokens.Add(new SyntaxToken(i, 1, SyntaxTokenKind.Punctuation));
                i++;
                atLineStart = false;
                continue;
            }

            // Unrecognised — emit Default (zero-length nudge so we don't loop forever),
            // i.e. just advance without tokenizing. Consumers paint the cell with the
            // theme's default fg.
            i++;
            atLineStart = false;
        }

        return tokens;
    }

    /// <summary>
    /// Peek past whitespace from <paramref name="from"/> to check whether the next
    /// non-space char is <c>|</c> — the property-table separator obj.dump emits.
    /// </summary>
    private static bool LooksLikePropertyTableKey(string source, int from)
    {
        for (var i = from; i < source.Length; i++)
        {
            if (source[i] == ' ' || source[i] == '\t') continue;
            return source[i] == '|';
        }
        return false;
    }

    private static bool IsIdentStart(char c)
        => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';

    private static bool IsIdentPart(char c)
        => IsIdentStart(c) || (c >= '0' && c <= '9');
}
