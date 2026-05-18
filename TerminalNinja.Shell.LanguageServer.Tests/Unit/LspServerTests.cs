using System.Text;
using System.Text.Json;
using TerminalNinja.Shell.Language.Protocol;
using TerminalNinja.Shell.LanguageServer;
using TerminalNinja.Shell.LanguageServer.Protocol;

namespace TerminalNinja.Shell.LanguageServer.Tests.Unit;

/// <summary>
/// In-proc integration tests for the LSP server. Each test pre-loads an input
/// stream with a complete request/notification sequence, runs the server to
/// EOF, then parses the output stream to verify the responses.
/// </summary>
public class LspServerTests
{
    // ─── helpers ────────────────────────────────────────────────────────────

    private static byte[] Frame(string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n");
        var combined = new byte[header.Length + bytes.Length];
        Buffer.BlockCopy(header, 0, combined, 0, header.Length);
        Buffer.BlockCopy(bytes, 0, combined, header.Length, bytes.Length);
        return combined;
    }

    private static byte[] BuildInput(params string[] bodies)
    {
        var bytes = new List<byte>();
        foreach (var body in bodies) bytes.AddRange(Frame(body));
        return bytes.ToArray();
    }

    private static List<JsonDocument> RunServerAndCollect(byte[] input)
    {
        var inputStream = new MemoryStream(input);
        var outputStream = new MemoryStream();
        var server = new LspServer();
        server.Run(inputStream, outputStream);
        outputStream.Position = 0;
        var reader = new JsonRpcReader(outputStream);
        var results = new List<JsonDocument>();
        while (true)
        {
            var doc = reader.ReadMessage();
            if (doc is null) break;
            results.Add(doc);
        }
        return results;
    }

    private static string Initialize() =>
        "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{}}";

    private static string Initialized() =>
        "{\"jsonrpc\":\"2.0\",\"method\":\"initialized\",\"params\":{}}";

    private static string DidOpen(string uri, string text)
    {
        var escaped = JsonEncodedText.Encode(text);
        return "{\"jsonrpc\":\"2.0\",\"method\":\"textDocument/didOpen\",\"params\":{" +
               $"\"textDocument\":{{\"uri\":\"{uri}\",\"languageId\":\"ninja\",\"version\":1,\"text\":\"{escaped}\"}}" +
               "}}";
    }

    private static string DidChange(string uri, string text)
    {
        var escaped = JsonEncodedText.Encode(text);
        return "{\"jsonrpc\":\"2.0\",\"method\":\"textDocument/didChange\",\"params\":{" +
               $"\"textDocument\":{{\"uri\":\"{uri}\",\"version\":2}}," +
               $"\"contentChanges\":[{{\"text\":\"{escaped}\"}}]" +
               "}}";
    }

    private static string DidClose(string uri) =>
        "{\"jsonrpc\":\"2.0\",\"method\":\"textDocument/didClose\",\"params\":{" +
        $"\"textDocument\":{{\"uri\":\"{uri}\"}}" +
        "}}";

    // ─── initialize ─────────────────────────────────────────────────────────

    [Test]
    public async Task Initialize_RespondsWithCapabilities()
    {
        var responses = RunServerAndCollect(BuildInput(Initialize()));
        await Assert.That(responses.Count).IsEqualTo(1);
        var root = responses[0].RootElement;
        await Assert.That(root.GetProperty("id").GetInt32()).IsEqualTo(1);
        var caps = root.GetProperty("result").GetProperty("capabilities");
        await Assert.That(caps.GetProperty("textDocumentSync").GetInt32()).IsEqualTo(1);
    }

    [Test]
    public async Task Initialized_Notification_NoResponse()
    {
        var responses = RunServerAndCollect(BuildInput(Initialized()));
        await Assert.That(responses).IsEmpty();
    }

    // ─── diagnostics ────────────────────────────────────────────────────────

