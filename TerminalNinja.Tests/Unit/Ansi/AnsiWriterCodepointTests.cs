using System.Text;
using TerminalNinja.Ansi;
using TerminalNinja.Primitives;
using TerminalNinja.Tests.Helpers;

namespace TerminalNinja.Tests.Unit.Ansi;

/// <summary>
/// Tests for the <see cref="AnsiWriter.WriteCodepoint(uint)"/> path: ASCII fast path,
/// 4-byte UTF-8 encoding for non-BMP scalars, and the wide-character cursor advance
/// that fixes the latent <c>_cursorX++</c> bug.
/// </summary>
public class AnsiWriterCodepointTests
{
    [Test]
    public async Task WriteCodepoint_Ascii_EmitsSingleByte()
    {
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.WriteCodepoint((uint)'A'));
        await Assert.That(output).IsEqualTo("A");
    }

    [Test]
    public async Task WriteCodepoint_NonBmp_EmitsFourByteUtf8()
    {
        // U+1F600 😀 = 0xF0 0x9F 0x98 0x80
        using var stream = new MemoryStream();
        using (var writer = new AnsiWriter(stream))
        {
            writer.WriteCodepoint(0x1F600u);
            writer.Flush();
        }

        var bytes = stream.ToArray();
        await Assert.That(bytes.Length).IsEqualTo(4);
        await Assert.That(bytes[0]).IsEqualTo((byte)0xF0);
        await Assert.That(bytes[1]).IsEqualTo((byte)0x9F);
        await Assert.That(bytes[2]).IsEqualTo((byte)0x98);
        await Assert.That(bytes[3]).IsEqualTo((byte)0x80);
    }

    [Test]
    public async Task WriteCell_WideTrail_IsSkipped()
    {
        // A WideTrail cell must produce zero output — the lead cell already emitted the codepoint
        // and the cursor advanced by 2.
        var trail = new Cell(0u, Color.White, Color.Black, TextDecorations.None, CellFlags.WideTrail);
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.WriteCell(5, 0, trail));

        await Assert.That(output).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task WriteCell_WideLeadThenNarrow_DoesNotEmitMoveBeforeNarrow()
    {
        // After writing a wide cell at (0, 0), the cursor sits at column 2. Writing a narrow
        // cell at (2, 0) should NOT need to emit a MoveTo — the cursor is already there.
        using var stream = new MemoryStream();
        using (var writer = new AnsiWriter(stream))
        {
            writer.BeginFrame(); // resets cursor tracking
            writer.WriteCell(0, 0, new Cell(0x4E2Du, Color.White, Color.Black, TextDecorations.None, CellFlags.WideLead));
            writer.WriteCell(2, 0, new Cell((uint)'a', Color.White, Color.Black));
            writer.Flush();
        }

        var text = Encoding.UTF8.GetString(stream.ToArray());
        // The output starts with a MoveTo to (0, 0) and contains '中' followed immediately by 'a' —
        // no MoveTo escape sequence between them. We assert by checking for the literal pair.
        await Assert.That(text).Contains("中a");
    }
}
