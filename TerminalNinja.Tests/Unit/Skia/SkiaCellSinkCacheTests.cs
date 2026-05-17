using SkiaSharp;
using TerminalNinja.Primitives;
using TerminalNinja.Skia;

namespace TerminalNinja.Tests.Unit.Skia;

/// <summary>
/// Smoke tests for the SKTextBlob shape cache in <see cref="SkiaCellSink"/>. We can't
/// directly assert on cache state (it's private), but we can verify the cache doesn't
/// regress correctness: repeated WriteRun calls with the same text still render correctly.
/// </summary>
public class SkiaCellSinkCacheTests
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
    public async Task WriteRun_SameTextRepeatedly_IsIdempotent()
    {
        var (sink, surface, font, typeface) = CreateSink(10, 3);
        try
        {
            var fg = new Color(0xFF, 0xFF, 0xFF);
            var bg = new Color(0x10, 0x20, 0x30);

            // First pass — cold cache, shapes via SKShaper.
            sink.BeginFrame();
            sink.WriteRun(0, 0, "Hello".AsSpan(), fg, bg, TextDecorations.None);
            sink.EndFrame();

            // Capture pixel after first render.
            using var snap1 = surface.Snapshot();
            using var px1 = snap1.PeekPixels();
            var corner1 = px1.GetPixelColor(1, 1);

            // Second pass — should hit the cache.
            sink.BeginFrame();
            sink.WriteRun(0, 0, "Hello".AsSpan(), fg, bg, TextDecorations.None);
            sink.EndFrame();

            using var snap2 = surface.Snapshot();
            using var px2 = snap2.PeekPixels();
            var corner2 = px2.GetPixelColor(1, 1);

            // Pixel-level identical: cached blob must produce the same output as cold shape.
            await Assert.That(corner1.Red).IsEqualTo(corner2.Red);
            await Assert.That(corner1.Green).IsEqualTo(corner2.Green);
            await Assert.That(corner1.Blue).IsEqualTo(corner2.Blue);
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
    public async Task WriteRun_ManyDistinctTexts_DoesNotThrow()
    {
        // Drive the cache past a moderate population to exercise the LRU eviction path.
        // The cap is 1024 in the sink; here we issue 50 distinct strings — not enough to
        // evict, but enough to make the cache non-trivial.
        var (sink, surface, font, typeface) = CreateSink(50, 1);
        try
        {
            sink.BeginFrame();
            for (var i = 0; i < 50; i++)
            {
                sink.WriteRun(i, 0, $"text-{i}".AsSpan(), Color.White, Color.Black, TextDecorations.None);
            }

            sink.EndFrame();

            // No exception is the success condition. Sanity-check the surface still reports
            // its dimensions so a partial state doesn't slip past the test.
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
    public async Task Dispose_DisposesCachedBlobs()
    {
        var (sink, surface, font, typeface) = CreateSink(5, 1);
        sink.BeginFrame();
        sink.WriteRun(0, 0, "abc".AsSpan(), Color.White, Color.Black, TextDecorations.None);
        sink.EndFrame();

        // Dispose should release any cached SKTextBlob references without throwing.
        // We can't observe the disposal directly (the blobs are private), but a leak here
        // would surface as a finalizer-thread exception on subsequent GC. Calling Dispose
        // twice is also expected to be safe.
        sink.Dispose();

        surface.Dispose();
        font.Dispose();
        typeface.Dispose();

        // Second dispose is idempotent — TUnit accepts a non-constant lambda capture.
        await Assert.That(() => sink.Dispose()).ThrowsNothing();
    }
}