    [Test]
    public async Task DidOpen_CleanSource_PublishesEmptyDiagnostics()
    {
        var responses = RunServerAndCollect(BuildInput(
            Initialize(),
            DidOpen("file:///clean.ninja", "let x = 42")));
        var pub = responses.Single(r => Method(r) == "textDocument/publishDiagnostics");
        var diags = pub.RootElement.GetProperty("params").GetProperty("diagnostics");
        await Assert.That(diags.GetArrayLength()).IsEqualTo(0);
    }

    [Test]
    public async Task DidOpen_BadSource_PublishesOneDiagnostic()
    {
        var responses = RunServerAndCollect(BuildInput(
            Initialize(),
            DidOpen("file:///bad.ninja", "@illegal")));
        var pub = responses.Single(r => Method(r) == "textDocument/publishDiagnostics");
        var diags = pub.RootElement.GetProperty("params").GetProperty("diagnostics");
        await Assert.That(diags.GetArrayLength()).IsEqualTo(1);
        var first = diags[0];
        await Assert.That(first.GetProperty("severity").GetInt32()).IsEqualTo(1);
        // 0-based range — error is on line 0.
        await Assert.That(first.GetProperty("range").GetProperty("start").GetProperty("line").GetInt32())
            .IsEqualTo(0);
    }

    [Test]
    public async Task DidChange_FixesBadSource_RepublishesEmptyDiagnostics()
    {
        var responses = RunServerAndCollect(BuildInput(
            Initialize(),
            DidOpen("file:///fix.ninja", "@illegal"),
            DidChange("file:///fix.ninja", "let x = 42")));
        var publishes = responses.Where(r => Method(r) == "textDocument/publishDiagnostics").ToList();
        await Assert.That(publishes.Count).IsEqualTo(2);
        // First (after didOpen): 1 diagnostic. Second (after didChange): 0.
        var first = publishes[0].RootElement.GetProperty("params").GetProperty("diagnostics");
        var second = publishes[1].RootElement.GetProperty("params").GetProperty("diagnostics");
        await Assert.That(first.GetArrayLength()).IsEqualTo(1);
        await Assert.That(second.GetArrayLength()).IsEqualTo(0);
    }

    [Test]
    public async Task DidClose_ClearsDiagnosticsForDocument()
    {
        var responses = RunServerAndCollect(BuildInput(
            Initialize(),
            DidOpen("file:///x.ninja", "@illegal"),
            DidClose("file:///x.ninja")));
        var publishes = responses.Where(r => Method(r) == "textDocument/publishDiagnostics").ToList();
        // Last publish (the close) should carry an empty diagnostics array.
        var last = publishes[^1].RootElement.GetProperty("params").GetProperty("diagnostics");
        await Assert.That(last.GetArrayLength()).IsEqualTo(0);
    }

    // ─── lifecycle ──────────────────────────────────────────────────────────

    [Test]
    public async Task Shutdown_ReturnsNullResult()
    {
        var responses = RunServerAndCollect(BuildInput(
            Initialize(),
            "{\"jsonrpc\":\"2.0\",\"id\":99,\"method\":\"shutdown\",\"params\":null}"));
        var shutdown = responses.Single(r => r.RootElement.TryGetProperty("id", out var idEl) && idEl.GetInt32() == 99);
        await Assert.That(shutdown.RootElement.GetProperty("result").ValueKind).IsEqualTo(JsonValueKind.Null);
    }

    [Test]
    public async Task UnknownMethod_AsRequest_RepliesWithMethodNotFound()
    {
        var responses = RunServerAndCollect(BuildInput(
            "{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"completelyBogus/method\",\"params\":{}}"));
        var resp = responses.Single(r => r.RootElement.TryGetProperty("id", out var id) && id.GetInt32() == 7);
        await Assert.That(resp.RootElement.GetProperty("error").GetProperty("code").GetInt32()).IsEqualTo(-32601);
    }

