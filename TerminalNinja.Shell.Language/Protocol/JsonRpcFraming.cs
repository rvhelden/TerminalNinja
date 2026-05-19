using System.Buffers;
using System.Text;
using System.Text.Json;

namespace TerminalNinja.Shell.Language.Protocol;

/// <summary>
/// Reader for Content-Length-framed JSON-RPC messages over a byte stream — the
/// transport layer shared by LSP (Language Server Protocol) and DAP (Debug
/// Adapter Protocol). Each message is preceded by ASCII headers terminated by
/// a blank line; the body is a UTF-8 encoded JSON payload of
/// <c>Content-Length</c> bytes.
/// </summary>
public sealed class JsonRpcReader
{
    private readonly Stream _input;

    /// <summary>Wrap <paramref name="input"/> for framed message reading.</summary>
    public JsonRpcReader(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        _input = input;
    }

    /// <summary>Read the next message, or return <c>null</c> when the stream is exhausted.</summary>
    public JsonDocument? ReadMessage()
    {
        // Skip leading blank lines from a peer that hangs up cleanly between frames
        // (or a smoke test that pipes an empty newline at us). The first non-blank
        // line is treated as the start of a real header block.
        string? line;
        do
        {
            line = ReadHeaderLine();
            if (line is null) return null; // EOF
        } while (line.Length == 0);

        int contentLength = -1;
        while (true)
        {
            var sep = line!.IndexOf(':');
            if (sep < 0)
                throw new InvalidDataException($"malformed header line (no ':'): '{line}'");
            var name = line[..sep].Trim();
            var value = line[(sep + 1)..].Trim();
            if (string.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(value, out contentLength) || contentLength < 0)
                    throw new InvalidDataException($"invalid Content-Length: '{value}'");
            }
            // Content-Type and other headers are accepted but ignored.

            line = ReadHeaderLine();
            if (line is null) return null; // EOF mid-header — treat as clean shutdown
            if (line.Length == 0) break;    // blank line terminates headers
        }

        if (contentLength < 0)
            throw new InvalidDataException("framed message missing Content-Length header");

        var body = new byte[contentLength];
        int read = 0;
        while (read < contentLength)
        {
            int n = _input.Read(body, read, contentLength - read);
            if (n <= 0) throw new EndOfStreamException("truncated message body");
            read += n;
        }
        return JsonDocument.Parse(body);
    }

    /// <summary>Read a single ASCII line terminated by CRLF, returning the line text (without the terminator) or <c>null</c> at EOF.</summary>
    private string? ReadHeaderLine()
    {
        var sb = new StringBuilder(64);
        while (true)
        {
            int b = _input.ReadByte();
            if (b < 0) return sb.Length == 0 ? null : sb.ToString();
            if (b == '\r')
            {
                int next = _input.ReadByte();
                if (next == '\n') return sb.ToString();
                if (next < 0) return sb.ToString();
                throw new InvalidDataException($"expected LF after CR in header, got 0x{next:X2}");
            }
            if (b == '\n') return sb.ToString();
            sb.Append((char)b);
        }
    }
}

/// <summary>Writer for Content-Length-framed JSON-RPC messages over a byte stream.</summary>
public sealed class JsonRpcWriter
{
    private readonly Stream _output;
    private readonly object _writeLock = new();

    /// <summary>Wrap <paramref name="output"/> for framed message writing.</summary>
    public JsonRpcWriter(Stream output)
    {
        ArgumentNullException.ThrowIfNull(output);
        _output = output;
    }

    /// <summary>Write <paramref name="bodyJson"/> as a framed message.</summary>
    public void WriteMessage(ReadOnlySpan<byte> bodyJson)
    {
        var header = Encoding.ASCII.GetBytes($"Content-Length: {bodyJson.Length}\r\n\r\n");
        lock (_writeLock)
        {
            _output.Write(header, 0, header.Length);
            _output.Write(bodyJson);
            _output.Flush();
        }
    }

    /// <summary>Write the contents of a <see cref="ArrayBufferWriter{T}"/>.</summary>
    public void WriteMessage(ArrayBufferWriter<byte> buffer) => WriteMessage(buffer.WrittenSpan);
}
