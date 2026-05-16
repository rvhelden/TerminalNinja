using System.Text;
using SkiaSharp;
using SkiaSharp.HarfBuzz;
using TerminalNinja.Primitives;
using TerminalNinja.Rendering;

namespace TerminalNinja.Skia;

/// <summary>
/// <see cref="ICellSink"/> backed by SkiaSharp, with HarfBuzz shaping support via
/// <see cref="IShapedRunSink"/>. Per-cell <see cref="WriteCell"/> paints background
/// rectangles, decoration primitives, and (for cells not covered by a queued shaped run)
/// the single-glyph fallback. Cells covered by a queued run get only the background;
/// their glyphs are emitted during <see cref="EndFrame"/> via <see cref="SKShaper"/>.
/// </summary>
/// <remarks>
/// <para>
/// Threading: not thread-safe. All calls must come from the thread that owns the GL context
/// (or, for software-rendered surfaces, the thread driving the host).
/// </para>
/// <para>
/// State: the sink caches the surface, font, typeface, shaper, and cell metrics — it does
/// NOT own the surface. The host (<see cref="SkiaApplication"/>) replaces the surface every
/// frame because the default framebuffer can be resized; <see cref="SetSurface"/> rotates
/// it without rebuilding the sink. Reusable <see cref="SKPaint"/> instances and a queue
/// for shaped runs live in the sink to keep the hot path allocation-free.
/// </para>
/// </remarks>
public sealed class SkiaCellSink : IShapedRunSink
{
    private SKSurface _surface;
    private readonly SKTypeface _typeface;
    private readonly SKFont _font;
    private readonly SKShaper _shaper;
    private readonly int _cellWidth;
    private readonly int _cellHeight;
    private readonly float _textBaseline;

    private readonly SKPaint _bgPaint = new() { IsAntialias = false, Style = SKPaintStyle.Fill };
    private readonly SKPaint _fgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _linePaint = new() { IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };

    // Queued runs accumulate during the frame and are drawn in EndFrame. The coverage
    // map records which (column, row) cells fall under a queued run so WriteCell can
    // suppress the per-cell glyph fallback for them (the shaped pass owns those glyphs).
    private readonly List<QueuedRun> _queuedRuns = [];
    private readonly Dictionary<int, HashSet<int>> _coveredColumnsByRow = [];

    /// <summary>Width of a single cell in pixels.</summary>
    public int CellWidth => _cellWidth;

    /// <summary>Height of a single cell in pixels.</summary>
    public int CellHeight => _cellHeight;

    /// <summary>Currently bound surface. Replaced via <see cref="SetSurface"/> on resize.</summary>
    public SKSurface Surface => _surface;

