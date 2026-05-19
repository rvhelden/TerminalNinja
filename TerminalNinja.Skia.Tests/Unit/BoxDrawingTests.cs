using SkiaSharp;
using TerminalNinja.Primitives;
using TerminalNinja.Skia;

namespace TerminalNinja.Skia.Tests.Unit;

/// <summary>
/// Tests for the procedural box-drawing path in <see cref="SkiaCellSink"/>. Box-drawing
/// glyphs from a monospace font don't fill their advance width, so an N-cell horizontal
/// border rendered as font glyphs tiles as a dashed line. The sink intercepts box-drawing
/// codepoints (U+2500..U+257F subset) and renders them with Skia primitives that span the
/// entire cell. These tests verify the resulting pixel bands are continuous across cell
/// boundaries.
/// </summary>
public class BoxDrawingTests
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
    public async Task WriteRun_HorizontalBoxDrawing_HasNoGapsBetweenCells()
    {
        // Render three contiguous ─ cells. Sample every pixel along the midline — every
        // sample must be the foreground colour (white). With the old font-glyph path the
        // pixels between cells were the background colour, producing the dashed look the
        // user reported.
        var (sink, surface, font, typeface) = CreateSink(3, 1);
        try
        {
            sink.BeginFrame();
            sink.WriteRun(0, 0, "───".AsSpan(), Color.White, Color.Black, TextDecorations.None);
            sink.EndFrame();

            using var snapshot = surface.Snapshot();
            using var pixmap = snapshot.PeekPixels();

            // The stroke is single (~ 1 px). Walk the row that contains the stroke and verify
            // none of the 3 cells × CellWidth pixels are background. We allow the test to find
            // the stroke row by scanning all rows and picking the first that is fully painted.
            var totalWidth = 3 * CellWidth;
            var foundStrokeRow = false;
            for (var row = 0; row < CellHeight; row++)
            {
                var allForeground = true;
                for (var col = 0; col < totalWidth; col++)
                {
                    var p = pixmap.GetPixelColor(col, row);
                    if (p.Red != 0xFF || p.Green != 0xFF || p.Blue != 0xFF)
                    {
                        allForeground = false;
                        break;
                    }
                }

                if (allForeground)
                {
                    foundStrokeRow = true;
                    break;
                }
            }

            await Assert.That(foundStrokeRow).IsTrue();
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
    public async Task WriteRun_VerticalBoxDrawing_HasNoGapsBetweenCells()
    {
        // Stack two │ cells vertically — same invariant on the column axis. Find the column
        // containing the stroke and assert every pixel down it (across both cells) is fg.
        var (sink, surface, font, typeface) = CreateSink(1, 2);
        try
        {
            sink.BeginFrame();
            sink.WriteRun(0, 0, "│".AsSpan(), Color.White, Color.Black, TextDecorations.None);
            sink.WriteRun(0, 1, "│".AsSpan(), Color.White, Color.Black, TextDecorations.None);
            sink.EndFrame();

            using var snapshot = surface.Snapshot();
            using var pixmap = snapshot.PeekPixels();

            var totalHeight = 2 * CellHeight;
            var foundStrokeCol = false;
            for (var col = 0; col < CellWidth; col++)
            {
                var allForeground = true;
                for (var row = 0; row < totalHeight; row++)
                {
                    var p = pixmap.GetPixelColor(col, row);
                    if (p.Red != 0xFF || p.Green != 0xFF || p.Blue != 0xFF)
                    {
                        allForeground = false;
                        break;
                    }
                }

                if (allForeground)
                {
                    foundStrokeCol = true;
                    break;
                }
            }

            await Assert.That(foundStrokeCol).IsTrue();
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
    public async Task WriteCell_BoxDrawing_PaintsForegroundAtMidline()
    {
        // WriteCell is the single-cell path used by tests and any caller that bypasses the
        // shaped run dispatch. It must hit the same procedural drawer, so a ─ painted via
        // WriteCell shows fg pixels at the cell's midline.
        var (sink, surface, font, typeface) = CreateSink(1, 1);
        try
        {
            sink.BeginFrame();
            sink.WriteCell(0, 0, new Cell(0x2500, Color.White, Color.Black));
            sink.EndFrame();

            using var snapshot = surface.Snapshot();
            using var pixmap = snapshot.PeekPixels();

            // Find any white pixel on the midline of cell (0, 0). The procedural stroke is
            // centred on floor(CellHeight / 2) so we sample exactly that row.
            var midY = CellHeight / 2;
            var anyForeground = false;
            for (var col = 0; col < CellWidth; col++)
            {
                var p = pixmap.GetPixelColor(col, midY);
                if (p.Red == 0xFF && p.Green == 0xFF && p.Blue == 0xFF)
                {
                    anyForeground = true;
                    break;
                }
            }

            await Assert.That(anyForeground).IsTrue();
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
    public async Task WriteRun_CornerThenDashes_FormsContinuousTopEdge()
    {
        // Mirror an actual Border top edge: ┌─── starting at column 0. Sample the midline
        // across the corner and the dashes — every pixel must be fg. This is the strongest
        // visual regression: the user's complaint was that "─" tiles look dashed; the corner
        // joint with the dash next to it is the exact place where the gap shows up first.
        var (sink, surface, font, typeface) = CreateSink(4, 1);
        try
        {
            sink.BeginFrame();
            sink.WriteRun(0, 0, "┌───".AsSpan(), Color.White, Color.Black, TextDecorations.None);
            sink.EndFrame();

            using var snapshot = surface.Snapshot();
            using var pixmap = snapshot.PeekPixels();

            // The corner has its horizontal stroke spanning [midX, right], and the dashes
            // span [0, cellWidth] each. So every pixel from cell-0 midX to the right edge
            // of cell 3 must be fg at the stroke row.
            var totalWidth = 4 * CellWidth;
            var midX = CellWidth / 2;
            var foundStrokeRow = false;
            for (var row = 0; row < CellHeight; row++)
            {
                var allForeground = true;
                for (var col = midX; col < totalWidth; col++)
                {
                    var p = pixmap.GetPixelColor(col, row);
                    if (p.Red != 0xFF || p.Green != 0xFF || p.Blue != 0xFF)
                    {
                        allForeground = false;
                        break;
                    }
                }

                if (allForeground)
                {
                    foundStrokeRow = true;
                    break;
                }
            }

            await Assert.That(foundStrokeRow).IsTrue();
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
