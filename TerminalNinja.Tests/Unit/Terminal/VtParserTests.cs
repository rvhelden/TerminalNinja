using System.Text;
using TerminalNinja.Terminal;

namespace TerminalNinja.Tests.Unit.Terminal;

/// <summary>
/// Tests for <see cref="VtParser"/> covering the common ANSI sequences a real PTY emits:
/// printable text, C0 controls, SGR, cursor moves, erase, DEC private modes, OSC strings,
/// UTF-8 multi-byte print, and stream-splitting at arbitrary byte boundaries.
/// </summary>
public class VtParserTests
{
    private static (VtParser parser, RecordingHandler handler) Setup()
    {
        return (new VtParser(), new RecordingHandler());
    }

    private static void Feed(VtParser parser, RecordingHandler handler, string ascii)
    {
        parser.Feed(Encoding.ASCII.GetBytes(ascii).AsSpan(), handler);
    }

    [Test]
    public async Task Print_AsciiText_EmitsCodepointPerByte()
    {
        var (p, h) = Setup();
        Feed(p, h, "abc");

        await Assert.That(h.Events.Count).IsEqualTo(3);
        await Assert.That(h.Events[0]).IsEqualTo(("Print", (uint)'a'));
        await Assert.That(h.Events[1]).IsEqualTo(("Print", (uint)'b'));
        await Assert.That(h.Events[2]).IsEqualTo(("Print", (uint)'c'));
    }

    [Test]
    public async Task Execute_NewlineAndCarriageReturn_FireExecuteEvents()
    {
        var (p, h) = Setup();
        Feed(p, h, "a\r\nb");

        await Assert.That(h.Events.Count).IsEqualTo(4);
        await Assert.That(h.Events[0].Kind).IsEqualTo("Print");
        await Assert.That(h.Events[1]).IsEqualTo(("Execute", (uint)0x0D)); // CR
        await Assert.That(h.Events[2]).IsEqualTo(("Execute", (uint)0x0A)); // LF
        await Assert.That(h.Events[3].Kind).IsEqualTo("Print");
    }

