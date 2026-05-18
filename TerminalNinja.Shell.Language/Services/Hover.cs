namespace TerminalNinja.Shell.Language.Services;

/// <summary>
/// Hover information for an identifier at a cursor position — typically the
/// builtin's signature plus a short description. Mirrors LSP's <c>Hover</c>
/// response shape so the server can serialise it directly.
/// </summary>
/// <param name="Contents">Markdown-friendly text describing the symbol.</param>
/// <param name="Range">The source range that produced the hover (the identifier
/// or <c>module.member</c> token under the cursor).</param>
public sealed record Hover(string Contents, Range Range);