    [Test]
    public async Task UnknownNotification_IsIgnoredSilently()
    {
        var responses = RunServerAndCollect(BuildInput(
            "{\"jsonrpc\":\"2.0\",\"method\":\"completelyBogus/notification\",\"params\":{}}"));
        await Assert.That(responses).IsEmpty();
    }

    // ─── documentSymbol ─────────────────────────────────────────────────────

    [Test]
    public async Task Initialize_Advertises_DocumentSymbolProvider()
    {
        var responses = RunServerAndCollect(BuildInput(Initialize()));
        var caps = responses[0].RootElement.GetProperty("result").GetProperty("capabilities");
        await Assert.That(caps.GetProperty("documentSymbolProvider").GetBoolean()).IsTrue();
    }

    [Test]
    public async Task DocumentSymbol_OnLetStatement_ReturnsVariableSymbol()
    {
        const string uri = "file:///sym.ninja";
        var responses = RunServerAndCollect(BuildInput(
            Initialize(),
            DidOpen(uri, "let answer = 42"),
            $"{{\"jsonrpc\":\"2.0\",\"id\":42,\"method\":\"textDocument/documentSymbol\",\"params\":{{" +
            $"\"textDocument\":{{\"uri\":\"{uri}\"}}" +
            "}}"));

        var sym = responses.Single(r =>
            r.RootElement.TryGetProperty("id", out var idEl) && idEl.GetInt32() == 42);
        var arr = sym.RootElement.GetProperty("result");
        await Assert.That(arr.GetArrayLength()).IsEqualTo(1);
        await Assert.That(arr[0].GetProperty("name").GetString()).IsEqualTo("answer");
        await Assert.That(arr[0].GetProperty("kind").GetInt32()).IsEqualTo((int)TerminalNinja.Shell.Language.Services.SymbolKind.Variable);
    }

    // ─── completion ─────────────────────────────────────────────────────────

    [Test]
    public async Task Initialize_Advertises_CompletionProvider_WithDotTrigger()
    {
        var responses = RunServerAndCollect(BuildInput(Initialize()));
        var caps = responses[0].RootElement.GetProperty("result").GetProperty("capabilities");
        var prov = caps.GetProperty("completionProvider");
        var trigger = prov.GetProperty("triggerCharacters");
        await Assert.That(trigger.GetArrayLength()).IsEqualTo(1);
        await Assert.That(trigger[0].GetString()).IsEqualTo(".");
    }

    [Test]
    public async Task Completion_OnMemberAccess_ReturnsModuleMembers()
    {
        const string uri = "file:///compl.ninja";
        var responses = RunServerAndCollect(BuildInput(
            Initialize(),
            DidOpen(uri, "obj."),
            $"{{\"jsonrpc\":\"2.0\",\"id\":77,\"method\":\"textDocument/completion\",\"params\":{{" +
            $"\"textDocument\":{{\"uri\":\"{uri}\"}}," +
            "\"position\":{\"line\":0,\"character\":4}" +
            "}}"));

        var compl = responses.Single(r =>
            r.RootElement.TryGetProperty("id", out var idEl) && idEl.GetInt32() == 77);
        var result = compl.RootElement.GetProperty("result");
        await Assert.That(result.GetProperty("isIncomplete").GetBoolean()).IsFalse();
        var items = result.GetProperty("items");
        var labels = new HashSet<string>();
        for (int i = 0; i < items.GetArrayLength(); i++)
            labels.Add(items[i].GetProperty("label").GetString() ?? "");
        await Assert.That(labels.Contains("type")).IsTrue();
        await Assert.That(labels.Contains("dump")).IsTrue();
        await Assert.That(labels.Contains("normalize")).IsTrue();
    }

    [Test]
    public async Task Completion_OnUnknownUri_ReturnsEmptyItems()
    {
        var responses = RunServerAndCollect(BuildInput(
            Initialize(),
            "{\"jsonrpc\":\"2.0\",\"id\":78,\"method\":\"textDocument/completion\",\"params\":{" +
            "\"textDocument\":{\"uri\":\"file:///nope.ninja\"}," +
            "\"position\":{\"line\":0,\"character\":0}" +
            "}}"));

        var compl = responses.Single(r =>
            r.RootElement.TryGetProperty("id", out var idEl) && idEl.GetInt32() == 78);
        var items = compl.RootElement.GetProperty("result").GetProperty("items");
        await Assert.That(items.GetArrayLength()).IsEqualTo(0);
    }