    /// <summary>
    /// Creates a sink that paints into <paramref name="surface"/> using <paramref name="font"/>
    /// at the given fixed cell metrics. <paramref name="typeface"/> is used to construct the
    /// HarfBuzz shaper; pass <see cref="SKFont.Typeface"/> when in doubt. The host is
    /// responsible for picking a font that renders at the given pixel size and for keeping
    /// the surface alive at least until the next <see cref="SetSurface"/> or <see cref="Dispose"/>.
    /// </summary>
    public SkiaCellSink(SKSurface surface, SKFont font, SKTypeface typeface, int cellWidth, int cellHeight)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(typeface);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellHeight);

        _surface = surface;
        _font = font;
        _typeface = typeface;
        _shaper = new SKShaper(typeface);
        _cellWidth = cellWidth;
        _cellHeight = cellHeight;

        // Baseline offset from the top of the cell. Skia's DrawText takes baseline-relative
        // y; for a monospace font we approximate as: ascent + a small inset for descenders.
        var metrics = font.Metrics;
        var ascent = -metrics.Ascent; // ascent is reported negative (above baseline)
        var descent = metrics.Descent;
        var lineHeight = ascent + descent;
        var lineGap = Math.Max(0f, _cellHeight - lineHeight);
        _textBaseline = ascent + (lineGap / 2f);
    }

    /// <summary>
    /// Back-compat overload that derives the typeface from <paramref name="font"/>. Falls
    /// back to <see cref="SKTypeface.Default"/> if the font has none attached.
    /// </summary>
    public SkiaCellSink(SKSurface surface, SKFont font, int cellWidth, int cellHeight)
        : this(surface, font, font.Typeface ?? SKTypeface.Default, cellWidth, cellHeight)
    {
    }

    /// <summary>
    /// Replaces the active surface (used by the host after a window resize). Discards
    /// any pending Skia state in the new surface's canvas — callers should call
    /// <see cref="BeginFrame"/> on the next frame to re-initialize state.
    /// </summary>
    public void SetSurface(SKSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        _surface = surface;
    }

    /// <inheritdoc />
    public void BeginFrame()
    {
        // Reset per-frame state — any runs queued from a previous frame would otherwise
        // paint over the freshly cleared canvas.
        _queuedRuns.Clear();
        _coveredColumnsByRow.Clear();
    }

    /// <inheritdoc />
    public void WriteRun(int x, int y, ReadOnlySpan<char> text, Color fg, Color bg, TextDecorations decorations)
    {
        if (text.IsEmpty)
        {
            return;
        }

        var widthCells = MeasureRunCellWidth(text);
        if (widthCells <= 0)
        {
            return;
        }

        _queuedRuns.Add(new QueuedRun(x, y, widthCells, text.ToString(), fg, decorations));

        // Mark every cell the run covers so WriteCell skips its glyph fallback for them.
        // Background fills still happen via WriteCell; we just claim ownership of the glyphs.
        if (!_coveredColumnsByRow.TryGetValue(y, out var cols))
        {
            cols = [];
            _coveredColumnsByRow[y] = cols;
        }

        for (var col = x; col < x + widthCells; col++)
        {
            cols.Add(col);
        }

        // Background and foreground inversion are computed per-cell in WriteCell; we don't
        // need bg here. Suppress the unused-parameter warning explicitly.
        _ = bg;
    }

    /// <inheritdoc />
    public void WriteCell(int x, int y, Cell cell)
    {
        // WideTrail cells are placeholders. The leading cell at (x - 1, y) already drew
        // the codepoint and (importantly) chose a 2-cell-wide background rectangle for it,
        // so painting the trail would double-fill and could overwrite glyph pixels.
        if ((cell.Flags & CellFlags.WideTrail) != 0)
        {
            return;
        }

        var widthCells = (cell.Flags & CellFlags.WideLead) != 0 ? 2 : 1;
        var px = x * _cellWidth;
        var py = y * _cellHeight;
        var rectW = widthCells * _cellWidth;
        var rectH = _cellHeight;

        var canvas = _surface.Canvas;

        // 1. Background fill. Inverse decoration swaps fg/bg before painting.
        var fg = cell.Foreground;
        var bg = cell.Background;
        if ((cell.Decorations & TextDecorations.Inverse) != 0)
        {
            (fg, bg) = (bg, fg);
        }

        _bgPaint.Color = ToSkColor(bg);
        canvas.DrawRect(px, py, rectW, rectH, _bgPaint);

        // Cells covered by a queued shaped run have their glyph drawn during EndFrame.
        // Skip the per-cell glyph fallback for them to avoid double-painting under the
        // shaped glyph (which can manifest as a doubled / ghosted character).
        var coveredByShapedRun = _coveredColumnsByRow.TryGetValue(y, out var cols) && cols.Contains(x);

        // 2. Glyph fallback. Skip empty cells (background already covers) and cells claimed
        // by a queued shaped run.
        if (!coveredByShapedRun && cell.Codepoint != 0 && cell.Codepoint != ' ')
        {
            _fgPaint.Color = ApplyDimmedAlpha(ToSkColor(fg), cell.Decorations);
            DrawGlyph(canvas, cell.Codepoint, px, py + _textBaseline, _fgPaint);
        }

        // 3. Decorations as separate primitives. Bold/italic are font-style hints that
        // would normally require switching SKTypeface — for v1 we ignore them at this layer
        // (a follow-up that introduces a font fallback chain can wire them up properly).
        if ((cell.Decorations & TextDecorations.Underline) != 0)
        {
            _linePaint.Color = ApplyDimmedAlpha(ToSkColor(fg), cell.Decorations);
            var lineY = py + _textBaseline + 2f;
            canvas.DrawLine(px, lineY, px + rectW, lineY, _linePaint);
        }

        if ((cell.Decorations & TextDecorations.Strikethrough) != 0)
        {
            _linePaint.Color = ApplyDimmedAlpha(ToSkColor(fg), cell.Decorations);
            var lineY = py + (_cellHeight / 2f);
            canvas.DrawLine(px, lineY, px + rectW, lineY, _linePaint);
        }
    }

    /// <inheritdoc />
    public void EndFrame()
    {
        if (_queuedRuns.Count > 0)
        {
            var canvas = _surface.Canvas;
            foreach (var run in _queuedRuns)
            {
                _fgPaint.Color = ApplyDimmedAlpha(ToSkColor(run.Foreground), run.Decorations);
                var px = run.X * _cellWidth;
                var py = (run.Y * _cellHeight) + _textBaseline;

                // DrawShapedText drives HarfBuzz shaping internally and renders ligatures /
                // complex-script glyphs. The Step 8 row-level diff path will replace this
                // with cached SKTextBlobs to amortize shaping across frames.
                canvas.DrawShapedText(_shaper, run.Text, px, py, _font, _fgPaint);
            }
        }

        // The host owns the GL swap. We just flush the Skia draw queue so the GPU sees
        // every queued operation before the host swaps buffers.
        _surface.Canvas.Flush();
    }

    private static int MeasureRunCellWidth(ReadOnlySpan<char> text)
    {
        var w = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            w += WidthTable.IsWide((uint)rune.Value) ? 2 : 1;
        }

        return w;
    }

    private readonly record struct QueuedRun(int X, int Y, int CellWidthCount, string Text, Color Foreground, TextDecorations Decorations);

    /// <inheritdoc />
    public void Reset()
    {
        // No cached style state to reset — every cell is drawn standalone. If a future
        // change introduces e.g. a glyph atlas, this is where we'd invalidate it.
    }

    /// <inheritdoc />
    public void Resize(int width, int height)
    {
        // The host re-creates the SKSurface around the new GL framebuffer and calls
        // SetSurface separately. Nothing to do here.
        _ = width;
        _ = height;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _bgPaint.Dispose();
        _fgPaint.Dispose();
        _linePaint.Dispose();
        _shaper.Dispose();
    }

    private void DrawGlyph(SKCanvas canvas, uint codepoint, float x, float baselineY, SKPaint paint)
    {
        // Skia's DrawText takes a string. For BMP codepoints we'd allocate via new string((char)cp, 1),
        // which is non-trivial per-cell. Encode the rune into a stack-allocated UTF-16 buffer instead.
        Span<char> utf16 = stackalloc char[2];
        int len;
        if (Rune.TryCreate(codepoint, out var rune))
        {
            len = rune.EncodeToUtf16(utf16);
        }
        else
        {
            // Malformed codepoint — render a replacement.
            utf16[0] = '�';
            len = 1;
        }

        canvas.DrawText(utf16[..len].ToString(), x, baselineY, _font, paint);
    }

    private static SKColor ToSkColor(Color c) => new(c.R, c.G, c.B, c.A);

    private static SKColor ApplyDimmedAlpha(SKColor color, TextDecorations decorations)
    {
        // Dim is conventionally rendered as ~50% intensity. For now scale the alpha;
        // a follow-up could blend toward the background for more accurate dimming.
        if ((decorations & TextDecorations.Dim) != 0)
        {
            return color.WithAlpha((byte)(color.Alpha / 2));
        }

        return color;
    }
}
