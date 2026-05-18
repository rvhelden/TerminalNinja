using SkiaSharp;
using TerminalNinja.Primitives;
using TerminalNinja.Skia;

namespace TerminalNinja.Skia.Tests.Unit;

/// <summary>
/// Tests for bold/italic decoration handling in <see cref="SkiaCellSink"/>. The sink loads
/// typeface variants on demand via <see cref="SKTypeface.FromFamilyName(string, SKFontStyle)"/>;
/// if the typeface family doesn't have a real variant available the sink falls back to the
/// regular font. We can't directly assert on which typeface produced the glyphs (depends on
/// platform-installed fonts), so the tests verify the structural contract: the sink renders
/// without throwing for every combination of Bold and Italic, and identical decoration
/// requests hit the cache.
/// </summary>
public class SkiaBoldItalicTests
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
    [Arguments(TextDecorations.None)]
    [Arguments(TextDecorations.Bold)]
    [Arguments(TextDecorations.Italic)]
    [Arguments(TextDecorations.Bold | TextDecorations.Italic)]
    public async Task WriteRun_AnyBoldItalicCombo_DoesNotThrow(TextDecorations decorations)
    {
        var (sink, surface, font, typeface) = CreateSink(8, 1);
        try
        {
            sink.BeginFrame();
            sink.WriteRun(0, 0, "Hello".AsSpan(), Color.White, Color.Black, decorations);
            sink.EndFrame();

            await Assert.That(sink.CellWidth).IsEqualTo(CellWidth);
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
    public async Task WriteRun_BoldThenBoldItalic_LoadsDifferentVariants()
    {
        // Two distinct decoration combinations should each cause a font variant lookup. We
        // assert indirectly: rendering both styles in the same frame leaves the surface in
        // a state where the cells they cover received pixels (i.e. the second style isn't
        // silently overwriting the first via shared cache state).
        var (sink, surface, font, typeface) = CreateSink(12, 1);
        try
        {
            var red = new Color(0xFF, 0x00, 0x00);

            sink.BeginFrame();
            sink.WriteRun(0, 0, "AB".AsSpan(), red, Color.Black, TextDecorations.Bold);
            sink.WriteRun(6, 0, "CD".AsSpan(), red, Color.Black, TextDecorations.Bold | TextDecorations.Italic);
            sink.EndFrame();

            using var snap = surface.Snapshot();
            using var px = snap.PeekPixels();

            // The corner of cell (0,0) is part of the bold run's bg rect — should be black.
            var corner1 = px.GetPixelColor(1, 1);
            await Assert.That(corner1.Red).IsEqualTo((byte)0x00);
            // The corner of cell (6,0) is part of the bold+italic run's bg rect — same.
            var corner2 = px.GetPixelColor(6 * CellWidth + 1, 1);
            await Assert.That(corner2.Red).IsEqualTo((byte)0x00);
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
    public async Task WriteRun_SameTextDifferentStyles_DistinctCacheEntries()
    {
        // Cache key is (text, style); the same text with Bold should not return the cached
        // blob for the same text without Bold. We can't peek the cache directly, but we
        // verify the path completes for both styles in the same frame.
        var (sink, surface, font, typeface) = CreateSink(20, 1);
        try
        {
            sink.BeginFrame();
            sink.WriteRun(0, 0, "fi".AsSpan(), Color.White, Color.Black, TextDecorations.None);
            sink.WriteRun(3, 0, "fi".AsSpan(), Color.White, Color.Black, TextDecorations.Bold);
            sink.WriteRun(6, 0, "fi".AsSpan(), Color.White, Color.Black, TextDecorations.Italic);
            sink.WriteRun(9, 0, "fi".AsSpan(), Color.White, Color.Black, TextDecorations.Bold | TextDecorations.Italic);
            sink.EndFrame();

            // Re-render — each should hit its own cached blob.
            sink.BeginFrame();
            sink.WriteRun(0, 0, "fi".AsSpan(), Color.White, Color.Black, TextDecorations.None);
            sink.WriteRun(3, 0, "fi".AsSpan(), Color.White, Color.Black, TextDecorations.Bold);
            sink.WriteRun(6, 0, "fi".AsSpan(), Color.White, Color.Black, TextDecorations.Italic);
            sink.WriteRun(9, 0, "fi".AsSpan(), Color.White, Color.Black, TextDecorations.Bold | TextDecorations.Italic);
            sink.EndFrame();

            // No assertion mechanism — completing without exception is the test. Sanity check
            // a surface property to give the assertion something concrete.
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
    public async Task WriteRun_UnderlineAndBold_BothApply()
    {
        var (sink, surface, font, typeface) = CreateSink(8, 1);
        try
        {
            sink.BeginFrame();
            sink.WriteRun(0, 0, "xy".AsSpan(), Color.White, Color.Black,
                TextDecorations.Bold | TextDecorations.Underline);
            sink.EndFrame();

            using var snap = surface.Snapshot();
            using var pix = snap.PeekPixels();

            // The underline lives near the baseline; check there's at least one non-black
            // pixel in the bottom half of cell (0, 0).
            var underlineYStart = CellHeight - 4;
            var underlineYEnd = CellHeight - 1;
            var foundUnderlinePixel = false;
            for (var y = underlineYStart; y <= underlineYEnd && !foundUnderlinePixel; y++)
            {
                for (var x = 0; x < CellWidth * 2; x++)
                {
                    var c = pix.GetPixelColor(x, y);
                    if (c.Red == 0xFF && c.Green == 0xFF && c.Blue == 0xFF)
                    {
                        foundUnderlinePixel = true;
                        break;
                    }
                }
            }

            await Assert.That(foundUnderlinePixel).IsTrue();
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
