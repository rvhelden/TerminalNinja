using SkiaSharp;

namespace TerminalNinja.Skia;

/// <summary>
/// Procedural renderer for Unicode box-drawing characters (U+2500..U+257F subset).
/// Monospace fonts typically draw ─ / │ / corner glyphs inside the glyph's advance box
/// with horizontal padding so neighbouring letters don't crowd — fine for prose, but
/// adjacent ─ cells then leave a 1–2 px gap that reads as a dashed line. Rendering box
/// chars as Skia primitives that span the entire cell rectangle makes them tile
/// seamlessly at any display scale.
/// </summary>
internal static class BoxDrawing
{
    /// <summary>True if the codepoint is a box-drawing character handled by <see cref="Draw"/>.</summary>
    public static bool Handles(uint codepoint) => codepoint switch
    {
        0x2500 or 0x2502 => true,                                       // ─ │
        0x250C or 0x2510 or 0x2514 or 0x2518 => true,                   // ┌ ┐ └ ┘
        0x251C or 0x2524 or 0x252C or 0x2534 or 0x253C => true,         // ├ ┤ ┬ ┴ ┼
        0x2550 or 0x2551 => true,                                       // ═ ║
        0x2554 or 0x2557 or 0x255A or 0x255D => true,                   // ╔ ╗ ╚ ╝
        0x2560 or 0x2563 or 0x2566 or 0x2569 or 0x256C => true,         // ╠ ╣ ╦ ╩ ╬
        0x256D or 0x256E or 0x256F or 0x2570 => true,                   // ╭ ╮ ╯ ╰
        _ => false,
    };

