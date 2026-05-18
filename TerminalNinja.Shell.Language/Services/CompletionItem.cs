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
/// A single completion suggestion. <see cref="Label"/> is what the user sees in the
/// list; <see cref="InsertText"/> is what gets dropped into the document on accept
/// (defaults to <see cref="Label"/> when null); <see cref="Detail"/> is a short
/// inline annotation (e.g. a function signature).
/// </summary>
public sealed record CompletionItem(
    string Label,
    CompletionKind Kind,
    string? Detail,
    string? InsertText);
