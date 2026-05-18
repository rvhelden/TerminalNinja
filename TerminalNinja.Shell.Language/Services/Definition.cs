namespace TerminalNinja.Shell.Language.Services;

/// <summary>
/// Source location of a symbol's declaration, returned by
/// <see cref="LanguageService.GetDefinition"/>. The LSP layer wraps this in
/// an LSP <c>Location</c> using the requesting document's URI.
/// </summary>
/// <param name="NameRange">The range of the declared identifier itself
/// (suitable for "selection range" in the editor — where the cursor lands).</param>
/// <param name="FullRange">The range of the whole declaring statement
/// (the entire <c>let NAME = VALUE</c>), useful for previews.</param>
public sealed record Definition(Range NameRange, Range FullRange);
