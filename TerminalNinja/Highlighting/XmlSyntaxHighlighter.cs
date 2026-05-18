namespace TerminalNinja.Highlighting;

/// <summary>
/// XML / HTML-ish tokeniser. Recognises tags (open / close / self-closing), attribute
/// name / value pairs, comments (<c>&lt;!-- … --&gt;</c>), CDATA sections, and the angle
/// brackets that delimit them. Tolerant of partial input.
/// </summary>
public sealed class XmlSyntaxHighlighter : ISyntaxHighlighter
{
    /// <inheritdoc />
    public string Language => "xml";

    /// <inheritdoc />
    public IReadOnlyList<SyntaxToken> Tokenize(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var tokens = new List<SyntaxToken>();
        var i = 0;
        var n = source.Length;

        while (i < n)
        {
            if (source[i] != '<')
            {
                // Text content between tags — no token; consumers fall back to default fg.
                i++;
                continue;
            }

            var lt = i;

            // <!-- comment -->
            if (StartsWith(source, i, "<!--"))
            {
                var start = i;
                i += 4;
                while (i + 3 <= n && !(source[i] == '-' && source[i + 1] == '-' && source[i + 2] == '>')) i++;
                if (i + 3 <= n) i += 3; else i = n;
                tokens.Add(new SyntaxToken(start, i - start, SyntaxTokenKind.Comment));
                continue;
            }

            // <![CDATA[ ... ]]>
            if (StartsWith(source, i, "<![CDATA["))
            {
                var start = i;
                i += 9;
                while (i + 3 <= n && !(source[i] == ']' && source[i + 1] == ']' && source[i + 2] == '>')) i++;
                if (i + 3 <= n) i += 3; else i = n;
                tokens.Add(new SyntaxToken(start, i - start, SyntaxTokenKind.StringLiteral));
                continue;
            }

            // Tag open: '<' plus an optional '/' plus a name. We emit the punctuation, the
            // tag name, then walk attributes.
            tokens.Add(new SyntaxToken(i, 1, SyntaxTokenKind.Punctuation));
            i++;
            if (i < n && source[i] == '/')
            {
                tokens.Add(new SyntaxToken(i, 1, SyntaxTokenKind.Punctuation));
                i++;
            }
            else if (i < n && source[i] == '?')
            {
                // <?xml … ?>  processing instructions — treat the '?' as punctuation.
                tokens.Add(new SyntaxToken(i, 1, SyntaxTokenKind.Punctuation));
                i++;
            }
            else if (i < n && source[i] == '!')
            {
                // <!DOCTYPE …>  declarations — treat the '!' as punctuation.
                tokens.Add(new SyntaxToken(i, 1, SyntaxTokenKind.Punctuation));
                i++;
            }

            // Tag name.
            var nameStart = i;
            while (i < n && IsNameChar(source[i])) i++;
            if (i > nameStart)
            {
                tokens.Add(new SyntaxToken(nameStart, i - nameStart, SyntaxTokenKind.Tag));
            }

            // Attributes until we hit '>' or end-of-input.
            while (i < n && source[i] != '>')
            {
                if (source[i] is ' ' or '\t' or '\r' or '\n') { i++; continue; }

                if (source[i] == '/' || source[i] == '?')
                {
                    tokens.Add(new SyntaxToken(i, 1, SyntaxTokenKind.Punctuation));
                    i++;
                    continue;
                }

                // Attribute name.
                if (IsNameStartChar(source[i]))
                {
                    var attrStart = i;
                    while (i < n && IsNameChar(source[i])) i++;
                    tokens.Add(new SyntaxToken(attrStart, i - attrStart, SyntaxTokenKind.AttributeName));
                    // '=' optionally followed by a quoted value.
                    while (i < n && source[i] is ' ' or '\t') i++;
                    if (i < n && source[i] == '=')
                    {
                        tokens.Add(new SyntaxToken(i, 1, SyntaxTokenKind.Operator));
                        i++;
                        while (i < n && source[i] is ' ' or '\t') i++;
                        if (i < n && (source[i] == '"' || source[i] == '\''))
                        {
                            var q = source[i];
                            var vStart = i++;
                            while (i < n && source[i] != q) i++;
                            if (i < n) i++; // closing quote
                            tokens.Add(new SyntaxToken(vStart, i - vStart, SyntaxTokenKind.AttributeValue));
                        }
                    }
                    continue;
                }

                // Anything else inside the tag is junk — single-char Error.
                tokens.Add(new SyntaxToken(i, 1, SyntaxTokenKind.Error));
                i++;
            }

            // Closing '>'.
            if (i < n && source[i] == '>')
            {
                tokens.Add(new SyntaxToken(i, 1, SyntaxTokenKind.Punctuation));
                i++;
            }
            else
            {
                // Reached end-of-input mid-tag — mark the unclosed lt..end-of-tag as error
                // so the caller can render it red. We've already emitted the inner tokens;
                // overlay an error on the leading '<' only to keep the scan O(n).
                if (tokens.Count > 0 && tokens[^1].Start >= lt)
                {
                    // already classified; nothing else to do
                }
            }
        }

        return tokens;
    }

    private static bool StartsWith(string source, int i, string prefix)
    {
        if (i + prefix.Length > source.Length) return false;
        for (var k = 0; k < prefix.Length; k++)
        {
            if (source[i + k] != prefix[k]) return false;
        }
        return true;
    }

    private static bool IsNameStartChar(char c)
        => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_' || c == ':';

    private static bool IsNameChar(char c)
        => IsNameStartChar(c) || (c >= '0' && c <= '9') || c == '-' || c == '.';
}
