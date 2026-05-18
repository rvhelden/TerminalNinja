using System.IO;
using SkiaSharp;

namespace TerminalNinja.Shell.Skia.Branding;

/// <summary>
/// Pure-SkiaSharp renderer for the NinjaShell application icon. Used both for the SDL3
/// window icon set on startup and to bake the static PNG bundled with the VS Code
/// extension (via the host's <c>--write-icon</c> CLI switch). No SDL / GL dependency —
/// runs anywhere SkiaSharp loads, so the same routine produces the runtime icon and the
/// committed PNG without any visual drift.
/// </summary>
internal static class IconRenderer
{
    // Palette mirrors TerminalNinja's Dark theme so the icon reads as part of the same
    // surface family: BG = ThemeBackgroundColor, chevron = ThemeAccentSecondaryColor
    // (yellow pops harder than the blue accent at small icon sizes against dark taskbars),
    // cursor block = ThemeAccentColor.
    private static readonly SKColor Background = new(0x1E, 0x1E, 0x1E);
    private static readonly SKColor Border = new(0x56, 0x9C, 0xD6);
    private static readonly SKColor Chevron = new(0xDC, 0xDC, 0xAA);
    private static readonly SKColor Cursor = new(0x56, 0x9C, 0xD6);

    /// <summary>
    /// Renders the icon into a fresh RGBA8888-premul <see cref="SKBitmap"/> of the given pixel
    /// size. Premul + RGBA8888 in memory is also what we hand to SDL via
    /// <c>SDL_PIXELFORMAT_ABGR8888</c> (same byte order on little-endian platforms — which
    /// is the only architecture family the host targets). Caller owns the bitmap.
    /// </summary>
    public static SKBitmap Render(int size)
    {
        var info = new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        Draw(canvas, size);
        canvas.Flush();
        return bitmap;
    }

    /// <summary>Renders the icon and writes it as a PNG to <paramref name="path"/>.</summary>
    public static void WritePng(string path, int size)
    {
        using var bitmap = Render(size);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    private static void Draw(SKCanvas canvas, int size)
    {
        var f = (float)size;
        var corner = f * 0.18f;
        var bounds = new SKRect(0, 0, f, f);

        // Rounded-square background — matches the chrome of the shell's surface panels.
        using (var paint = new SKPaint
        {
            Color = Background,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        })
        {
            canvas.DrawRoundRect(bounds, corner, corner, paint);
        }

        // Subtle inner accent ring. StrokeWidth scales with size so the border doesn't
        // disappear at 16px or look heavy-handed at 256px. The half-stroke inset keeps the
        // stroke inside the rounded background (DrawRoundRect strokes are centered on the
        // path so without an inset the outer half would clip off the bitmap edge).
        using (var paint = new SKPaint
        {
            Color = Border,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = MathF.Max(1f, f * 0.04f),
        })
        {
            var inset = paint.StrokeWidth / 2f;
            var ringRect = new SKRect(inset, inset, f - inset, f - inset);
            canvas.DrawRoundRect(ringRect, corner - inset, corner - inset, paint);
        }

        // Prompt chevron `>` — two strokes meeting at a sharp point on the right. Rounded
        // caps + miter join give a clean "katana-tip" silhouette without micro-aliasing the
        // outer corners at small sizes. Centered slightly left of the midline to leave
        // breathing room for the cursor block.
        var cx = f * 0.42f;
        var cy = f * 0.5f;
        var arm = f * 0.20f;
        using (var paint = new SKPaint
        {
            Color = Chevron,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = MathF.Max(2f, f * 0.11f),
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Miter,
            StrokeMiter = 8f,
        })
        using (var path = new SKPath())
        {
            path.MoveTo(cx - arm, cy - arm);
            path.LineTo(cx + arm, cy);
            path.LineTo(cx - arm, cy + arm);
            canvas.DrawPath(path, paint);
        }

        // Cursor block — square with a hint of rounding. Sits just past the chevron's tip,
        // vertically centered with it. Reads as a blinking-prompt cursor.
        using (var paint = new SKPaint
        {
            Color = Cursor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        })
        {
            var blockSize = f * 0.16f;
            var blockLeft = cx + arm + f * 0.06f;
            var blockTop = cy - blockSize / 2f;
            var blockRadius = MathF.Max(1f, blockSize * 0.20f);
            var blockRect = new SKRect(blockLeft, blockTop, blockLeft + blockSize, blockTop + blockSize);
            canvas.DrawRoundRect(blockRect, blockRadius, blockRadius, paint);
        }
    }
}