    // ─── hover ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Initialize_Advertises_HoverProvider()
    {
        var responses = RunServerAndCollect(BuildInput(Initialize()));
        var caps = responses[0].RootElement.GetProperty("result").GetProperty("capabilities");
        await Assert.That(caps.GetProperty("hoverProvider").GetBoolean()).IsTrue();
    }

    [Test]
    public async Task Hover_OnBuiltin_ReturnsMarkdownContentsAndRange()
    {
        const string uri = "file:///hover.ninja";
        var responses = RunServerAndCollect(BuildInput(
            Initialize(),
            DidOpen(uri, "where"),
            $"{{\"jsonrpc\":\"2.0\",\"id\":11,\"method\":\"textDocument/hover\",\"params\":{{" +
            $"\"textDocument\":{{\"uri\":\"{uri}\"}}," +
            "\"position\":{\"line\":0,\"character\":3}" +
            "}}"));

        var hover = responses.Single(r =>
            r.RootElement.TryGetProperty("id", out var idEl) && idEl.GetInt32() == 11);
        var result = hover.RootElement.GetProperty("result");
        var contents = result.GetProperty("contents");
        await Assert.That(contents.GetProperty("kind").GetString()).IsEqualTo("markdown");
        await Assert.That(contents.GetProperty("value").GetString()!).Contains("where");
        var range = result.GetProperty("range");
        await Assert.That(range.GetProperty("start").GetProperty("character").GetInt32()).IsEqualTo(0);
        await Assert.That(range.GetProperty("end").GetProperty("character").GetInt32()).IsEqualTo(5);
    }

    [Test]
    public async Task Hover_OnUnknownIdentifier_ReturnsNullResult()
    {
        const string uri = "file:///hover-nope.ninja";
        var responses = RunServerAndCollect(BuildInput(
            Initialize(),
            DidOpen(uri, "xyzdoesnotexist"),
            $"{{\"jsonrpc\":\"2.0\",\"id\":12,\"method\":\"textDocument/hover\",\"params\":{{" +
            $"\"textDocument\":{{\"uri\":\"{uri}\"}}," +
            "\"position\":{\"line\":0,\"character\":5}" +
            "}}"));

        var hover = responses.Single(r =>
            r.RootElement.TryGetProperty("id", out var idEl) && idEl.GetInt32() == 12);
        await Assert.That(hover.RootElement.GetProperty("result").ValueKind).IsEqualTo(JsonValueKind.Null);
    }

    [Test]
    public async Task Hover_OnUnknownUri_ReturnsNullResult()
    {
        var responses = RunServerAndCollect(BuildInput(
            Initialize(),
            "{\"jsonrpc\":\"2.0\",\"id\":13,\"method\":\"textDocument/hover\",\"params\":{" +
            "\"textDocument\":{\"uri\":\"file:///never-opened.ninja\"}," +
            "\"position\":{\"line\":0,\"character\":0}" +
            "}}"));

        var hover = responses.Single(r =>
            r.RootElement.TryGetProperty("id", out var idEl) && idEl.GetInt32() == 13);
        await Assert.That(hover.RootElement.GetProperty("result").ValueKind).IsEqualTo(JsonValueKind.Null);
    }

    // ─── definition ─────────────────────────────────────────────────────────

    [Test]
    public async Task Initialize_Advertises_DefinitionProvider()
    {
        var responses = RunServerAndCollect(BuildInput(Initialize()));
        var caps = responses[0].RootElement.GetProperty("result").GetProperty("capabilities");
        await Assert.That(caps.GetProperty("definitionProvider").GetBoolean()).IsTrue();
    }

