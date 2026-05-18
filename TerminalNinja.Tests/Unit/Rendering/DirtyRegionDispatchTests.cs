using TerminalNinja.Buffers;
using TerminalNinja.Controls;
using TerminalNinja.Primitives;
using TerminalNinja.Rendering;

namespace TerminalNinja.Tests.Unit.Rendering;

/// <summary>
/// Tests for the FBO + dirty-blit rendering model: Renderer.Present must compute the
/// tight bounding box of actually-changed cells (not the loose buffer-level dirty rect),
/// invoke <c>ClearRegion</c> on the shaped sink, and emit only runs that intersect that
/// bounding box. Steady-state frames that re-render identical content should produce
/// zero ClearRegion / WriteRun calls so the persistent surface keeps its pixels.
/// </summary>
public class DirtyRegionDispatchTests
{
    [Test]
    public async Task FirstFrame_PaintsAllContent_AndCallsClearRegionOnce()
    {
        var sink = new RecordingShapedSink();
        using var renderer = new Renderer(sink, 20, 3);

        var capture = new ManualCellWriter(b =>
        {
            b.SetChar(2, 0, (uint)'A', Color.White, Color.Black);
            b.SetChar(3, 0, (uint)'B', Color.White, Color.Black);
        });

        renderer.Draw(capture);
        renderer.Present();

        await Assert.That(sink.ClearRegions.Count).IsEqualTo(1);
        await Assert.That(sink.Runs.Count).IsEqualTo(1);
        await Assert.That(sink.Runs[0].Text).IsEqualTo("AB");
    }

    [Test]
    public async Task SecondFrame_IdenticalContent_EmitsNothing()
    {
        // The renderer must trust the persistent surface: when no cell content changes
        // between frames, no ClearRegion + no WriteRun. The dirty-blit relies on this —
        // emitting anything would smear the surface.
        var sink = new RecordingShapedSink();
        using var renderer = new Renderer(sink, 20, 3);

        var control = new ManualCellWriter(b =>
        {
            b.SetChar(0, 0, (uint)'X', Color.White, Color.Black);
        });

        renderer.Draw(control);
        renderer.Present();
        var afterFirstFrameRuns = sink.Runs.Count;
        var afterFirstFrameClears = sink.ClearRegions.Count;

        // Frame 2 with identical content
        renderer.Clear();
        renderer.Draw(control);
        renderer.Present();

        await Assert.That(sink.Runs.Count - afterFirstFrameRuns).IsEqualTo(0);
        await Assert.That(sink.ClearRegions.Count - afterFirstFrameClears).IsEqualTo(0);
    }

    [Test]
    public async Task ClearRegion_Bounds_MatchActualChangedCells()
    {
        // Two frames: first with content, second with content shifted by 5 cells. The
        // dirty bounding box should bound only cells that actually differ, NOT include
        // unchanged cells even if they fall in the buffer's loose dirty rect.
        var sink = new RecordingShapedSink();
        using var renderer = new Renderer(sink, 30, 3);

        var initial = new ManualCellWriter(b =>
        {
            b.SetChar(10, 1, (uint)'X', Color.White, Color.Black);
        });
        renderer.Draw(initial);
        renderer.Present();

        sink.ClearRegions.Clear();
        sink.Runs.Clear();

        // Change only cell (15, 1) by drawing both X at (10,1) AND Y at (15,1). The
        // renderer's Clear-then-Draw cycle re-touches both cells, but ComputeChangeBoundingBox
        // only finds (15, 1) since (10, 1) matches previous frame.
        var updated = new ManualCellWriter(b =>
        {
            b.SetChar(10, 1, (uint)'X', Color.White, Color.Black);
            b.SetChar(15, 1, (uint)'Y', Color.White, Color.Black);
        });
        renderer.Clear();
        renderer.Draw(updated);
        renderer.Present();

        await Assert.That(sink.ClearRegions.Count).IsEqualTo(1);
        var clear = sink.ClearRegions[0];
        await Assert.That(clear.CellX).IsEqualTo(15);
        await Assert.That(clear.CellY).IsEqualTo(1);
        await Assert.That(clear.CellWidth).IsEqualTo(1);
        await Assert.That(clear.CellHeight).IsEqualTo(1);
    }

    [Test]
    public async Task RunIntersectingDirtyArea_IsEmitted_OthersSkipped()
    {
        // Three runs on different rows. Change one cell in row 1. Only the run on row 1
        // should be re-emitted; runs on rows 0 and 2 are skipped.
        var sink = new RecordingShapedSink();
        using var renderer = new Renderer(sink, 30, 3);

        var initial = new ManualCellWriter(b =>
        {
            b.SetChar(0, 0, (uint)'A', Color.White, Color.Black);
            b.SetChar(0, 1, (uint)'B', Color.White, Color.Black);
            b.SetChar(0, 2, (uint)'C', Color.White, Color.Black);
        });
        renderer.Draw(initial);
        renderer.Present();

        sink.Runs.Clear();
        sink.ClearRegions.Clear();

        var updated = new ManualCellWriter(b =>
        {
            b.SetChar(0, 0, (uint)'A', Color.White, Color.Black);
            b.SetChar(0, 1, (uint)'b', Color.White, Color.Black); // changed
            b.SetChar(0, 2, (uint)'C', Color.White, Color.Black);
        });
        renderer.Clear();
        renderer.Draw(updated);
        renderer.Present();

        // Only one WriteRun for the row-1 change. Rows 0 and 2 keep their persistent pixels.
        await Assert.That(sink.Runs.Count).IsEqualTo(1);
        await Assert.That(sink.Runs[0].Y).IsEqualTo(1);
        await Assert.That(sink.Runs[0].Text).IsEqualTo("b");
    }

