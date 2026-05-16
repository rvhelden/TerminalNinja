using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Tests.Unit.Buffers;

/// <summary>
/// Tests for wide East Asian / emoji codepoint handling in <see cref="CellBuffer"/>:
/// the lead cell carries <see cref="CellFlags.WideLead"/>, the trail cell carries
/// <see cref="CellFlags.WideTrail"/> with Codepoint == 0, and the diff enumerator
/// skips trail cells so the renderer doesn't emit them.
/// </summary>
public class CellWideCharTests
{
    [Test]
    public async Task SetChar_NarrowCodepoint_DoesNotMarkFlags()
    {
        using var buffer = new CellBuffer(10, 2);
        buffer.SetChar(0, 0, (uint)'A', Color.White, Color.Black);

        var cell = buffer.GetCell(0, 0);
        await Assert.That(cell.Codepoint).IsEqualTo((uint)'A');
        await Assert.That(cell.Flags).IsEqualTo(CellFlags.None);
    }

    [Test]
    public async Task SetChar_WideCjk_WritesLeadAndTrail()
    {
        using var buffer = new CellBuffer(10, 2);

        // U+4E2D '中' is in the CJK Unified Ideographs range — wide.
        buffer.SetChar(0, 0, 0x4E2Du, Color.White, Color.Black);

        var lead = buffer.GetCell(0, 0);
        var trail = buffer.GetCell(1, 0);

        await Assert.That(lead.Codepoint).IsEqualTo(0x4E2Du);
        await Assert.That(lead.Flags & CellFlags.WideLead).IsEqualTo(CellFlags.WideLead);
        await Assert.That(trail.Codepoint).IsEqualTo(0u);
        await Assert.That(trail.Flags & CellFlags.WideTrail).IsEqualTo(CellFlags.WideTrail);
    }

    [Test]
    public async Task SetChar_NonBmpEmoji_FitsInUintCodepoint()
    {
        using var buffer = new CellBuffer(10, 2);

        // U+1F600 😀 — non-BMP, would not fit in a UTF-16 char field.
        buffer.SetChar(0, 0, 0x1F600u, Color.White, Color.Black);

        var lead = buffer.GetCell(0, 0);
        await Assert.That(lead.Codepoint).IsEqualTo(0x1F600u);
        await Assert.That(lead.Flags & CellFlags.WideLead).IsEqualTo(CellFlags.WideLead);
    }

    [Test]
    public async Task SetChar_WideAtRightEdge_FallsBackToSpace()
    {
        using var buffer = new CellBuffer(2, 1);

        // Width is 2; writing a wide char at x=1 would put the trail at x=2 (out of bounds).
        buffer.SetChar(1, 0, 0x4E2Du, Color.White, Color.Black);

        var cell = buffer.GetCell(1, 0);
        await Assert.That(cell.Codepoint).IsEqualTo((uint)' ');
        await Assert.That(cell.Flags).IsEqualTo(CellFlags.None);
    }

    [Test]
    public async Task GetChanges_SkipsWideTrailCells()
    {
        using var buffer = new CellBuffer(10, 2);
        buffer.SetChar(0, 0, 0x4E2Du, Color.White, Color.Black); // wide
        buffer.SetChar(2, 0, (uint)'a', Color.White, Color.Black); // narrow

        var emitted = new List<(int X, int Y, uint Codepoint)>();
        foreach (var change in buffer.GetChanges())
        {
            emitted.Add((change.X, change.Y, change.Cell.Codepoint));
        }

        // Lead at (0, 0) emitted; trail at (1, 0) skipped; narrow at (2, 0) emitted.
        await Assert.That(emitted).Contains((0, 0, 0x4E2Du));
        await Assert.That(emitted).Contains((2, 0, (uint)'a'));
        foreach (var write in emitted)
        {
            await Assert.That(write.X).IsNotEqualTo(1);
        }
    }
}
