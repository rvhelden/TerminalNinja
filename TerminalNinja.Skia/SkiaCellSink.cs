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

    // SKTextBlob cache keyed by run text. HarfBuzz shaping is by far the hottest cost on the
    // GPU path; caching the shaped+positioned blob means subsequent frames that re-render
    // the same string skip the shape step entirely. The font/typeface is stable for the
    // lifetime of the sink, so text alone is a sufficient cache key.
    //
    // LRU eviction keeps the cache bounded: an LinkedList tracks insertion order, and the
    // oldest entry is evicted when we hit the cap. The cap is intentionally generous —
    // typical TUI content has a few hundred distinct strings, well under 1024.
    private const int ShapeCacheCap = 1024;
    private readonly Dictionary<string, LinkedListNode<CacheEntry>> _blobCache = new(capacity: ShapeCacheCap);
    private readonly LinkedList<CacheEntry> _blobLruOrder = new();

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
        // Step 8 makes WriteRun self-contained — no per-frame queue to reset. Reserved for
        // future state caches (e.g. an LRU glyph atlas) that may need invalidation hooks.
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

        var canvas = _surface.Canvas;
        var px = x * _cellWidth;
        var py = y * _cellHeight;
        var rectW = widthCells * _cellWidth;
        var rectH = _cellHeight;

        // Inverse decoration swaps fg/bg before any painting.
        var displayFg = fg;
        var displayBg = bg;
        if ((decorations & TextDecorations.Inverse) != 0)
        {
            (displayFg, displayBg) = (displayBg, displayFg);
        }

        // 1. Background fill spanning the entire run.
        _bgPaint.Color = ToSkColor(displayBg);
        canvas.DrawRect(px, py, rectW, rectH, _bgPaint);

        // 2. Shape + draw glyphs. We cache the shaped + positioned SKTextBlob keyed by run text
        // so subsequent frames re-rendering the same string skip HarfBuzz entirely. The font
        // is fixed for the sink's lifetime, so text alone is sufficient as a key.
        _fgPaint.Color = ApplyDimmedAlpha(ToSkColor(displayFg), decorations);
        var blob = GetOrBuildBlob(text);
        if (blob is not null)
        {
            canvas.DrawText(blob, px, py + _textBaseline, _fgPaint);
        }
        else
        {
            // Fallback for edge cases where SKShaper / SKTextBlobBuilder doesn't produce a
            // blob (e.g. empty text after shaping). DrawShapedText reshapes inline.
            canvas.DrawShapedText(_shaper, text.ToString(), px, py + _textBaseline, _font, _fgPaint);
        }

        // 3. Decorations as separate primitives. Bold/italic would require switching typeface;
        // ignore for now (a font fallback chain in a follow-up wires them up properly).
        if ((decorations & TextDecorations.Underline) != 0)
        {
            _linePaint.Color = _fgPaint.Color;
            var lineY = py + _textBaseline + 2f;
            canvas.DrawLine(px, lineY, px + rectW, lineY, _linePaint);
        }

        if ((decorations & TextDecorations.Strikethrough) != 0)
        {
            _linePaint.Color = _fgPaint.Color;
            var lineY = py + (_cellHeight / 2f);
            canvas.DrawLine(px, lineY, px + rectW, lineY, _linePaint);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// In Step 8 the renderer routes shaped sinks through <see cref="WriteRun"/> and does
    /// not call <see cref="WriteCell"/>. This method remains because the <see cref="ICellSink"/>
    /// contract requires it and direct callers (tests, or future controls that bypass the
    /// renderer's row-level path) still need single-cell rendering.
    /// </remarks>
    public void WriteCell(int x, int y, Cell cell)
    {
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

        var fg = cell.Foreground;
        var bg = cell.Background;
        if ((cell.Decorations & TextDecorations.Inverse) != 0)
        {
            (fg, bg) = (bg, fg);
        }

        _bgPaint.Color = ToSkColor(bg);
        canvas.DrawRect(px, py, rectW, rectH, _bgPaint);

        if (cell.Codepoint != 0 && cell.Codepoint != ' ')
        {
            _fgPaint.Color = ApplyDimmedAlpha(ToSkColor(fg), cell.Decorations);
            DrawGlyph(canvas, cell.Codepoint, px, py + _textBaseline, _fgPaint);
        }

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

    private SKTextBlob? GetOrBuildBlob(ReadOnlySpan<char> text)
    {
        // Look up by content; the lookup needs a string key, so convert once. Hot path will
        // be a cache hit, so the allocation only matters on cold misses (rare in steady state).
        var key = text.ToString();
        if (_blobCache.TryGetValue(key, out var existing))
        {
            // Move to back of LRU order without re-allocating the node.
            _blobLruOrder.Remove(existing);
            _blobLruOrder.AddLast(existing);
            return existing.Value.Blob;
        }

        var blob = BuildBlob(key);
        if (blob is null)
        {
            return null;
        }

        // Evict the oldest entry if we're at capacity.
        if (_blobCache.Count >= ShapeCacheCap && _blobLruOrder.First is { } oldest)
        {
            _blobLruOrder.RemoveFirst();
            if (_blobCache.Remove(oldest.Value.Key))
            {
                oldest.Value.Blob.Dispose();
            }
        }

        var node = _blobLruOrder.AddLast(new CacheEntry(key, blob));
        _blobCache[key] = node;
        return blob;
    }

    private SKTextBlob? BuildBlob(string text)
    {
        // Shape the text at (0, 0) — we apply the actual draw origin at DrawText time so the
        // same shaped result can be reused across cells/frames at different positions.
        var result = _shaper.Shape(text, 0, 0, _font);
        if (result.Codepoints is null || result.Codepoints.Length == 0)
        {
            return null;
        }

        // SKShaper.Result.Codepoints is uint[] in 3.119.x; SKTextBlobBuilder's positioned-run
        // glyph span is ushort. Narrow on copy — codepoints from HarfBuzz are glyph indices,
        // which fit in 16 bits for every font we'll realistically use.
        using var builder = new SKTextBlobBuilder();
        var run = builder.AllocatePositionedRun(_font, result.Codepoints.Length);
        var glyphs = run.Glyphs;
        for (var i = 0; i < result.Codepoints.Length; i++)
        {
            glyphs[i] = (ushort)result.Codepoints[i];
        }

        result.Points.AsSpan().CopyTo(run.Positions);
        return builder.Build();
    }

    /// <summary>Discards the SKTextBlob shape cache. Useful when font / cell metrics change.</summary>
    private void ClearShapeCache()
    {
        foreach (var node in _blobLruOrder)
        {
            node.Blob.Dispose();
        }

        _blobLruOrder.Clear();
        _blobCache.Clear();
    }

    private readonly record struct CacheEntry(string Key, SKTextBlob Blob);

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
        ClearShapeCache();
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
