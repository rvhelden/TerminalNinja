using System.Collections.Immutable;
using System.Text;

namespace TerminalNinja.Shell.Repl;

/// <summary>
/// Shell-style word splitter used by <see cref="AliasInterceptor"/> to chop an alias
/// line's arguments. Whitespace separates tokens; double-quoted strings keep spaces
/// (and any other characters) together as a single token. Inside quotes the only
/// escapes recognised are <c>\"</c> (literal quote) and <c>\\</c> (literal backslash);
/// every other character is taken verbatim — including newlines and control bytes.
/// </summary>
/// <remarks>
/// The tokenizer is intentionally minimal — it is not a shell lexer. It exists to
/// support "one quoted argument with embedded spaces" while leaving the canonical
/// expression syntax (parens, pipes, lambdas) to the parser via the interceptor's
/// bail-out checks.
/// </remarks>
public static class ShellArgTokenizer
{
    /// <summary>
    /// One emitted token. <see cref="WasQuoted"/> records whether the source wrote it
    /// inside <c>"…"</c> — callers (notably <see cref="AliasInterceptor"/>) use that
    /// flag to permit punctuation like <c>|</c> or <c>;</c> inside quotes while still
    /// treating the same character in an unquoted token as an expression-mode signal
    /// that aborts shell-style interception.
    /// </summary>
    /// <param name="Value">The decoded token text — escapes already resolved.</param>
    /// <param name="WasQuoted">True if the token came from a double-quoted source span.</param>
    public readonly record struct Token(string Value, bool WasQuoted);

    /// <summary>
    /// Split <paramref name="source"/> into tokens. Returns <c>false</c> when a quoted
    /// string was opened but never closed — the caller should treat that as
    /// "not a valid shell line" and fall through to the expression parser instead
    /// of guessing where the quote should have ended.
    /// </summary>
    public static bool TryTokenize(string source, out ImmutableArray<Token> tokens)
    {
        ArgumentNullException.ThrowIfNull(source);
        var b = ImmutableArray.CreateBuilder<Token>();
        var buf = new StringBuilder();
        int i = 0;
        while (i < source.Length)
        {
            char c = source[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c == '"')
            {
                i++;
                bool closed = false;
                while (i < source.Length)
                {
                    char qc = source[i];
                    if (qc == '\\' && i + 1 < source.Length && (source[i + 1] == '"' || source[i + 1] == '\\'))
                    {
                        buf.Append(source[i + 1]);
                        i += 2;
                        continue;
                    }
                    if (qc == '"') { closed = true; i++; break; }
                    buf.Append(qc);
                    i++;
                }
                if (!closed) { tokens = default; return false; }
                b.Add(new Token(buf.ToString(), WasQuoted: true));
                buf.Clear();
                continue;
            }
            // Bare token: read until whitespace or quote.
            while (i < source.Length && !char.IsWhiteSpace(source[i]) && source[i] != '"')
            {
                buf.Append(source[i]);
                i++;
            }
            b.Add(new Token(buf.ToString(), WasQuoted: false));
            buf.Clear();
        }
        tokens = b.ToImmutable();
        return true;
    }
}
