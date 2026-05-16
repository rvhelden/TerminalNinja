using SkiaSharp;
using TerminalNinja.Primitives;
using TerminalNinja.Skia;

namespace TerminalNinja.Tests.Unit.Skia;

/// <summary>
/// Tests for <see cref="SkiaCellSink"/> that drive it against a CPU-rendered SKSurface
/// (no GL context required). We assert two things: the sink completes the operations
/// without throwing, and the pixel buffer ends up with the expected coarse color pattern.
/// Glyph-level rendering (anti-aliasing, font metrics) is intentionally NOT asserted —
/// it would couple tests to platform font availability.
/// </summary>
public class SkiaCellSinkTests
{
    private const int CellWidth = 9;
    private const int CellHeight = 18;

    private static SKSurface CreateCpuSurface(int cellsWide, int cellsTall)
    {
        var info = new SKImageInfo(cellsWide * CellWidth, cellsTall * CellHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        return SKSurface.Create(info)
            ?? throw new InvalidOperationException("Failed to create CPU SKSurface for test.");
    }

    private static (SkiaCellSink sink, SKSurface surface, SKFont font, SKTypeface typeface) CreateSink(int cellsWide, int cellsTall)
    {
        var surface = CreateCpuSurface(cellsWide, cellsTall);
        var typeface = SKTypeface.Default;
        var font = new SKFont(typeface, size: 14f);
        var sink = new SkiaCellSink(surface, font, CellWidth, CellHeight);
        return (sink, surface, font, typeface);
    }

    [Test]
    public async Task Sink_ReportsMetricsFromConstructor()
    {
        var (sink, surface, font, typeface) = CreateSink(4, 2);
        try
        {
            sink.BeginFrame();
            sink.EndFrame();

            // The sink exposes the cell metrics it was constructed with so the host can
            // map pixel coordinates back to cell coordinates (e.g. for mouse hit-testing).
            await Assert.That(sink.CellWidth).IsEqualTo(CellWidth);
            await Assert.That(sink.CellHeight).IsEqualTo(CellHeight);
            await Assert.That(sink.Surface).IsSameReferenceAs(surface);
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
    public async Task WriteCell_PaintsBackgroundRectInCellCoords()
    {
        var (sink, surface, font, typeface) = CreateSink(4, 2);
        try
        {
            var red = new Color(0xFF, 0x00, 0x00);
            sink.BeginFrame();
            sink.WriteCell(1, 0, new Cell((uint)' ', Color.White, red));
            sink.EndFrame();

            using var snapshot = surface.Snapshot();
            using var pixmap = snapshot.PeekPixels();

            // Sample the centre pixel of cell (1, 0). It should be the red background.
            var centerX = CellWidth + (CellWidth / 2);
            var centerY = CellHeight / 2;
            var pixel = pixmap.GetPixelColor(centerX, centerY);

            await Assert.That(pixel.Red).IsEqualTo((byte)0xFF);
            await Assert.That(pixel.Green).IsEqualTo((byte)0x00);
            await Assert.That(pixel.Blue).IsEqualTo((byte)0x00);
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
    public async Task WriteCell_WideTrail_IsSkipped()
    {
        var (sink, surface, font, typeface) = CreateSink(4, 1);
        try
        {
            // Paint cell (0, 0) red as a baseline.
            sink.BeginFrame();
            sink.WriteCell(0, 0, new Cell((uint)' ', Color.White, new Color(0xFF, 0x00, 0x00)));

            // Now write a WideTrail cell at (1, 0) claiming to be blue — the sink should ignore
            // it because the WideLead at (0, 0) is responsible for both cells. Pixel at (1, 0)
            // should therefore stay whatever was last drawn there (default cleared SKSurface = black).
            var trailCell = new Cell(0u, Color.White, new Color(0x00, 0x00, 0xFF), TextDecorations.None, CellFlags.WideTrail);
            sink.WriteCell(1, 0, trailCell);
            sink.EndFrame();

            using var snapshot = surface.Snapshot();
            using var pixmap = snapshot.PeekPixels();

            // The trail cell's intended location should NOT be blue.
            var centerX = CellWidth + (CellWidth / 2);
            var centerY = CellHeight / 2;
            var pixel = pixmap.GetPixelColor(centerX, centerY);

            await Assert.That(pixel.Blue).IsNotEqualTo((byte)0xFF);
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
    public async Task WriteCell_WideLead_PaintsTwoCellWideBackground()
    {
        var (sink, surface, font, typeface) = CreateSink(4, 1);
        try
        {
            var green = new Color(0x00, 0xFF, 0x00);
            sink.BeginFrame();

            // Wide lead at (0, 0) — should paint a 2*CellWidth pixel-wide background.
            // Use a non-wide codepoint with the WideLead flag to keep the test about the
            // sink's behavior, not WidthTable's classification.
            var leadCell = new Cell((uint)'X', Color.White, green, TextDecorations.None, CellFlags.WideLead);
            sink.WriteCell(0, 0, leadCell);
            sink.EndFrame();

            using var snapshot = surface.Snapshot();
            using var pixmap = snapshot.PeekPixels();

            // Pixel inside cell (1, 0) should also be green because the lead's rect spans two cells.
            var centerX = CellWidth + (CellWidth / 2);
            var centerY = CellHeight / 2;
            var pixel = pixmap.GetPixelColor(centerX, centerY);

            await Assert.That(pixel.Green).IsEqualTo((byte)0xFF);
            await Assert.That(pixel.Red).IsEqualTo((byte)0x00);
            await Assert.That(pixel.Blue).IsEqualTo((byte)0x00);
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
    public async Task Inverse_SwapsForegroundAndBackground()
    {
        var (sink, surface, font, typeface) = CreateSink(2, 1);
        try
        {
            var fg = new Color(0xFF, 0x00, 0x00);
            var bg = new Color(0x00, 0xFF, 0x00);
            sink.BeginFrame();
            sink.WriteCell(0, 0, new Cell((uint)' ', fg, bg, TextDecorations.Inverse));
            sink.EndFrame();

            using var snapshot = surface.Snapshot();
            using var pixmap = snapshot.PeekPixels();

            var pixel = pixmap.GetPixelColor(CellWidth / 2, CellHeight / 2);
            // Inverse swaps; the painted background should now be the original foreground (red).
            await Assert.That(pixel.Red).IsEqualTo((byte)0xFF);
            await Assert.That(pixel.Green).IsEqualTo((byte)0x00);
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
    public async Task RendererIntegration_DrawsControlTree()
    {
        // End-to-end: SkiaCellSink wired into Renderer, given a Border, no exceptions, pixels paint.
        var (sink, surface, font, typeface) = CreateSink(20, 5);
        try
        {
            using var renderer = new TerminalNinja.Rendering.Renderer(sink, 20, 5);
            var border = new TerminalNinja.Controls.Border
            {
                Background = new Color(0x00, 0x80, 0xFF),
            };

            renderer.Draw(border);
            renderer.Present();

            using var snapshot = surface.Snapshot();
            using var pixmap = snapshot.PeekPixels();

            // Border with Background fills the whole 20x5 cell area with the colour.
            var pixel = pixmap.GetPixelColor(CellWidth * 5, CellHeight * 2);
            await Assert.That(pixel.Blue).IsEqualTo((byte)0xFF);
            await Assert.That(pixel.Green).IsEqualTo((byte)0x80);
        }
        finally
        {
            sink.Dispose();
            surface.Dispose();
            font.Dispose();
            typeface.Dispose();
        }
    }
}
