using System;
using System.Collections.Generic;
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

    // Bold / italic font variants are loaded lazily by matching the base typeface's family
    // name against SKFontStyle.{Bold, Italic, BoldItalic}. Each variant has its own SKShaper
    // (HarfBuzz needs the typeface up front) and SKFont (same pixel size as the regular font).
    // If the family doesn't have a true variant available, we fall back to the regular font
    // — the alternative would be synthetic faking via SKPaint, which doesn't shape correctly.
    private readonly Dictionary<byte, StyledFont> _styledFonts = new();

    // SKTextBlob cache keyed by (text, style-bits). HarfBuzz shaping is the hottest cost on
    // the GPU path; caching the shaped+positioned blob means subsequent frames that re-render
    // the same string + style skip the shape step entirely.
    //
    // LRU eviction keeps the cache bounded: a LinkedList tracks insertion order, and the
    // oldest entry is evicted when we hit the cap. The cap is intentionally generous —
    // typical TUI content has a few hundred distinct strings, well under 1024.
    private const int ShapeCacheCap = 1024;
    private readonly Dictionary<BlobKey, LinkedListNode<CacheEntry>> _blobCache = new(capacity: ShapeCacheCap);
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
    public void ClearRegion(int cellX, int cellY, int cellWidth, int cellHeight)
    {
        if (cellWidth <= 0 || cellHeight <= 0)
        {
            return;
        }

        // Wipe a cell rectangle back to the default background (Color.Black). The renderer
        // calls this on persistent surfaces before re-emitting runs in a dirty region so
        // cells that became empty don't show stale pixels from a previous frame.
        _bgPaint.Color = ToSkColor(Color.Black);
        _surface.Canvas.DrawRect(
            cellX * _cellWidth,
            cellY * _cellHeight,
            cellWidth * _cellWidth,
            cellHeight * _cellHeight,
            _bgPaint);
    }

    /// <summary>
    /// Wipes the entire surface back to the default background. Used by the host when first
    /// creating the persistent surface or after a resize, so subsequent dirty-only repaints
    /// start from a known-clean baseline.
    /// </summary>
    public void ClearAll(int cellsWide, int cellsTall)
    {
        ClearRegion(0, 0, cellsWide, cellsTall);
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

        // 2. Shape + draw glyphs. Pick the typeface variant matching bold/italic decorations,
        // shape (via HarfBuzz) once and cache the positioned SKTextBlob keyed by (text, style).
        // Subsequent frames re-rendering the same string + style skip HarfBuzz entirely.
        //
        // Glyphs are clipped to the run's pixel rectangle so a character whose shaping
        // produces a negative left bearing (some Unicode symbols, italic faces, and certain
        // Nerd Font glyphs do) cannot paint outside its allocated cells. Without the clip
        // those stray pixels survive across frames — the dirty-rect renderer only re-clears
        // the bounding box of changed cells, so anything that bled left of column 0 of a
        // run never gets wiped.
        _fgPaint.Color = ApplyDimmedAlpha(ToSkColor(displayFg), decorations);
        var styled = GetStyledFont(decorations);
        var blob = GetOrBuildBlob(text, decorations);

        canvas.Save();
        canvas.ClipRect(new SKRect(px, py, px + rectW, py + rectH));
        try
        {
            if (blob is not null)
            {
                canvas.DrawText(blob, px, py + _textBaseline, _fgPaint);
            }
            else
            {
                // Fallback for edge cases where SKShaper / SKTextBlobBuilder doesn't produce
                // a blob (e.g. empty text after shaping). DrawShapedText reshapes inline using
                // the style-matched shaper.
                canvas.DrawShapedText(styled.Shaper, text.ToString(), px, py + _textBaseline, styled.Font, _fgPaint);
            }
        }
        finally
        {
            canvas.Restore();
        }

        // 3. Decorations that aren't carried by the font (under/strikethrough). Bold/italic
        // are baked into the chosen styled font above.
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
            // Clip the glyph to its cell rectangle — see the matching comment in WriteRun.
            // Negative left bearing or asymmetric glyph metrics would otherwise paint outside
            // the cell and survive across frames as the dirty-rect renderer never re-clears
            // pixels that fall outside a changed cell's bounds.
            canvas.Save();
            canvas.ClipRect(new SKRect(px, py, px + rectW, py + rectH));
            try
            {
                DrawGlyph(canvas, cell.Codepoint, px, py + _textBaseline, _fgPaint);
            }
            finally
            {
                canvas.Restore();
            }
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

    private SKTextBlob? GetOrBuildBlob(ReadOnlySpan<char> text, TextDecorations decorations)
    {
        var styleKey = StyleKeyFromDecorations(decorations);
        var keyStr = text.ToString();
        var key = new BlobKey(keyStr, styleKey);

        if (_blobCache.TryGetValue(key, out var existing))
        {
            _blobLruOrder.Remove(existing);
            _blobLruOrder.AddLast(existing);
            return existing.Value.Blob;
        }

        var styled = GetStyledFont(decorations);
        var blob = BuildBlob(keyStr, styled.Font, styled.Shaper, _cellWidth);
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

    private static SKTextBlob? BuildBlob(string text, SKFont font, SKShaper shaper, int cellWidth)
    {
        // Shape the text at (0, 0) — we apply the actual draw origin at DrawText time so the
        // same shaped result can be reused across cells/frames at different positions.
        var result = shaper.Shape(text, 0, 0, font);
        if (result.Codepoints is null || result.Codepoints.Length == 0)
        {
            return null;
        }

        // SKShaper.Result.Codepoints is uint[] in 3.119.x; SKTextBlobBuilder's positioned-run
        // glyph span is ushort. Narrow on copy — codepoints from HarfBuzz are glyph indices,
        // which fit in 16 bits for every font we'll realistically use.
        using var builder = new SKTextBlobBuilder();
        var run = builder.AllocatePositionedRun(font, result.Codepoints.Length);
        var glyphs = run.Glyphs;
        for (var i = 0; i < result.Codepoints.Length; i++)
        {
            glyphs[i] = (ushort)result.Codepoints[i];
        }

        // Force each glyph to sit at its source character's cell column. HarfBuzz's per-glyph
        // X positions accumulate GPOS / kerning / fractional-advance noise that's invisible
        // for one character but accumulates over a long run — most visibly on contiguous
        // vertical box-drawing glyphs (the panel border │) where the same glyph stacked
        // across rows visibly drifts a cell off-axis. We use HarfBuzz's cluster array to
        // map glyph → source char index, then plant the glyph at `cluster × cellWidth`.
        //
        // This preserves ligature behaviour: a 2-char ligature produces 1 glyph with
        // cluster pointing at the first source char, so the glyph anchors at that char's
        // cell and the following glyph (cluster=2) lands two cells over — the ligature
        // naturally occupies the cells it visually spans. Pure-monospace input (the common
        // case) maps 1:1 cluster↔glyph, so every glyph snaps to its own cell exactly.
        var clusters = result.Clusters;
        var dstPoints = run.Positions;
        for (var i = 0; i < result.Codepoints.Length && i < dstPoints.Length; i++)
        {
            var charIndex = clusters is not null && i < clusters.Length ? (int)clusters[i] : i;
            dstPoints[i] = new SKPoint(charIndex * cellWidth, 0);
        }
        return builder.Build();
    }

    /// <summary>
    /// Returns the (font, shaper) pair matching the bold/italic decoration bits, lazily
    /// loading a typeface variant on first request. Falls back to the base font/shaper when
    /// the typeface family doesn't have a true variant (e.g. SKTypeface.Default may not).
    /// </summary>
    private StyledFont GetStyledFont(TextDecorations decorations)
    {
        var key = StyleKeyFromDecorations(decorations);
        if (key == 0)
        {
            return new StyledFont(_font, _shaper);
        }

        if (_styledFonts.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var style = (key & 1) switch
        {
            1 when (key & 2) != 0 => SKFontStyle.BoldItalic,
            1 => SKFontStyle.Bold,
            _ => SKFontStyle.Italic, // key & 2 must be set when we reach here
        };

        var family = _typeface.FamilyName ?? string.Empty;
        var variantTypeface = family.Length > 0 ? SKTypeface.FromFamilyName(family, style) : null;

        StyledFont entry;
        if (variantTypeface is null || ReferenceEquals(variantTypeface, _typeface))
        {
            // No real variant available — use the regular font/shaper. Synthetic bold via
            // SKPaint stroke would render but bypass the shaper, breaking ligatures.
            variantTypeface?.Dispose();
            entry = new StyledFont(_font, _shaper);
        }
        else
        {
            var variantFont = new SKFont(variantTypeface, _font.Size);
            var variantShaper = new SKShaper(variantTypeface);
            entry = new StyledFont(variantFont, variantShaper);
        }

        _styledFonts[key] = entry;
        return entry;
    }

    private static byte StyleKeyFromDecorations(TextDecorations decorations)
    {
        var bold = (decorations & TextDecorations.Bold) != 0 ? 1 : 0;
        var italic = (decorations & TextDecorations.Italic) != 0 ? 2 : 0;
        return (byte)(bold | italic);
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

    private readonly record struct BlobKey(string Text, byte StyleKey);

    private readonly record struct CacheEntry(BlobKey Key, SKTextBlob Blob);

    /// <summary>A typeface variant: paired SKFont (sized) and SKShaper (HarfBuzz-bound to the typeface).</summary>
    private readonly record struct StyledFont(SKFont Font, SKShaper Shaper);

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

        // Dispose any lazily-loaded typeface variants we created. The base font/shaper
        // were either constructed in our own ctor (we dispose) or passed in (caller owns —
        // we don't dispose the SKFont). We DO own _shaper either way (created in ctor).
        foreach (var styled in _styledFonts.Values)
        {
            if (!ReferenceEquals(styled.Font, _font))
            {
                styled.Font.Dispose();
            }

            if (!ReferenceEquals(styled.Shaper, _shaper))
            {
                styled.Shaper.Dispose();
            }
        }

        _styledFonts.Clear();
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
