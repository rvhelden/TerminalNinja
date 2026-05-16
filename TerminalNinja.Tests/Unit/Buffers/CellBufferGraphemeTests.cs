using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Tests.Unit.Buffers;

/// <summary>
/// Tests for the per-row grapheme cluster side table on <see cref="CellBuffer"/>:
/// multi-codepoint clusters are stored, retrieved, swept on overwrite, preserved on
/// Resize, swapped in lockstep with cells, and surfaced by the diff enumerator when
/// only the cluster sequence changes (same lead + colors but different combining marks).
/// </summary>
public class CellBufferGraphemeTests
{
    [Test]
    public async Task SetGrapheme_StoresFullCluster()
    {
        using var buffer = new CellBuffer(5, 1);

        // Decomposed é = e + U+0301 combining acute accent.
        ReadOnlySpan<uint> cluster = stackalloc uint[] { (uint)'e', 0x0301u };
        buffer.SetGrapheme(0, 0, cluster, Color.White, Color.Black, TextDecorations.None);

        var lead = buffer.GetCell(0, 0);
        await Assert.That(lead.Codepoint).IsEqualTo((uint)'e');
        await Assert.That(lead.Flags & CellFlags.HasGrapheme).IsEqualTo(CellFlags.HasGrapheme);

        var stored = buffer.GetGrapheme(0, 0);
        await Assert.That(stored.Length).IsEqualTo(2);
        await Assert.That(stored[0]).IsEqualTo((uint)'e');
        await Assert.That(stored[1]).IsEqualTo(0x0301u);
    }

    [Test]
    public async Task SetGrapheme_WideEmoji_MarksWideLeadAndTrail()
    {
        using var buffer = new CellBuffer(10, 1);

        // Family ZWJ emoji: 👨‍👩‍👧‍👦
        ReadOnlySpan<uint> cluster = stackalloc uint[]
        {
            0x1F468u, 0x200Du, 0x1F469u, 0x200Du, 0x1F467u, 0x200Du, 0x1F466u,
        };
        buffer.SetGrapheme(0, 0, cluster, Color.White, Color.Black, TextDecorations.None);

        var lead = buffer.GetCell(0, 0);
        var trail = buffer.GetCell(1, 0);

        await Assert.That(lead.Flags & CellFlags.HasGrapheme).IsEqualTo(CellFlags.HasGrapheme);
        await Assert.That(lead.Flags & CellFlags.WideLead).IsEqualTo(CellFlags.WideLead);
        await Assert.That(trail.Flags & CellFlags.WideTrail).IsEqualTo(CellFlags.WideTrail);
        await Assert.That(buffer.GetGrapheme(0, 0).Length).IsEqualTo(7);
    }

    [Test]
    public async Task SetChar_OverwritingGrapheme_DropsSideTableEntry()
    {
        using var buffer = new CellBuffer(5, 1);
        ReadOnlySpan<uint> cluster = stackalloc uint[] { (uint)'e', 0x0301u };
        buffer.SetGrapheme(0, 0, cluster, Color.White, Color.Black, TextDecorations.None);
        await Assert.That(buffer.GetGrapheme(0, 0).Length).IsEqualTo(2);

        buffer.SetChar(0, 0, (uint)'X', Color.White, Color.Black);

        var cell = buffer.GetCell(0, 0);
        await Assert.That(cell.Codepoint).IsEqualTo((uint)'X');
        await Assert.That(cell.Flags & CellFlags.HasGrapheme).IsEqualTo(CellFlags.None);

        // GetGrapheme falls back to the single-codepoint view when no entry exists.
        var seq = buffer.GetGrapheme(0, 0);
        await Assert.That(seq.Length).IsEqualTo(1);
        await Assert.That(seq[0]).IsEqualTo((uint)'X');
    }

    [Test]
    public async Task GetGrapheme_NonGraphemeCell_ReturnsSingleCodepointFallback()
    {
        using var buffer = new CellBuffer(3, 1);
        buffer.SetChar(0, 0, (uint)'A', Color.White, Color.Black);

        var seq = buffer.GetGrapheme(0, 0);
        await Assert.That(seq.Length).IsEqualTo(1);
        await Assert.That(seq[0]).IsEqualTo((uint)'A');
    }

    [Test]
    public async Task Resize_PreservesGraphemesWithinNewBounds_DropsOutOfRangeColumns()
    {
        using var buffer = new CellBuffer(10, 3);
        ReadOnlySpan<uint> cluster = stackalloc uint[] { (uint)'e', 0x0301u };

        buffer.SetGrapheme(2, 0, cluster, Color.White, Color.Black, TextDecorations.None);
        buffer.SetGrapheme(8, 1, cluster, Color.White, Color.Black, TextDecorations.None);

        // Shrink to width 5 — the grapheme at column 8 should be dropped (column out of new range).
        // Column 2 should survive; both cells fall inside the new 5-wide buffer.
        buffer.Resize(5, 3);

        await Assert.That(buffer.GetGrapheme(2, 0).Length).IsEqualTo(2);
        await Assert.That(buffer.GetGrapheme(8, 1).Length).IsEqualTo(0); // out of new bounds → empty
    }

    [Test]
    public async Task SwapBuffers_SwapsRowGraphemes()
    {
        using var buffer = new CellBuffer(5, 1);
        ReadOnlySpan<uint> cluster = stackalloc uint[] { (uint)'e', 0x0301u };
        buffer.SetGrapheme(0, 0, cluster, Color.White, Color.Black, TextDecorations.None);

        // Before swap: _current has the cluster at (0, 0). After swap: _previous has it,
        // _current is whatever was in _previous before (Cell.Empty / space, no clusters).
        buffer.SwapBuffers();

        var afterSwap = buffer.GetCell(0, 0);
        // The new _current cell is Cell.Empty (space) with no grapheme flag — the cluster
        // and its side-table entry travelled to _previous together via the swap.
        await Assert.That(afterSwap.Codepoint).IsEqualTo((uint)' ');
        await Assert.That(afterSwap.Flags & CellFlags.HasGrapheme).IsEqualTo(CellFlags.None);
    }

    [Test]
    public async Task GetChanges_DetectsClusterSequenceChange()
    {
        using var buffer = new CellBuffer(5, 1);

        // Frame 1: write é, present (swap), screen now matches.
        ReadOnlySpan<uint> clusterA = stackalloc uint[] { (uint)'e', 0x0301u };
        buffer.SetGrapheme(0, 0, clusterA, Color.White, Color.Black, TextDecorations.None);
        foreach (var _ in buffer.GetChanges()) { }
        buffer.SwapBuffers();

        // Frame 2: write a different cluster with the SAME lead codepoint and colors —
        // the Cell-level comparison would say "no change", but the cluster array differs.
        ReadOnlySpan<uint> clusterB = stackalloc uint[] { (uint)'e', 0x0302u }; // circumflex
        buffer.SetGrapheme(0, 0, clusterB, Color.White, Color.Black, TextDecorations.None);

        var captured = new List<(int X, int Y)>();
        foreach (var change in buffer.GetChanges())
        {
            captured.Add((change.X, change.Y));
        }

        await Assert.That(captured.Count).IsEqualTo(1);
        await Assert.That(captured[0]).IsEqualTo((0, 0));
    }
}
