using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// One row in a <see cref="CompletionPanel"/>. Carries everything the panel
/// needs to render without depending on a specific language service:
/// <list type="bullet">
/// <item><description><see cref="Label"/> — the identifier text.</description></item>
/// <item><description><see cref="Glyph"/> — a single visible character drawn in front of the label
/// to indicate item kind (e.g. <c>ƒ</c> for function, <c>α</c> for variable).</description></item>
/// <item><description><see cref="GlyphColor"/> — the colour the glyph renders in.</description></item>
/// <item><description><see cref="Detail"/> — short inline annotation shown in the details pane heading
/// (a function signature, a value's shape, etc.).</description></item>
/// <item><description><see cref="Documentation"/> — longer prose rendered in the details pane body
/// (may contain newlines).</description></item>
/// </list>
/// Callers (e.g. a language-server-backed REPL) map their own completion items
/// to <see cref="CompletionEntry"/> values before passing them in.
/// </summary>
public sealed record CompletionEntry(
    string Label,
    string Glyph,
    Color GlyphColor,
    string? Detail,
    string? Documentation);
