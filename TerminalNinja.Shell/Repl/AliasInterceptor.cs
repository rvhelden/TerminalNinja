using System.Collections.Immutable;
using TerminalNinja.Shell.Config;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Repl;

/// <summary>
/// Single-line gate that decides whether a REPL input line should be invoked as a
/// shell-mode alias call (e.g. <c>cd foo</c>) instead of parsed as an expression
/// (<c>fs.cd("foo")</c>). The interceptor never mutates state — it only inspects the
/// line and resolves the callable from a <see cref="NinjaConfig"/> snapshot, leaving
/// invocation to the caller.
/// </summary>
/// <remarks>
/// A line is intercepted only when all of the following hold:
/// (1) the first non-whitespace run is an identifier; (2) the identifier is a
/// registered alias; (3) the character immediately following the identifier is
/// whitespace or end-of-line (never <c>(</c>, <c>.</c>, <c>=</c>, etc., which would
/// be expression-mode); (4) the remainder tokenises successfully via
/// <see cref="ShellArgTokenizer"/>; and (5) no unquoted token contains language
/// punctuation. If any condition fails, <see cref="TryIntercept"/> returns
/// <c>false</c> and the caller should hand the line to the parser unchanged.
/// </remarks>
public static class AliasInterceptor
{
    /// <summary>
    /// The resolved invocation produced by <see cref="TryIntercept"/>: the alias's
    /// callable, the alias name, and the parsed args. Every entry in
    /// <see cref="Args"/> is an <see cref="NString"/> — shell-mode interception
    /// never coerces tokens to numbers or other types; pass through to the canonical
    /// expression syntax if you need typed arguments.
    /// </summary>
    public readonly record struct AliasInvocation(string Name, NValue Func, ImmutableArray<NValue> Args);

    private static readonly char[] ExpressionContextChars = ['(', '.', '=', '[', '{', ':', ','];

    /// <summary>
    /// Returns <c>true</c> if <paramref name="line"/> should be executed as a shell-mode
    /// alias call against <paramref name="config"/>; <paramref name="invocation"/>
    /// then carries the callable and its arguments (each argument as an <see cref="NString"/>).
    /// </summary>
    public static bool TryIntercept(string line, NinjaConfig config, out AliasInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(config);
        invocation = default;

        int i = 0;
        while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
        if (i >= line.Length) return false;

        int idStart = i;
        if (!IsIdentifierStart(line[i])) return false;
        while (i < line.Length && IsIdentifierChar(line[i])) i++;
        var name = line.Substring(idStart, i - idStart);

        if (!config.TryGetAlias(name, out var callable)) return false;

        // Next non-whitespace char must be either end-of-line or a "shell"-style continuation,
        // not an expression-context punctuation that would change the meaning of the identifier.
        if (i < line.Length && !char.IsWhiteSpace(line[i]))
        {
            // The bare identifier butts up against a non-space character — that's expression context.
            return false;
        }
        // Peek past whitespace to catch "cd  = 1" style assignments.
        int peek = i;
        while (peek < line.Length && char.IsWhiteSpace(line[peek])) peek++;
        if (peek < line.Length && Array.IndexOf(ExpressionContextChars, line[peek]) >= 0) return false;

        var rest = line[i..];
        if (!ShellArgTokenizer.TryTokenize(rest, out var tokens)) return false;

        foreach (var t in tokens)
        {
            if (!t.WasQuoted && ContainsExpressionPunctuation(t.Value)) return false;
        }

        var args = ImmutableArray.CreateBuilder<NValue>(tokens.Length);
        foreach (var t in tokens) args.Add(new NString(t.Value));
        invocation = new AliasInvocation(name, callable, args.ToImmutable());
        return true;
    }

    private static bool IsIdentifierStart(char c)
        => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';

    private static bool IsIdentifierChar(char c)
        => IsIdentifierStart(c) || (c >= '0' && c <= '9');

    private static bool ContainsExpressionPunctuation(string token)
    {
        foreach (char c in token)
        {
            switch (c)
            {
                case '|':
                case '&':
                case '(':
                case ')':
                case '[':
                case ']':
                case '{':
                case '}':
                case ',':
                case '`':
                case ';':
                    return true;
            }
        }
        // Two-char operator: `=>`.
        return token.Contains("=>", StringComparison.Ordinal);
    }
}
