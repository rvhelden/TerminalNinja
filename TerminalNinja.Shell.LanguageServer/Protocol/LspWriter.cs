using System.Buffers;
using System.Text.Json;
using TerminalNinja.Shell.Language.Services;

namespace TerminalNinja.Shell.LanguageServer.Protocol;

/// <summary>
/// LSP-aware writer over <see cref="JsonRpcWriter"/>. Builds outgoing JSON-RPC
/// messages with <see cref="Utf8JsonWriter"/> directly — no reflection-based
/// serialisation, so the whole stack stays AOT-clean.
/// </summary>
public sealed class LspWriter
{
    private readonly JsonRpcWriter _rpc;

    /// <summary>Wrap <paramref name="rpc"/> with LSP message-building helpers.</summary>
    public LspWriter(JsonRpcWriter rpc)
    {
        ArgumentNullException.ThrowIfNull(rpc);
        _rpc = rpc;
    }

    /// <summary>Write a response keyed by <paramref name="id"/>; <paramref name="writeResult"/> emits the contents of the <c>"result"</c> field.</summary>
    public void WriteResponse(JsonElement id, Action<Utf8JsonWriter> writeResult)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteString("jsonrpc", "2.0");
            w.WritePropertyName("id");
            id.WriteTo(w);
            w.WritePropertyName("result");
            writeResult(w);
            w.WriteEndObject();
        }
        _rpc.WriteMessage(buffer);
    }

    /// <summary>Write a response with a literal <c>null</c> result.</summary>
    public void WriteNullResponse(JsonElement id)
    {
        WriteResponse(id, w => w.WriteNullValue());
    }

    /// <summary>Write an error response keyed by <paramref name="id"/>.</summary>
    public void WriteError(JsonElement id, int code, string message)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteString("jsonrpc", "2.0");
            w.WritePropertyName("id");
            id.WriteTo(w);
            w.WriteStartObject("error");
            w.WriteNumber("code", code);
            w.WriteString("message", message);
            w.WriteEndObject();
            w.WriteEndObject();
        }
        _rpc.WriteMessage(buffer);
    }

    /// <summary>Write a server-to-client notification with the given <paramref name="method"/> name; <paramref name="writeParams"/> emits the contents of <c>"params"</c>.</summary>
    public void WriteNotification(string method, Action<Utf8JsonWriter> writeParams)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteString("jsonrpc", "2.0");
            w.WriteString("method", method);
            w.WritePropertyName("params");
            writeParams(w);
            w.WriteEndObject();
        }
        _rpc.WriteMessage(buffer);
    }

    /// <summary>Convenience: emit a <c>textDocument/publishDiagnostics</c> notification for one URI.</summary>
    public void PublishDiagnostics(string uri, IReadOnlyList<Diagnostic> diagnostics)
    {
        WriteNotification("textDocument/publishDiagnostics", w =>
        {
            w.WriteStartObject();
            w.WriteString("uri", uri);
            w.WriteStartArray("diagnostics");
            foreach (var d in diagnostics) WriteDiagnostic(w, d);
            w.WriteEndArray();
            w.WriteEndObject();
        });
    }

    private static void WriteDiagnostic(Utf8JsonWriter w, Diagnostic d)
    {
        w.WriteStartObject();
        w.WriteStartObject("range");
        WritePosition(w, "start", d.Range.Start);
        WritePosition(w, "end", d.Range.End);
        w.WriteEndObject();
        w.WriteNumber("severity", (int)d.Severity);
        w.WriteString("message", d.Message);
        w.WriteEndObject();
    }

    private static void WritePosition(Utf8JsonWriter w, string propertyName, Position p)
    {
        w.WriteStartObject(propertyName);
        w.WriteNumber("line", p.Line);
        w.WriteNumber("character", p.Character);
        w.WriteEndObject();
    }

    /// <summary>Write a <c>textDocument/documentSymbol</c> response for the given <paramref name="id"/>.</summary>
    public void WriteDocumentSymbols(JsonElement id, IReadOnlyList<DocumentSymbol> symbols)
    {
        WriteResponse(id, w =>
        {
            w.WriteStartArray();
            foreach (var s in symbols) WriteDocumentSymbol(w, s);
            w.WriteEndArray();
        });
    }

    /// <summary>Write a <c>textDocument/completion</c> response for the given <paramref name="id"/>.</summary>
    public void WriteCompletions(JsonElement id, IReadOnlyList<CompletionItem> items)
    {
        WriteResponse(id, w =>
        {
            // Use the CompletionList shape so we can mark the list as non-incomplete.
            w.WriteStartObject();
            w.WriteBoolean("isIncomplete", false);
            w.WriteStartArray("items");
            foreach (var item in items)
            {
                w.WriteStartObject();
                w.WriteString("label", item.Label);
                w.WriteNumber("kind", (int)item.Kind);
                if (item.Detail is not null) w.WriteString("detail", item.Detail);
                if (item.InsertText is not null) w.WriteString("insertText", item.InsertText);
                if (item.Documentation is not null) w.WriteString("documentation", item.Documentation);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        });
    }

    /// <summary>
    /// Write a <c>textDocument/signatureHelp</c> response. Sends one signature (the
    /// one we resolved) with the active parameter index set. LSP allows multiple
    /// signatures for overloads — NinjaShell builtins aren't overloaded, so we
    /// always emit exactly one.
    /// </summary>
    public void WriteSignatureHelp(JsonElement id, SignatureHelp? help)
    {
        WriteResponse(id, w =>
        {
            if (help is null) { w.WriteNullValue(); return; }
            w.WriteStartObject();
            w.WriteStartArray("signatures");
            w.WriteStartObject();
            w.WriteString("label", help.Label);
            if (help.Documentation is not null) w.WriteString("documentation", help.Documentation);
            w.WriteStartArray("parameters");
            foreach (var p in help.Parameters)
            {
                w.WriteStartObject();
                // LSP allows label to be a string or [int, int] range — we send the range
                // so the client can highlight inside the signature's main label.
                w.WriteStartArray("label");
                w.WriteNumberValue(p.LabelStart);
                w.WriteNumberValue(p.LabelStart + p.LabelLength);
                w.WriteEndArray();
                if (p.Documentation is not null) w.WriteString("documentation", p.Documentation);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
            w.WriteEndArray();
            w.WriteNumber("activeSignature", 0);
            w.WriteNumber("activeParameter", help.ActiveParameter);
            w.WriteEndObject();
        });
    }

    /// <summary>
    /// Write a <c>textDocument/definition</c> response. Emits an LSP
    /// <c>Location</c> (a single <c>{ uri, range }</c> object) when a
    /// definition is found, or a literal JSON <c>null</c> when none.
    /// </summary>
    public void WriteDefinition(JsonElement id, string uri, Definition? definition)
    {
        WriteResponse(id, w =>
        {
            if (definition is null) { w.WriteNullValue(); return; }
            w.WriteStartObject();
            w.WriteString("uri", uri);
            w.WriteStartObject("range");
            WritePosition(w, "start", definition.NameRange.Start);
            WritePosition(w, "end", definition.NameRange.End);
            w.WriteEndObject();
            w.WriteEndObject();
        });
    }

    /// <summary>
    /// Write a <c>textDocument/hover</c> response. A <c>null</c> hover (no symbol
    /// under the cursor) is sent as a literal JSON <c>null</c>, which is what LSP
    /// clients expect when there's nothing to show.
    /// </summary>
    public void WriteHover(JsonElement id, Hover? hover)
    {
        WriteResponse(id, w =>
        {
            if (hover is null) { w.WriteNullValue(); return; }
            w.WriteStartObject();
            w.WriteStartObject("contents");
            w.WriteString("kind", "markdown");
            w.WriteString("value", hover.Contents);
            w.WriteEndObject();
            w.WriteStartObject("range");
            WritePosition(w, "start", hover.Range.Start);
            WritePosition(w, "end", hover.Range.End);
            w.WriteEndObject();
            w.WriteEndObject();
        });
    }

    private static void WriteDocumentSymbol(Utf8JsonWriter w, DocumentSymbol s)
    {
        w.WriteStartObject();
        w.WriteString("name", s.Name);
        if (s.Detail is not null) w.WriteString("detail", s.Detail);
        w.WriteNumber("kind", (int)s.Kind);
        w.WriteStartObject("range");
        WritePosition(w, "start", s.Range.Start);
        WritePosition(w, "end", s.Range.End);
        w.WriteEndObject();
        w.WriteStartObject("selectionRange");
        WritePosition(w, "start", s.SelectionRange.Start);
        WritePosition(w, "end", s.SelectionRange.End);
        w.WriteEndObject();
        if (s.Children.Count > 0)
        {
            w.WriteStartArray("children");
            foreach (var child in s.Children) WriteDocumentSymbol(w, child);
            w.WriteEndArray();
        }
        w.WriteEndObject();
    }
}
