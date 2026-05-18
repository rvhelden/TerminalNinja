using System.Text.Json;
using TerminalNinja.Shell.Language.Services;
using TerminalNinja.Shell.LanguageServer.Protocol;

namespace TerminalNinja.Shell.LanguageServer;

/// <summary>
/// Synchronous, single-threaded LSP server. <see cref="Run"/> drives a
/// read-dispatch-write loop until the input stream closes or the client
/// requests shutdown + exit. The actual language analysis lives in
/// <see cref="LanguageService"/>; this class is the LSP transport
/// translation layer.
/// </summary>
public sealed class LspServer
{
    private readonly DocumentStore _docs = new();
    private bool _exitRequested;

    /// <summary>Document store, exposed for tests.</summary>
    internal DocumentStore Documents => _docs;

    /// <summary>
    /// Drive the read-dispatch-write loop. Returns when the input stream is
    /// exhausted (e.g. the client closed stdio) or when an <c>exit</c>
    /// notification has been observed.
    /// </summary>
    public void Run(Stream input, Stream output)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        var reader = new JsonRpcReader(input);
        var writer = new LspWriter(new JsonRpcWriter(output));

        while (!_exitRequested)
        {
            JsonDocument? message;
            try
            {
                message = reader.ReadMessage();
            }
            catch (EndOfStreamException)
            {
                break;
            }
            if (message is null) break;
            using (message) Dispatch(message.RootElement, writer);
        }
    }

    /// <summary>Dispatch a single inbound message. Internal so tests can drive without the loop.</summary>
    internal void Dispatch(JsonElement message, LspWriter writer)
    {
        if (!message.TryGetProperty("method", out var methodEl) || methodEl.ValueKind != JsonValueKind.String)
        {
            // Responses to server-initiated requests (none yet) — ignore.
            return;
        }
        var method = methodEl.GetString()!;
        var hasId = message.TryGetProperty("id", out var idEl);

        switch (method)
        {
            case "initialize":
                if (hasId) HandleInitialize(idEl, writer);
                break;
            case "initialized":
                // No response required.
                break;
            case "shutdown":
                // Per LSP, the client sends shutdown then exit. We respond with
                // null and wait for the exit notification before stopping.
                if (hasId) writer.WriteNullResponse(idEl);
                break;
            case "exit":
                _exitRequested = true;
                break;
            case "textDocument/didOpen":
                HandleDidOpen(message, writer);
                break;
            case "textDocument/didChange":
                HandleDidChange(message, writer);
                break;
            case "textDocument/didClose":
                HandleDidClose(message, writer);
                break;
            case "textDocument/documentSymbol":
                if (hasId) HandleDocumentSymbol(idEl, message, writer);
                break;
            case "textDocument/completion":
                if (hasId) HandleCompletion(idEl, message, writer);
                break;
            case "textDocument/signatureHelp":
                if (hasId) HandleSignatureHelp(idEl, message, writer);
                break;
            default:
                // Unknown request → MethodNotFound (-32601). Unknown notification → ignore.
                if (hasId) writer.WriteError(idEl, -32601, $"method not supported: {method}");
                break;
        }
    }

    private static void HandleInitialize(JsonElement id, LspWriter writer)
    {
        writer.WriteResponse(id, w =>
        {
            w.WriteStartObject();
            w.WriteStartObject("capabilities");
            // Full document sync — client sends the entire text on every change.
            w.WriteNumber("textDocumentSync", 1);
            // Outline / breadcrumb support.
            w.WriteBoolean("documentSymbolProvider", true);
            // Completion — trigger on '.' for member access, and on every typed character via the editor.
            w.WriteStartObject("completionProvider");
            w.WriteStartArray("triggerCharacters");
            w.WriteStringValue(".");
            w.WriteEndArray();
            w.WriteEndObject();
            // Signature help — triggered when the user opens a paren or moves to the next argument.
            w.WriteStartObject("signatureHelpProvider");
            w.WriteStartArray("triggerCharacters");
            w.WriteStringValue("(");
            w.WriteStringValue(",");
            w.WriteEndArray();
            w.WriteEndObject();
            w.WriteEndObject();
            w.WriteStartObject("serverInfo");
            w.WriteString("name", "ninja-lsp");
            w.WriteString("version", "0.0.1");
            w.WriteEndObject();
            w.WriteEndObject();
        });
    }

    private void HandleDidOpen(JsonElement message, LspWriter writer)
    {
        if (!TryReadTextDocument(message, out var uri, out var text)) return;
        _docs.Open(uri, text);
        Publish(uri, writer);
    }

    private void HandleDidChange(JsonElement message, LspWriter writer)
    {
        if (!message.TryGetProperty("params", out var p)) return;
        if (!p.TryGetProperty("textDocument", out var td)) return;
        if (!td.TryGetProperty("uri", out var uriEl)) return;
        var uri = uriEl.GetString();
        if (uri is null) return;

        // With textDocumentSync=full the latest entry in contentChanges holds the
        // entire new document; for safety we take the *last* change, which is the
        // most recent full snapshot.
        if (!p.TryGetProperty("contentChanges", out var changes) || changes.ValueKind != JsonValueKind.Array)
            return;

        string? text = null;
        foreach (var change in changes.EnumerateArray())
        {
            if (change.TryGetProperty("text", out var textEl))
                text = textEl.GetString();
        }
        if (text is null) return;
        _docs.Update(uri, text);
        Publish(uri, writer);
    }

    private void HandleCompletion(JsonElement id, JsonElement message, LspWriter writer)
    {
        if (!message.TryGetProperty("params", out var p)
            || !p.TryGetProperty("textDocument", out var td)
            || !td.TryGetProperty("uri", out var uriEl)
            || !p.TryGetProperty("position", out var posEl))
        {
            writer.WriteCompletions(id, Array.Empty<CompletionItem>());
            return;
        }
        var uri = uriEl.GetString();
        var text = uri is null ? null : _docs.GetText(uri);
        if (text is null)
        {
            writer.WriteCompletions(id, Array.Empty<CompletionItem>());
            return;
        }
        if (!posEl.TryGetProperty("line", out var lineEl) || !posEl.TryGetProperty("character", out var charEl))
        {
            writer.WriteCompletions(id, Array.Empty<CompletionItem>());
            return;
        }
        var cursor = new Position(lineEl.GetInt32(), charEl.GetInt32());
        var items = LanguageService.GetCompletions(text, cursor);
        writer.WriteCompletions(id, items);
    }

    private void HandleSignatureHelp(JsonElement id, JsonElement message, LspWriter writer)
    {
        if (!message.TryGetProperty("params", out var p)
            || !p.TryGetProperty("textDocument", out var td)
            || !td.TryGetProperty("uri", out var uriEl)
            || !p.TryGetProperty("position", out var posEl))
        {
            writer.WriteSignatureHelp(id, null);
            return;
        }
        var uri = uriEl.GetString();
        var text = uri is null ? null : _docs.GetText(uri);
        if (text is null)
        {
            writer.WriteSignatureHelp(id, null);
            return;
        }
        if (!posEl.TryGetProperty("line", out var lineEl) || !posEl.TryGetProperty("character", out var charEl))
        {
            writer.WriteSignatureHelp(id, null);
            return;
        }
        var cursor = new Position(lineEl.GetInt32(), charEl.GetInt32());
        writer.WriteSignatureHelp(id, LanguageService.GetSignatureHelp(text, cursor));
    }

    private void HandleDocumentSymbol(JsonElement id, JsonElement message, LspWriter writer)
    {
        if (!message.TryGetProperty("params", out var p)
            || !p.TryGetProperty("textDocument", out var td)
            || !td.TryGetProperty("uri", out var uriEl))
        {
            writer.WriteResponse(id, w => w.WriteStartArray()); // empty array; closed below
            return;
        }
        var uri = uriEl.GetString();
        var text = uri is null ? null : _docs.GetText(uri);
        var symbols = text is null
            ? Array.Empty<DocumentSymbol>()
            : LanguageService.GetDocumentSymbols(text);
        writer.WriteDocumentSymbols(id, symbols);
    }

    private void HandleDidClose(JsonElement message, LspWriter writer)
    {
        if (!message.TryGetProperty("params", out var p)) return;
        if (!p.TryGetProperty("textDocument", out var td)) return;
        if (!td.TryGetProperty("uri", out var uriEl)) return;
        var uri = uriEl.GetString();
        if (uri is null) return;
        _docs.Close(uri);
        // Clear diagnostics by publishing an empty list — protocol convention.
        writer.PublishDiagnostics(uri, Array.Empty<Diagnostic>());
    }

    private void Publish(string uri, LspWriter writer)
    {
        var text = _docs.GetText(uri);
        if (text is null) return;
        var diagnostics = LanguageService.GetDiagnostics(text);
        writer.PublishDiagnostics(uri, diagnostics);
    }

    private static bool TryReadTextDocument(JsonElement message, out string uri, out string text)
    {
        uri = "";
        text = "";
        if (!message.TryGetProperty("params", out var p)) return false;
        if (!p.TryGetProperty("textDocument", out var td)) return false;
        if (!td.TryGetProperty("uri", out var uriEl)) return false;
        if (!td.TryGetProperty("text", out var textEl)) return false;
        var u = uriEl.GetString();
        var t = textEl.GetString();
        if (u is null || t is null) return false;
        uri = u;
        text = t;
        return true;
    }
}
