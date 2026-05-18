using System.Text;
using TerminalNinja.Shell.LanguageServer.Protocol;

namespace TerminalNinja.Tests.Unit.LanguageServer;

/// <summary>Coverage for the LSP base-protocol Content-Length framing layer.</summary>
public class JsonRpcFramingTests
{
    private static byte[] Frame(string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n");
        var combined = new byte[header.Length + bytes.Length];
        Buffer.BlockCopy(header, 0, combined, 0, header.Length);
        Buffer.BlockCopy(bytes, 0, combined, header.Length, bytes.Length);
        return combined;
    }

    [Test]
    public async Task Reader_SingleMessage_ParsesBody()
    {
        var input = new MemoryStream(Frame("{\"id\":1}"));
        var reader = new JsonRpcReader(input);
        using var doc = reader.ReadMessage();
        await Assert.That(doc).IsNotNull();
        await Assert.That(doc!.RootElement.GetProperty("id").GetInt32()).IsEqualTo(1);
    }

    [Test]
    public async Task Reader_TwoMessagesBackToBack_BothParsed()
    {
        var bytes = new List<byte>();
        bytes.AddRange(Frame("{\"id\":1}"));
        bytes.AddRange(Frame("{\"id\":2}"));
        var input = new MemoryStream(bytes.ToArray());
        var reader = new JsonRpcReader(input);

        using var doc1 = reader.ReadMessage();
        using var doc2 = reader.ReadMessage();
        await Assert.That(doc1!.RootElement.GetProperty("id").GetInt32()).IsEqualTo(1);
        await Assert.That(doc2!.RootElement.GetProperty("id").GetInt32()).IsEqualTo(2);
    }

    [Test]
    public async Task Reader_EmptyInput_ReturnsNull()
    {
        var reader = new JsonRpcReader(new MemoryStream());
        var doc = reader.ReadMessage();
        await Assert.That(doc).IsNull();
    }

    [Test]
    public async Task Reader_MissingContentLength_Throws()
    {
        var input = new MemoryStream(Encoding.ASCII.GetBytes("X-Other: 1\r\n\r\n{}"));
        var reader = new JsonRpcReader(input);
        await Assert.That(() => reader.ReadMessage()).ThrowsExactly<InvalidDataException>();
    }

    [Test]
    public async Task Reader_TruncatedBody_Throws()
    {
        // Claim 100 bytes but provide 2.
        var input = new MemoryStream(Encoding.ASCII.GetBytes("Content-Length: 100\r\n\r\n{}"));
        var reader = new JsonRpcReader(input);
        await Assert.That(() => reader.ReadMessage()).ThrowsExactly<EndOfStreamException>();
    }

    [Test]
    public async Task Writer_ProducesCorrectFraming()
    {
        var output = new MemoryStream();
        var writer = new JsonRpcWriter(output);
        var body = "{\"ok\":true}"u8;
        writer.WriteMessage(body);

        var written = Encoding.UTF8.GetString(output.ToArray());
        await Assert.That(written).IsEqualTo("Content-Length: 11\r\n\r\n{\"ok\":true}");
    }

    [Test]
    public async Task RoundTrip_WriterToReader_PreservesPayload()
    {
        var pipe = new MemoryStream();
        var writer = new JsonRpcWriter(pipe);
        writer.WriteMessage("{\"hello\":\"world\"}"u8);
        pipe.Position = 0;
        var reader = new JsonRpcReader(pipe);
        using var doc = reader.ReadMessage();
        await Assert.That(doc!.RootElement.GetProperty("hello").GetString()).IsEqualTo("world");
    }
}