    [Test]
    public async Task Definition_OnReference_ReturnsLocationAtLetName()
    {
        const string uri = "file:///def.ninja";
        var responses = RunServerAndCollect(BuildInput(
            Initialize(),
            // "let x = 42\nx" — request go-to-def on the `x` reference on line 1.
            DidOpen(uri, "let x = 42\nx"),
            $"{{\"jsonrpc\":\"2.0\",\"id\":21,\"method\":\"textDocument/definition\",\"params\":{{" +
            $"\"textDocument\":{{\"uri\":\"{uri}\"}}," +
            "\"position\":{\"line\":1,\"character\":0}" +
            "}}"));

        var def = responses.Single(r =>
            r.RootElement.TryGetProperty("id", out var idEl) && idEl.GetInt32() == 21);
        var result = def.RootElement.GetProperty("result");
        await Assert.That(result.GetProperty("uri").GetString()).IsEqualTo(uri);
        var range = result.GetProperty("range");
        // "let " is 4 chars → identifier starts at column 4 on line 0.
        await Assert.That(range.GetProperty("start").GetProperty("line").GetInt32()).IsEqualTo(0);
        await Assert.That(range.GetProperty("start").GetProperty("character").GetInt32()).IsEqualTo(4);
        await Assert.That(range.GetProperty("end").GetProperty("character").GetInt32()).IsEqualTo(5);
    }

    [Test]
    public async Task Definition_OnUnknownIdentifier_ReturnsNullResult()
    {
        const string uri = "file:///def-nope.ninja";
        var responses = RunServerAndCollect(BuildInput(
            Initialize(),
            DidOpen(uri, "let x = 42\nunknownName"),
            $"{{\"jsonrpc\":\"2.0\",\"id\":22,\"method\":\"textDocument/definition\",\"params\":{{" +
            $"\"textDocument\":{{\"uri\":\"{uri}\"}}," +
            "\"position\":{\"line\":1,\"character\":3}" +
            "}}"));

        var def = responses.Single(r =>
            r.RootElement.TryGetProperty("id", out var idEl) && idEl.GetInt32() == 22);
        await Assert.That(def.RootElement.GetProperty("result").ValueKind).IsEqualTo(JsonValueKind.Null);
    }

    [Test]
    public async Task Definition_OnUnknownUri_ReturnsNullResult()
    {
        var responses = RunServerAndCollect(BuildInput(
            Initialize(),
            "{\"jsonrpc\":\"2.0\",\"id\":23,\"method\":\"textDocument/definition\",\"params\":{" +
            "\"textDocument\":{\"uri\":\"file:///never-opened.ninja\"}," +
            "\"position\":{\"line\":0,\"character\":0}" +
            "}}"));

        var def = responses.Single(r =>
            r.RootElement.TryGetProperty("id", out var idEl) && idEl.GetInt32() == 23);
        await Assert.That(def.RootElement.GetProperty("result").ValueKind).IsEqualTo(JsonValueKind.Null);
    }

    [Test]
    public async Task DocumentSymbol_OnUnknownUri_ReturnsEmptyArray()
    {
        var responses = RunServerAndCollect(BuildInput(
            Initialize(),
            "{\"jsonrpc\":\"2.0\",\"id\":5,\"method\":\"textDocument/documentSymbol\",\"params\":{" +
            "\"textDocument\":{\"uri\":\"file:///never-opened.ninja\"}" +
            "}}"));

        var sym = responses.Single(r =>
            r.RootElement.TryGetProperty("id", out var idEl) && idEl.GetInt32() == 5);
        var arr = sym.RootElement.GetProperty("result");
        await Assert.That(arr.GetArrayLength()).IsEqualTo(0);
    }

    private static string Method(JsonDocument doc)
    {
        if (doc.RootElement.TryGetProperty("method", out var m) && m.ValueKind == JsonValueKind.String)
            return m.GetString() ?? "";
        return "";
    }
}
