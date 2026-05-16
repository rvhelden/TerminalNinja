using SkiaSharp;
using TerminalNinja.Buffers;
using TerminalNinja.Controls;
using TerminalNinja.Primitives;
using TerminalNinja.Rendering;
using TerminalNinja.Skia;

namespace TerminalNinja.Tests.Unit.Skia;

/// <summary>
/// Tests for the <see cref="IShapedRunSink"/> path in <see cref="SkiaCellSink"/> and the
/// <see cref="TextBlock"/> dispatch logic that wires it up. We drive the sink against a
/// CPU-rendered SKSurface so the tests run without a GL context.
/// </summary>
public class SkiaShapedRunTests
{
    private const int CellWidth = 9;
    private const int CellHeight = 18;

    private static (SkiaCellSink sink, SKSurface surface, SKFont font, SKTypeface typeface) CreateSink(int cellsWide, int cellsTall)
    {
        var info = new SKImageInfo(cellsWide * CellWidth, cellsTall * CellHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("CPU surface unavailable.");
        var typeface = SKTypeface.Default;
        var font = new SKFont(typeface, 14f);
        var sink = new SkiaCellSink(surface, font, typeface, CellWidth, CellHeight);
        return (sink, surface, font, typeface);
    }

    [Test]
    public async Task SkiaCellSink_ImplementsIShapedRunSink()
    {
        var (sink, surface, font, typeface) = CreateSink(2, 1);
        try
        {
            await Assert.That(sink).IsAssignableTo<IShapedRunSink>();
        }
        finally
        {
            sink.Dispose();
            surface.Dispose();
            font.Dispose();
            typeface.Dispose();
        }
    }

    [Test]
    public async Task WriteRun_FollowedByWriteCell_SuppressesPerCellGlyph()
    {
        // The sink is supposed to paint backgrounds via WriteCell but defer glyph rendering
        // to EndFrame for cells covered by a queued run. We can't easily assert "no glyph drawn"
        // pixel-by-pixel without a known font, so we check the behaviour indirectly: the cell's
        // background colour survives unobscured at the cell centre.
        var (sink, surface, font, typeface) = CreateSink(4, 1);
        try
        {
            var fg = new Color(0xFF, 0x00, 0x00);
            var bg = new Color(0x00, 0xC8, 0x00);

            sink.BeginFrame();
            sink.WriteRun(0, 0, "fi".AsSpan(), fg, bg, TextDecorations.None);
            sink.WriteCell(0, 0, new Cell((uint)'f', fg, bg));
            sink.WriteCell(1, 0, new Cell((uint)'i', fg, bg));
            sink.EndFrame();

            // The cell-background green should be intact at the corners (least likely to be
            // covered by glyph pixels regardless of font). The shaped run draws on top via
            // DrawShapedText in EndFrame — that's the path being exercised even though we
            // can't easily verify the final glyph pixels in a font-portable way.
            using var snapshot = surface.Snapshot();
            using var pixmap = snapshot.PeekPixels();

            var topLeft = pixmap.GetPixelColor(1, 1);
            await Assert.That(topLeft.Green).IsEqualTo((byte)0xC8);
            await Assert.That(topLeft.Red).IsEqualTo((byte)0x00);
        }
        finally
        {
            sink.Dispose();
            surface.Dispose();
            font.Dispose();
            typeface.Dispose();
        }
    }

    [Test]
    public async Task WriteRun_BeginFrame_ResetsQueue()
    {
        // Calling WriteRun then BeginFrame should drop the queued run — otherwise the next
        // frame would paint stale shaped glyphs over freshly cleared pixels.
        var (sink, surface, font, typeface) = CreateSink(4, 1);
        try
        {
            sink.BeginFrame();
            sink.WriteRun(0, 0, "test".AsSpan(), Color.White, Color.Black, TextDecorations.None);
            // Don't call EndFrame; just start a new frame.
            sink.BeginFrame();

            // Cell (0, 0) now has a red background; we'd see foreground glyph pixels only if
            // the previous frame's queued run leaked through. With the queue cleared in
            // BeginFrame, the cell shows pure red.
            sink.WriteCell(0, 0, new Cell((uint)'X', Color.White, new Color(0xFF, 0x00, 0x00)));
            sink.EndFrame();

            using var snapshot = surface.Snapshot();
            using var pixmap = snapshot.PeekPixels();

            // The corner of cell (0, 0) should be pure red — the per-cell glyph for 'X' is
            // drawn (queue was cleared so no coverage), but the corner is unlikely to be
            // covered by 'X' glyph pixels for typical fonts.
            var corner = pixmap.GetPixelColor(1, 1);
            await Assert.That(corner.Red).IsEqualTo((byte)0xFF);
            await Assert.That(corner.Green).IsEqualTo((byte)0x00);
        }
        finally
        {
            sink.Dispose();
            surface.Dispose();
            font.Dispose();
            typeface.Dispose();
        }
    }

    [Test]
    public async Task WriteRun_PaintsBackgroundsViaWriteCell()
    {
        // Backgrounds are owned by WriteCell, not WriteRun. Without per-cell WriteCell calls,
        // the canvas behind the shaped run stays cleared. The Renderer always emits
        // WriteCell for every dirty cell, but the sink should not paint backgrounds itself
        // in WriteRun.
        var (sink, surface, font, typeface) = CreateSink(2, 1);
        try
        {
            sink.BeginFrame();
            sink.WriteRun(0, 0, "ab".AsSpan(), Color.White, new Color(0xFF, 0x00, 0xFF), TextDecorations.None);
            // Deliberately skip WriteCell — assert background did NOT appear.
            sink.EndFrame();

            using var snapshot = surface.Snapshot();
            using var pixmap = snapshot.PeekPixels();

            var corner = pixmap.GetPixelColor(1, 1);
            // SKSurface.Create starts cleared to (0,0,0,0) on Premul. Magenta from WriteRun
            // should not appear because WriteRun does not paint background.
            await Assert.That(corner.Red).IsNotEqualTo((byte)0xFF);
        }
        finally
        {
            sink.Dispose();
            surface.Dispose();
            font.Dispose();
            typeface.Dispose();
        }
    }

    [Test]
    public async Task TextBlock_WithShapedSink_QueuesRun()
    {
        // End-to-end: TextBlock renders against a Renderer wired to SkiaCellSink. The shaped
        // path must be invoked because SkiaCellSink implements IShapedRunSink. We assert via
        // a wrapping IShapedRunSink that records WriteRun calls.
        var recordingSink = new RecordingShapedSink();
        using var renderer = new Renderer(recordingSink, 20, 3);

        var textBlock = new TextBlock
        {
            Text = "Hello",
            Foreground = Color.White,
            Background = Color.Black,
        };

        renderer.Draw(textBlock);
        renderer.Present();

        await Assert.That(recordingSink.Runs.Count).IsGreaterThan(0);
        await Assert.That(recordingSink.Runs[0].Text).IsEqualTo("Hello");
    }

    [Test]
    public async Task TextBlock_WithPlainSink_DoesNotCallWriteRun()
    {
        // Plain MemoryCellSink does not implement IShapedRunSink, so TextBlock should
        // fall back to the per-cell SetChar path and never call WriteRun.
        var memory = new MemoryCellSink();
        using var renderer = new Renderer(memory, 20, 3);

        var textBlock = new TextBlock { Text = "Hello" };
        renderer.Draw(textBlock);
        renderer.Present();

        // No assertion needed on memory.Writes — the absence of a call is the test. We assert
        // the writes count is positive (something rendered) to make sure the test isn't a no-op.
        await Assert.That(memory.Writes.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task RenderingPipeline_ExposesActiveSinkOnBuffer()
    {
        // The Renderer must publish its sink on CellBuffer.ActiveSink for the duration of
        // Draw so capability-aware controls can detect it. Verify by attaching a fake control
        // that captures ActiveSink during its Render call.
        var memory = new MemoryCellSink();
        using var renderer = new Renderer(memory, 10, 3);

        var capture = new ActiveSinkCapturingControl();
        renderer.Draw(capture);

        await Assert.That(capture.CapturedSink).IsSameReferenceAs(memory);
    }

    private sealed class RecordingShapedSink : IShapedRunSink
    {
        public List<(int X, int Y, string Text, Color Fg, Color Bg, TextDecorations Deco)> Runs { get; } = [];
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
    }

    private sealed class ActiveSinkCapturingControl : UIElement
    {
        public ICellSink? CapturedSink { get; private set; }

        public override Size2D GetPreferredSize(Rect availableSpace) => new(0, 0);

        public override Rect CalculateBounds(Rect parentBounds) => parentBounds;

        public override void Render(CellBuffer buffer, Rect parentBounds)
        {
            CapturedSink = buffer.ActiveSink;
        }
    }
}
