using TerminalNinja.Buffers;
using TerminalNinja.Controls;
using TerminalNinja.Primitives;
using TerminalNinja.Rendering;

namespace TerminalNinja.Tests.Unit.Rendering;

/// <summary>
/// Tests for the Step 8 row-level shaped-run dispatch in <see cref="Renderer.Present"/>.
/// Drives the pipeline through a <see cref="RecordingShapedSink"/> to assert what runs
/// the renderer reconstructs from cell content.
/// </summary>
public class RowLevelDispatchTests
{
    [Test]
    public async Task GetDirtyRows_NoDirty_YieldsNothing()
    {
        using var buffer = new CellBuffer(10, 5);
        // SwapBuffers clears the dirty rect; constructor marks everything dirty so we need to swap.
        // Trick: do an empty Present cycle to reset state.
        var emitted = 0;
        foreach (var _ in buffer.GetDirtyRows()) { /* drain to reset */ }
        buffer.SwapBuffers();

        foreach (var _ in buffer.GetDirtyRows())
        {
            emitted++;
        }

        await Assert.That(emitted).IsEqualTo(0);
    }

    [Test]
    public async Task GetDirtyRows_OnlyDirtyRowsYielded()
    {
        using var buffer = new CellBuffer(10, 5);
        buffer.SwapBuffers(); // clear initial full-dirty state

        // Touch cells on rows 1 and 3 only.
        buffer.SetChar(0, 1, (uint)'A', Color.White, Color.Black);
        buffer.SetChar(5, 3, (uint)'B', Color.White, Color.Black);

        var rows = new List<int>();
        foreach (var y in buffer.GetDirtyRows())
        {
            rows.Add(y);
        }

        // Dirty rect bounds rows [1, 3] inclusive, so rows 1, 2, 3 are reported even if
        // row 2 has no writes — the renderer walks each row and skips empty cells anyway.
        await Assert.That(rows).Contains(1);
        await Assert.That(rows).Contains(3);
        await Assert.That(rows).DoesNotContain(0);
        await Assert.That(rows).DoesNotContain(4);
    }

    [Test]
    public async Task Renderer_ShapedSink_GroupsContiguousSameStyleCellsIntoOneRun()
    {
        var sink = new RecordingShapedSink();
        using var renderer = new Renderer(sink, 20, 1);

        // Write five cells with identical style. The renderer should emit one run with text "Hello".
        var fg = new Color(0xAB, 0xCD, 0xEF);
        var bg = new Color(0x12, 0x34, 0x56);
        var capture = new ManualCellWriter(b =>
        {
            b.SetChar(0, 0, (uint)'H', fg, bg);
            b.SetChar(1, 0, (uint)'e', fg, bg);
            b.SetChar(2, 0, (uint)'l', fg, bg);
            b.SetChar(3, 0, (uint)'l', fg, bg);
            b.SetChar(4, 0, (uint)'o', fg, bg);
        });

        renderer.Draw(capture);
        renderer.Present();

        await Assert.That(sink.Runs.Count).IsEqualTo(1);
        var run = sink.Runs[0];
        await Assert.That(run.X).IsEqualTo(0);
        await Assert.That(run.Y).IsEqualTo(0);
        await Assert.That(run.Text).IsEqualTo("Hello");
        await Assert.That(run.Fg).IsEqualTo(fg);
        await Assert.That(run.Bg).IsEqualTo(bg);
    }

