using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Renders pixel data as half-block characters in the terminal.
/// Each terminal cell represents 2 vertical pixels using Unicode half-block characters
/// (▀ U+2580), giving 2x vertical resolution. An 80x24 terminal can display 80x48 pixels.
/// <para>
/// Set <see cref="Source"/> to a <c>Color[,]</c> array (width x height) of pixel colors.
/// Use <see cref="Stretch"/> to control how the image is scaled to fit available bounds.
/// </para>
/// </summary>
[RuntimeNameProperty("Name")]
public sealed class Image : FrameworkElement
{
    public Image()
    {
        DefaultStyleKey = typeof(Image);
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source), typeof(Color[,]), typeof(Image),
            new FrameworkPropertyMetadata((object?)null, affectsRender: true));

    public static readonly DependencyProperty StretchProperty =
        DependencyProperty.Register(nameof(Stretch), typeof(Stretch), typeof(Image),
            new FrameworkPropertyMetadata(Stretch.Uniform, affectsRender: true));

    public static readonly DependencyProperty WidthProperty =
        DependencyProperty.Register(nameof(Width), typeof(Size), typeof(Image),
            new FrameworkPropertyMetadata(Size.Auto, affectsRender: true));

    public static readonly DependencyProperty HeightProperty =
        DependencyProperty.Register(nameof(Height), typeof(Size), typeof(Image),
            new FrameworkPropertyMetadata(Size.Auto, affectsRender: true));

    /// <summary>Gets or sets the pixel data as a Color[width, height] array.</summary>
    public Color[,]? Source
    {
        get => (Color[,]?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>Gets or sets how the image is scaled to fit available bounds.</summary>
    public Stretch Stretch
    {
        get => (Stretch)GetValue(StretchProperty)!;
        set => SetValue(StretchProperty, value);
    }

    /// <summary>Gets or sets the control width.</summary>
    public Size Width
    {
        get => (Size)GetValue(WidthProperty)!;
        set => SetValue(WidthProperty, value);
    }

    /// <summary>Gets or sets the control height.</summary>
    public Size Height
    {
        get => (Size)GetValue(HeightProperty)!;
        set => SetValue(HeightProperty, value);
    }

    // ─── Layout ──────────────────────────────────────────────────────

    /// <summary>Gets the source image width in pixels, or 0 if no source.</summary>
    private int SourceWidth => Source?.GetLength(0) ?? 0;

    /// <summary>Gets the source image height in pixels, or 0 if no source.</summary>
    private int SourceHeight => Source?.GetLength(1) ?? 0;

    /// <summary>Gets the source height in terminal cells (2 pixels per cell).</summary>
    private int SourceCellHeight => (SourceHeight + 1) / 2;

    public override Size2D GetPreferredSize(Rect parent)
    {
        var w = Width.Mode == SizeMode.Absolute ? Width.Resolve(parent.Width) : SourceWidth;
        var h = Height.Mode == SizeMode.Absolute ? Height.Resolve(parent.Height) : SourceCellHeight;
        return new Size2D(Math.Max(w, 1), Math.Max(h, 1));
    }

    public override Rect CalculateBounds(Rect parent)
    {
        var preferred = GetPreferredSize(parent);
        var w = Width.Mode == SizeMode.Auto ? preferred.Width : Width.Resolve(parent.Width);
        var h = Height.Mode == SizeMode.Auto ? preferred.Height : Height.Resolve(parent.Height);
        return ApplyAlignment(parent, w, h);
    }

    // ─── Rendering ───────────────────────────────────────────────────

    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        var clipped = bounds.Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0) return;

        var source = Source;
        if (source == null || SourceWidth == 0 || SourceHeight == 0) return;

        var srcW = SourceWidth;
        var srcH = SourceHeight;
        var dstW = bounds.Width;
        var dstH = bounds.Height * 2; // 2 pixels per cell row

        // Calculate scaled dimensions based on Stretch mode
        int renderW, renderH, offsetX, offsetY;
        CalculateStretch(srcW, srcH, dstW, dstH, out renderW, out renderH, out offsetX, out offsetY);

        for (var cellY = 0; cellY < bounds.Height; cellY++)
        {
            var screenY = bounds.Y + cellY;
            if (screenY < 0 || screenY >= buffer.Height) continue;

            for (var cellX = 0; cellX < bounds.Width; cellX++)
            {
                var screenX = bounds.X + cellX;
                if (screenX < 0 || screenX >= buffer.Width) continue;

                var topColor = SamplePixel(source, srcW, srcH, cellX, cellY * 2, renderW, renderH, offsetX, offsetY);
                var bottomColor = SamplePixel(source, srcW, srcH, cellX, cellY * 2 + 1, renderW, renderH, offsetX, offsetY);

                if (topColor.IsTransparent && bottomColor.IsTransparent)
                    continue;

                if (topColor == bottomColor)
                {
                    buffer.SetChar(screenX, screenY, '\u2588', topColor, topColor); // █
                }
                else if (bottomColor.IsTransparent)
                {
                    buffer.SetChar(screenX, screenY, '\u2580', topColor, Color.Transparent); // ▀
                }
                else if (topColor.IsTransparent)
                {
                    buffer.SetChar(screenX, screenY, '\u2584', bottomColor, Color.Transparent); // ▄
                }
                else
                {
                    buffer.SetChar(screenX, screenY, '\u2580', topColor, bottomColor); // ▀ with fg=top, bg=bottom
                }
            }
        }
    }

    // ─── Stretch Calculation ─────────────────────────────────────────

    private void CalculateStretch(int srcW, int srcH, int dstW, int dstH,
        out int renderW, out int renderH, out int offsetX, out int offsetY)
    {
        offsetX = 0;
        offsetY = 0;

        switch (Stretch)
        {
            case Stretch.None:
                renderW = srcW;
                renderH = srcH;
                break;

            case Stretch.Fill:
                renderW = dstW;
                renderH = dstH;
                break;

            case Stretch.Uniform:
            {
                var scaleX = (double)dstW / srcW;
                var scaleY = (double)dstH / srcH;
                var scale = Math.Min(scaleX, scaleY);
                renderW = (int)(srcW * scale);
                renderH = (int)(srcH * scale);
                offsetX = (dstW - renderW) / 2;
                offsetY = (dstH - renderH) / 2;
                break;
            }

            case Stretch.UniformToFill:
            {
                var scaleX = (double)dstW / srcW;
                var scaleY = (double)dstH / srcH;
                var scale = Math.Max(scaleX, scaleY);
                renderW = (int)(srcW * scale);
                renderH = (int)(srcH * scale);
                offsetX = (dstW - renderW) / 2; // may be negative (cropping)
                offsetY = (dstH - renderH) / 2;
                break;
            }

            default:
                renderW = dstW;
                renderH = dstH;
                break;
        }
    }

    /// <summary>
    /// Samples a pixel from the source image, mapping destination coordinates
    /// back to source coordinates based on the render dimensions and offset.
    /// </summary>
    private static Color SamplePixel(Color[,] source, int srcW, int srcH,
        int destX, int destY, int renderW, int renderH, int offsetX, int offsetY)
    {
        // Map destination pixel to the scaled image space
        var scaledX = destX - offsetX;
        var scaledY = destY - offsetY;

        // Outside the rendered image area
        if (scaledX < 0 || scaledX >= renderW || scaledY < 0 || scaledY >= renderH)
            return Color.Transparent;

        // Map to source pixel
        var srcX = srcW > 0 && renderW > 0 ? scaledX * srcW / renderW : 0;
        var srcY = srcH > 0 && renderH > 0 ? scaledY * srcH / renderH : 0;

        srcX = Math.Clamp(srcX, 0, srcW - 1);
        srcY = Math.Clamp(srcY, 0, srcH - 1);

        return source[srcX, srcY];
    }
}