    /// <summary>
    /// Draws the box-drawing character into a single cell at pixel origin
    /// (<paramref name="px"/>, <paramref name="py"/>) using <paramref name="paint"/>'s
    /// current colour. The caller is responsible for clipping if pixels must not bleed
    /// past the cell rectangle. Callers MUST first check <see cref="Handles"/>; passing an
    /// unsupported codepoint is a no-op.
    /// </summary>
    public static void Draw(SKCanvas canvas, float px, float py, int cellWidth, int cellHeight, uint codepoint, SKPaint paint)
    {
        // Pixel-aligned mid-axes. Floor so a 9×18 cell's vertical mid lands on integer y=9 —
        // the same pixel row most monospace fonts use for ─, so glyph + procedural produce
        // visually identical horizontal lines aside from the cell-spanning extent.
        var midX = MathF.Floor(px + cellWidth * 0.5f);
        var midY = MathF.Floor(py + cellHeight * 0.5f);
        var right = px + cellWidth;
        var bottom = py + cellHeight;

        // Single-line stroke thickness scales with cell width (~ 1/8). Clamp ≥ 1 so 1× displays
        // don't drop the line entirely; at 2× / 3× we get a naturally thicker stroke without
        // explicit scale plumbing.
        var single = MathF.Max(1f, MathF.Round(cellWidth / 8f));
        var hs = single * 0.5f;

        // Double-line gap: distance between the two parallel stroke centres. 0.35×cellWidth
        // gives clear separation at 9px while still reading as a single double-line at 1×.
        var dGap = MathF.Max(2f, MathF.Round(cellWidth * 0.35f));
        var dHalfGap = dGap * 0.5f;
        var dTop = midY - dHalfGap;
        var dBot = midY + dHalfGap;
        var dLeft = midX - dHalfGap;
        var dRight = midX + dHalfGap;

        // H / V draw a horizontal / vertical stroke. Endpoints are inclusive ranges along the
        // long axis; the stroke is `single` thick, centred on `y` / `x`. Corner cases extend
        // the endpoint by hs so adjacent strokes overlap at the joint and leave no notch.
        void H(float x1, float x2, float y) => canvas.DrawRect(x1, y - hs, x2 - x1, single, paint);
        void V(float y1, float y2, float x) => canvas.DrawRect(x - hs, y1, single, y2 - y1, paint);

        switch (codepoint)
        {
            // ─ │  Light single horizontal / vertical: full-cell span.
            case 0x2500: H(px, right, midY); break;
            case 0x2502: V(py, bottom, midX); break;

            // ┌ ┐ └ ┘  Light corners: half-strokes meeting at midpoint, +hs overlap on the
            // edge that runs into the corner so the joint paints as a solid square.
            case 0x250C: H(midX - hs, right, midY); V(midY - hs, bottom, midX); break;
            case 0x2510: H(px, midX + hs, midY); V(midY - hs, bottom, midX); break;
            case 0x2514: H(midX - hs, right, midY); V(py, midY + hs, midX); break;
            case 0x2518: H(px, midX + hs, midY); V(py, midY + hs, midX); break;

            // ├ ┤ ┬ ┴ ┼  Light T-junctions and cross.
            case 0x251C: V(py, bottom, midX); H(midX - hs, right, midY); break;
            case 0x2524: V(py, bottom, midX); H(px, midX + hs, midY); break;
            case 0x252C: H(px, right, midY); V(midY - hs, bottom, midX); break;
            case 0x2534: H(px, right, midY); V(py, midY + hs, midX); break;
            case 0x253C: H(px, right, midY); V(py, bottom, midX); break;

            // ═ ║  Double horizontal / vertical: two parallel single strokes.
            case 0x2550: H(px, right, dTop); H(px, right, dBot); break;
            case 0x2551: V(py, bottom, dLeft); V(py, bottom, dRight); break;

            // ╔ ╗ ╚ ╝  Double corners. Inner / outer strokes form an L; the inner stroke is
            // shortened so it terminates at the inner side of the perpendicular stroke (no
            // T-stub poking past the corner). The outer stroke extends to the cell edge.
            case 0x2554: // ╔
                H(dLeft, right, dTop); V(dTop, bottom, dLeft);
                H(dRight, right, dBot); V(dBot, bottom, dRight);
                break;
            case 0x2557: // ╗
                H(px, dRight, dTop); V(dTop, bottom, dRight);
                H(px, dLeft, dBot); V(dBot, bottom, dLeft);
                break;
            case 0x255A: // ╚
                H(dLeft, right, dBot); V(py, dBot, dLeft);
                H(dRight, right, dTop); V(py, dTop, dRight);
                break;
            case 0x255D: // ╝
                H(px, dRight, dBot); V(py, dBot, dRight);
                H(px, dLeft, dTop); V(py, dTop, dLeft);
                break;

            // ╠ ╣ ╦ ╩ ╬  Double T-junctions and cross. The crossing line stays full-cell;
            // the stub line stops at the inner stroke of the crossing pair.
            case 0x2560: // ╠
                V(py, bottom, dLeft); V(py, bottom, dRight);
                H(dRight, right, dTop); H(dRight, right, dBot);
                break;
            case 0x2563: // ╣
                V(py, bottom, dLeft); V(py, bottom, dRight);
                H(px, dLeft, dTop); H(px, dLeft, dBot);
                break;
            case 0x2566: // ╦
                H(px, right, dTop); H(px, right, dBot);
                V(dBot, bottom, dLeft); V(dBot, bottom, dRight);
                break;
            case 0x2569: // ╩
                H(px, right, dTop); H(px, right, dBot);
                V(py, dTop, dLeft); V(py, dTop, dRight);
                break;
            case 0x256C: // ╬
                H(px, dLeft, dTop); H(dRight, right, dTop);
                H(px, dLeft, dBot); H(dRight, right, dBot);
                V(py, dTop, dLeft); V(dBot, bottom, dLeft);
                V(py, dTop, dRight); V(dBot, bottom, dRight);
                break;

            // ╭ ╮ ╯ ╰  Rounded corners. Modelled as right-angle joints for now — the round
            // is a 1–2 px visual nicety that the procedural straight version doesn't sacrifice,
            // and a proper arc primitive at single-pixel stroke widths reads worse than the
            // crisp square joint at small cell sizes anyway.
            case 0x256D: H(midX - hs, right, midY); V(midY - hs, bottom, midX); break; // ╭ as ┌
            case 0x256E: H(px, midX + hs, midY); V(midY - hs, bottom, midX); break;    // ╮ as ┐
            case 0x256F: H(px, midX + hs, midY); V(py, midY + hs, midX); break;        // ╯ as ┘
            case 0x2570: H(midX - hs, right, midY); V(py, midY + hs, midX); break;     // ╰ as └
        }
    }
}
