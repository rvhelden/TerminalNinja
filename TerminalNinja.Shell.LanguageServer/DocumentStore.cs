namespace TerminalNinja.Shell.LanguageServer;

/// <summary>
/// In-memory map of <c>textDocument.uri</c> → current source text. The LSP server
/// maintains one of these for the lifetime of a session. Versions sent by the
/// client are accepted but not retained — overwriting on every didChange matches
/// the <c>textDocumentSync = full</c> capability we advertise.
/// </summary>
public sealed class DocumentStore
{
    private readonly Dictionary<string, string> _docs = new(StringComparer.Ordinal);

    /// <summary>The set of currently-open document URIs.</summary>
    public IReadOnlyCollection<string> OpenUris => _docs.Keys;

    /// <summary>Open <paramref name="uri"/> with the given <paramref name="text"/>. Replaces any previous content.</summary>
    public void Open(string uri, string text)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(text);
        _docs[uri] = text;
    }

    /// <summary>Replace the text for an open URI. Throws if the URI is not open.</summary>
    public void Update(string uri, string text)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(text);
        if (!_docs.ContainsKey(uri))
            throw new InvalidOperationException($"didChange for unopened URI '{uri}'");
        _docs[uri] = text;
    }

    /// <summary>Remove an open URI. No-op if it wasn't open.</summary>
    public void Close(string uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        _docs.Remove(uri);
    }

    /// <summary>Read the current text for <paramref name="uri"/>, or <c>null</c> when it's not open.</summary>
    public string? GetText(string uri) => _docs.TryGetValue(uri, out var text) ? text : null;
}