    [Test]
    public async Task Csi_Sgr_BoldRedForeground_ParsedCorrectly()
    {
        // ESC [ 1 ; 31 m
        var (p, h) = Setup();
        p.Feed("\x1B[1;31m"u8, h);

        await Assert.That(h.CsiEvents.Count).IsEqualTo(1);
        var csi = h.CsiEvents[0];
        await Assert.That(csi.FinalByte).IsEqualTo((byte)'m');
        await Assert.That(csi.Parameters).IsEquivalentTo(new[] { 1, 31 });
        await Assert.That(csi.IsPrivate).IsFalse();
        await Assert.That(csi.Intermediates.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Csi_CursorPositionWithDefaults_EmitsMinusOneForMissingParams()
    {
        // ESC [ ; 5 H  — row default (-1), col 5
        var (p, h) = Setup();
        p.Feed("\x1B[;5H"u8, h);

        await Assert.That(h.CsiEvents.Count).IsEqualTo(1);
        var csi = h.CsiEvents[0];
        await Assert.That(csi.FinalByte).IsEqualTo((byte)'H');
        await Assert.That(csi.Parameters).IsEquivalentTo(new[] { -1, 5 });
    }

    [Test]
    public async Task Csi_PrivateMode_DecSetCursorBlink_FlagsPrivate()
    {
        // ESC [ ? 25 h  — show cursor
        var (p, h) = Setup();
        p.Feed("\x1B[?25h"u8, h);

        await Assert.That(h.CsiEvents.Count).IsEqualTo(1);
        var csi = h.CsiEvents[0];
        await Assert.That(csi.IsPrivate).IsTrue();
        await Assert.That(csi.Parameters).IsEquivalentTo(new[] { 25 });
        await Assert.That(csi.FinalByte).IsEqualTo((byte)'h');
    }

    [Test]
    public async Task Csi_EraseDisplay_NoParams_DefaultsToMinusOne()
    {
        // ESC [ J
        var (p, h) = Setup();
        p.Feed("\x1B[J"u8, h);

        await Assert.That(h.CsiEvents.Count).IsEqualTo(1);
        var csi = h.CsiEvents[0];
        await Assert.That(csi.FinalByte).IsEqualTo((byte)'J');
        await Assert.That(csi.Parameters.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Esc_RisHardReset_EmitsEscDispatch()
    {
        // ESC c — note: "\x1Bc" parses in C# as \x1Bc = U+01BC (the hex
        // literal eats the 'c'), so feed the bytes explicitly.
        var (p, h) = Setup();
        p.Feed(new byte[] { 0x1B, (byte)'c' }, h);

        await Assert.That(h.EscEvents.Count).IsEqualTo(1);
        await Assert.That(h.EscEvents[0].FinalByte).IsEqualTo((byte)'c');
        await Assert.That(h.EscEvents[0].Intermediates.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Osc_WindowTitle_BelTerminated()
    {
        // ESC ] 0 ; Hello BEL
        var (p, h) = Setup();
        p.Feed("\x1B]0;Hello\x07"u8, h);

        await Assert.That(h.OscEvents.Count).IsEqualTo(1);
        await Assert.That(h.OscEvents[0].Command).IsEqualTo(0);
        await Assert.That(Encoding.ASCII.GetString(h.OscEvents[0].Data)).IsEqualTo("Hello");
    }

    [Test]
    public async Task Osc_Hyperlink_StTerminated()
    {
        // ESC ] 8 ; ; https://example.com ESC \
        var (p, h) = Setup();
        p.Feed("\x1B]8;;https://example.com\x1B\\"u8, h);

        await Assert.That(h.OscEvents.Count).IsEqualTo(1);
        await Assert.That(h.OscEvents[0].Command).IsEqualTo(8);
        await Assert.That(Encoding.ASCII.GetString(h.OscEvents[0].Data)).IsEqualTo(";https://example.com");
    }

    [Test]
    public async Task Print_Utf8MultiByteEmoji_DecodesToCodepoint()
    {
        // U+1F600 😀 = F0 9F 98 80
        var (p, h) = Setup();
        p.Feed(new byte[] { 0xF0, 0x9F, 0x98, 0x80 }, h);

        await Assert.That(h.Events.Count).IsEqualTo(1);
        await Assert.That(h.Events[0]).IsEqualTo(("Print", 0x1F600u));
    }

    [Test]
    public async Task Print_Utf8Cjk_DecodesToCodepoint()
    {
        // U+4E2D 中 = E4 B8 AD
        var (p, h) = Setup();
        p.Feed(new byte[] { 0xE4, 0xB8, 0xAD }, h);

        await Assert.That(h.Events.Count).IsEqualTo(1);
        await Assert.That(h.Events[0]).IsEqualTo(("Print", 0x4E2Du));
    }

    [Test]
    public async Task Feed_SplitMidSequence_StateCarriesAcrossCalls()
    {
        // ESC [ 1 ; 3 1 m  fed one byte at a time
        var parser = new VtParser();
        var handler = new RecordingHandler();

        foreach (var b in "\x1B[1;31m"u8.ToArray())
        {
            parser.Feed(new[] { b }, handler);
        }

        await Assert.That(handler.CsiEvents.Count).IsEqualTo(1);
        await Assert.That(handler.CsiEvents[0].Parameters).IsEquivalentTo(new[] { 1, 31 });
    }

    [Test]
    public async Task Feed_MixedPrintAndSequence_OrderedCorrectly()
    {
        // "a" ESC [ 31 m "b"
        var (p, h) = Setup();
        p.Feed("a\x1B[31mb"u8, h);

        await Assert.That(h.Events.Count).IsEqualTo(3);
        await Assert.That(h.Events[0]).IsEqualTo(("Print", (uint)'a'));
        await Assert.That(h.Events[1].Kind).IsEqualTo("Csi");
        await Assert.That(h.Events[2]).IsEqualTo(("Print", (uint)'b'));
    }

    [Test]
    public async Task Csi_ManyParams_TruncatesAt16()
    {
        var sb = new StringBuilder("\x1B[");
        for (var i = 0; i < 20; i++)
        {
            if (i > 0) sb.Append(';');
            sb.Append(i);
        }
        sb.Append('m');

        var (p, h) = Setup();
        p.Feed(Encoding.ASCII.GetBytes(sb.ToString()), h);

        await Assert.That(h.CsiEvents.Count).IsEqualTo(1);
        await Assert.That(h.CsiEvents[0].Parameters.Length).IsEqualTo(16);
        await Assert.That(h.CsiEvents[0].Parameters[0]).IsEqualTo(0);
        await Assert.That(h.CsiEvents[0].Parameters[15]).IsEqualTo(15);
    }

    [Test]
    public async Task Reset_ClearsStateMidSequence()
    {
        var (p, h) = Setup();
        p.Feed("\x1B[1;"u8, h); // mid-CSI
        p.Reset();
        p.Feed("a"u8, h);

        await Assert.That(h.CsiEvents.Count).IsEqualTo(0);
        await Assert.That(h.Events.Count).IsEqualTo(1);
        await Assert.That(h.Events[0]).IsEqualTo(("Print", (uint)'a'));
    }

    private sealed class RecordingHandler : IVtParserHandler
    {
        public List<(string Kind, uint Value)> Events { get; } = [];
        public List<CsiRecord> CsiEvents { get; } = [];
        public List<EscRecord> EscEvents { get; } = [];
        public List<OscRecord> OscEvents { get; } = [];

        public void OnPrint(uint codepoint) => Events.Add(("Print", codepoint));
        public void OnExecute(byte controlByte) => Events.Add(("Execute", controlByte));

        public void OnCsiDispatch(byte finalByte, ReadOnlySpan<int> parameters, ReadOnlySpan<byte> intermediates, bool isPrivate)
        {
            CsiEvents.Add(new CsiRecord(finalByte, parameters.ToArray(), intermediates.ToArray(), isPrivate));
            Events.Add(("Csi", finalByte));
        }

        public void OnEscDispatch(byte finalByte, ReadOnlySpan<byte> intermediates)
        {
            EscEvents.Add(new EscRecord(finalByte, intermediates.ToArray()));
            Events.Add(("Esc", finalByte));
        }

        public void OnOscDispatch(int command, ReadOnlySpan<byte> data)
        {
            OscEvents.Add(new OscRecord(command, data.ToArray()));
            Events.Add(("Osc", (uint)command));
        }

        public sealed record CsiRecord(byte FinalByte, int[] Parameters, byte[] Intermediates, bool IsPrivate);
        public sealed record EscRecord(byte FinalByte, byte[] Intermediates);
        public sealed record OscRecord(int Command, byte[] Data);
    }
}
