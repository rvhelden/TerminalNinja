namespace TerminalNinja.Shell.Language.Services;

/// <summary>LSP completion item kinds. Integer values match the protocol so they serialise directly.</summary>
public enum CompletionKind
{
    Text = 1,
    Method = 2,
    Function = 3,
    Constructor = 4,
    Field = 5,
    Variable = 6,
    Class = 7,
    Interface = 8,
    Module = 9,
    Property = 10,
    Unit = 11,
    Value = 12,
    Enum = 13,
    Keyword = 14,
    Snippet = 15,
}

/// <summary>
/// A single completion suggestion.
/// <list type="bullet">
/// <item><description><see cref="Label"/> — what the user sees in the list.</description></item>
/// <item><description><see cref="Kind"/> — drives the icon / colour in two-pane UIs.</description></item>
/// <item><description><see cref="Detail"/> — short inline annotation (e.g. a function signature).</description></item>
/// <item><description><see cref="Documentation"/> — longer human-readable explanation rendered in the
/// details pane. For scope items this carries <c>shape:</c> / <c>data:</c> lines from
/// <see cref="ValueFormatter"/> so user-defined bindings preview their value.</description></item>
/// <item><description><see cref="InsertText"/> — what gets dropped into the document on accept
/// (defaults to <see cref="Label"/> when null).</description></item>
/// </list>
/// </summary>
public sealed record CompletionItem(
    string Label,
    CompletionKind Kind,
    string? Detail,
    string? InsertText,
    string? Documentation = null);