    [Test]
    public async Task LongRunPartiallyDirty_IsFullyEmitted_ForShapingContext()
    {
        // A run spans 8 cells. Only the middle cell changes. The renderer must emit the
        // ENTIRE run (not just the dirty cell) because ligatures span the full run.
        var sink = new RecordingShapedSink();
        using var renderer = new Renderer(sink, 20, 1);

        var initial = new ManualCellWriter(b =>
        {
            var text = "ligature";
            for (var i = 0; i < text.Length; i++)
            {
                b.SetChar(i, 0, text[i], Color.White, Color.Black);
            }
        });
        renderer.Draw(initial);
        renderer.Present();

        sink.Runs.Clear();
        sink.ClearRegions.Clear();

        var updated = new ManualCellWriter(b =>
        {
            var text = "ligaTure"; // 'a'→'T' at index 4
            for (var i = 0; i < text.Length; i++)
            {
                b.SetChar(i, 0, text[i], Color.White, Color.Black);
            }
        });
        renderer.Clear();
        renderer.Draw(updated);
        renderer.Present();

        await Assert.That(sink.Runs.Count).IsEqualTo(1);
        await Assert.That(sink.Runs[0].Text).IsEqualTo("ligaTure"); // full run, not just 'T'
        await Assert.That(sink.Runs[0].X).IsEqualTo(0); // starts at column 0, not at the dirty column
    }

    [Test]
    public async Task CellsThatBecameEmpty_AreCoveredByClearRegion()
    {
        // Previous frame: "ABCD". Current frame: just "AB" (cells 2-3 became empty).
        // The ClearRegion bounding box must include the cells that became empty so their
        // pixels in the persistent surface get wiped.
        var sink = new RecordingShapedSink();
        using var renderer = new Renderer(sink, 20, 1);

        var initial = new ManualCellWriter(b =>
        {
            b.SetChar(0, 0, (uint)'A', Color.White, Color.Black);
            b.SetChar(1, 0, (uint)'B', Color.White, Color.Black);
            b.SetChar(2, 0, (uint)'C', Color.White, Color.Black);
            b.SetChar(3, 0, (uint)'D', Color.White, Color.Black);
        });
        renderer.Draw(initial);
        renderer.Present();

        sink.Runs.Clear();
        sink.ClearRegions.Clear();

        var updated = new ManualCellWriter(b =>
        {
            b.SetChar(0, 0, (uint)'A', Color.White, Color.Black);
            b.SetChar(1, 0, (uint)'B', Color.White, Color.Black);
            // (2, 0) and (3, 0) deliberately not written — they revert to Cell.Empty after Clear.
        });
        renderer.Clear();
        renderer.Draw(updated);
        renderer.Present();

        // The dirty bbox should span cells 2-3 (the ones that became empty). ClearRegion
        // covers them so their pixels get wiped.
        await Assert.That(sink.ClearRegions.Count).IsEqualTo(1);
        var clear = sink.ClearRegions[0];
        await Assert.That(clear.CellX).IsLessThanOrEqualTo(2);
        await Assert.That(clear.CellX + clear.CellWidth).IsGreaterThanOrEqualTo(4);

        // No run is emitted for the empty cells — they're handled by ClearRegion alone.
        foreach (var r in sink.Runs)
        {
            await Assert.That(r.X).IsLessThan(2);
        }
    }

    [Test]
    public async Task PlainSink_DoesNotReceiveClearRegion()
    {
        // ClearRegion is part of IShapedRunSink, not ICellSink. A plain MemoryCellSink
        // never sees it — the Renderer's per-cell path is unchanged from Step 8.
        var memory = new MemoryCellSink();
        using var renderer = new Renderer(memory, 10, 1);

        var capture = new ManualCellWriter(b =>
        {
            b.SetChar(0, 0, (uint)'X', Color.White, Color.Black);
        });
        renderer.Draw(capture);
        renderer.Present();

        // Memory sink received WriteCell, not ClearRegion (it doesn't implement IShapedRunSink).
        await Assert.That(memory.Writes.Count).IsGreaterThan(0);
    }

    private sealed class RecordingShapedSink : IShapedRunSink
    {
        public List<(int X, int Y, string Text, Color Fg, Color Bg, TextDecorations Deco)> Runs { get; } = [];
        public List<(int CellX, int CellY, int CellWidth, int CellHeight)> ClearRegions { get; } = [];

        public void BeginFrame() { }
        public void EndFrame() { }
        public void Reset() { }
        public void Resize(int width, int height) { _ = width; _ = height; }
        public void Dispose() { }
        public void WriteCell(int x, int y, Cell cell) { _ = x; _ = y; _ = cell; }

        public void WriteRun(int x, int y, ReadOnlySpan<char> text, Color fg, Color bg, TextDecorations decorations)
        {
            Runs.Add((x, y, text.ToString(), fg, bg, decorations));
        }

        public void ClearRegion(int cellX, int cellY, int cellWidth, int cellHeight)
        {
            ClearRegions.Add((cellX, cellY, cellWidth, cellHeight));
        }
    }

    private sealed class ManualCellWriter : UIElement
    {
        private readonly Action<CellBuffer> _write;

        public ManualCellWriter(Action<CellBuffer> write)
        {
            _write = write;
        }

        public override Size2D GetPreferredSize(Rect availableSpace) => new(0, 0);
        public override Rect CalculateBounds(Rect parentBounds) => parentBounds;
        protected override void OnRender(CellBuffer buffer, Rect parentBounds) => _write(buffer);
    }
}
