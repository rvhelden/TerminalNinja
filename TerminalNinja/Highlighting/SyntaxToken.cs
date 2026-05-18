namespace TerminalNinja.Highlighting;

/// <summary>
/// A classified range in a source string. Ranges are byte offsets into the original source
/// (UTF-16 char indices in C#), half-open: characters in <c>[Start, Start + Length)</c>
/// belong to <see cref="Kind"/>.
/// </summary>
/// <param name="Start">0-based UTF-16 char offset.</param>
/// <param name="Length">Number of characters covered.</param>
/// <param name="Kind">The classification.</param>
/// <remarks>
/// Tokens emitted by an <see cref="ISyntaxHighlighter"/> should be non-overlapping and
/// ordered by <see cref="Start"/>. Consumers can rely on that to do a linear merge against
/// the source: walk both streams together, advancing offsets as they go.
/// </remarks>
public readonly record struct SyntaxToken(int Start, int Length, SyntaxTokenKind Kind);