    [Test]
    public async Task Renderer_ShapedSink_StyleBreakSplitsRuns()
    {
        var sink = new RecordingShapedSink();
        using var renderer = new Renderer(sink, 20, 1);

        var red = new Color(0xFF, 0x00, 0x00);
        var green = new Color(0x00, 0xFF, 0x00);

        var capture = new ManualCellWriter(b =>
        {
            b.SetChar(0, 0, (uint)'A', red, Color.Black);
            b.SetChar(1, 0, (uint)'B', red, Color.Black);
            b.SetChar(2, 0, (uint)'C', green, Color.Black); // style break here
            b.SetChar(3, 0, (uint)'D', green, Color.Black);
        });

        renderer.Draw(capture);
        renderer.Present();

        await Assert.That(sink.Runs.Count).IsEqualTo(2);
        await Assert.That(sink.Runs[0].Text).IsEqualTo("AB");
        await Assert.That(sink.Runs[0].Fg).IsEqualTo(red);
        await Assert.That(sink.Runs[1].Text).IsEqualTo("CD");
        await Assert.That(sink.Runs[1].Fg).IsEqualTo(green);
    }

    [Test]
    public async Task Renderer_ShapedSink_DefaultColoredEmptyCellsBreakRun()
    {
        var sink = new RecordingShapedSink();
        using var renderer = new Renderer(sink, 20, 1);

        // Write "Hi" then leave the rest of the row as default-colored empty cells (Cell.Empty).
        // The renderer should NOT extend the run through them — they're indistinguishable from
        // background and shaping them is wasted work.
        var capture = new ManualCellWriter(b =>
        {
            b.SetChar(0, 0, (uint)'H', Color.White, Color.Black);
            b.SetChar(1, 0, (uint)'i', Color.White, Color.Black);
        });

        renderer.Draw(capture);
        renderer.Present();

        await Assert.That(sink.Runs.Count).IsEqualTo(1);
        await Assert.That(sink.Runs[0].Text).IsEqualTo("Hi");
    }

    [Test]
    public async Task Renderer_ShapedSink_WideCharRendersAsSingleCodepointInRun()
    {
        var sink = new RecordingShapedSink();
        using var renderer = new Renderer(sink, 10, 1);

        var capture = new ManualCellWriter(b =>
        {
            // U+4E2D '中' is wide — occupies cells (0,0) WideLead + (1,0) WideTrail.
            b.SetChar(0, 0, 0x4E2Du, Color.White, Color.Black);
            // Then a narrow char at (2,0); same style so it should extend the run.
            b.SetChar(2, 0, (uint)'a', Color.White, Color.Black);
        });

        renderer.Draw(capture);
        renderer.Present();

        await Assert.That(sink.Runs.Count).IsEqualTo(1);
        var run = sink.Runs[0];
        // Text contains the wide codepoint followed by 'a' — the trail cell's empty placeholder
        // is correctly skipped during text reconstruction.
        await Assert.That(run.Text).IsEqualTo("中a");
    }

    [Test]
    public async Task Renderer_PlainSink_DoesNotCallWriteRun()
    {
        // AnsiWriter and MemoryCellSink only implement ICellSink, not IShapedRunSink. They
        // must go through the per-cell loop, not the row-level path.
        var memory = new MemoryCellSink();
        using var renderer = new Renderer(memory, 10, 1);

        var capture = new ManualCellWriter(b =>
        {
            b.SetChar(0, 0, (uint)'X', Color.White, Color.Black);
        });

        renderer.Draw(capture);
        renderer.Present();

        await Assert.That(memory.Writes.Count).IsGreaterThan(0);
    }

    private sealed class RecordingShapedSink : IShapedRunSink
    {
        public List<(int X, int Y, string Text, Color Fg, Color Bg, TextDecorations Deco)> Runs { get; } = [];
        public List<(int X, int Y)> CellWrites { get; } = [];

        public void BeginFrame() { }
        public void EndFrame() { }
        public void Reset() { }
        public void Resize(int width, int height) { _ = width; _ = height; }
        public void Dispose() { }
        public void WriteCell(int x, int y, Cell cell) { CellWrites.Add((x, y)); _ = cell; }

        public void WriteRun(int x, int y, ReadOnlySpan<char> text, Color fg, Color bg, TextDecorations decorations)
        {
            Runs.Add((x, y, text.ToString(), fg, bg, decorations));
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
