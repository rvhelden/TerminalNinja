using TerminalNinja.Primitives;

namespace TerminalNinja.Buffers;

/// <summary>
/// An off-screen drawing surface that maps a 2×4 grid of dots onto every terminal
/// cell using Unicode braille patterns (U+2800–U+28FF). This yields four times the
/// vertical and twice the horizontal resolution of a plain cell, which is ideal for
/// drawing smooth lines and curves in charts.
///
/// Pixels are addressed in dot coordinates: the canvas is
/// <see cref="PixelWidth"/> = <c>cellWidth * 2</c> dots wide and
/// <see cref="PixelHeight"/> = <c>cellHeight * 4</c> dots tall, with the origin at
/// the top-left. Draw with <see cref="Plot"/> / <see cref="Line"/>, then compose the
/// result onto a <see cref="CellBuffer"/> with <see cref="Blit"/>.
/// </summary>
public sealed class BrailleCanvas
{
    // Bit value for each dot position within a cell, indexed by [row 0-3][col 0-1].
    // Braille dot numbering:  1 4 / 2 5 / 3 6 / 7 8  → bit masks below.
    private static readonly byte[,] DotBits =
    {
        { 0x01, 0x08 }, // row 0
        { 0x02, 0x10 }, // row 1
        { 0x04, 0x20 }, // row 2
        { 0x40, 0x80 }, // row 3
    };

    private const uint BrailleBase = 0x2800;

    private readonly byte[] _cells;

    /// <summary>Creates a canvas covering the given number of terminal cells.</summary>
    public BrailleCanvas(int cellWidth, int cellHeight)
    {
        CellWidth = Math.Max(0, cellWidth);
        CellHeight = Math.Max(0, cellHeight);
        _cells = new byte[CellWidth * CellHeight];
    }

    /// <summary>Width of the canvas in terminal cells.</summary>
    public int CellWidth { get; }

    /// <summary>Height of the canvas in terminal cells.</summary>
    public int CellHeight { get; }

    /// <summary>Width of the canvas in dots (two per cell).</summary>
    public int PixelWidth => CellWidth * 2;

    /// <summary>Height of the canvas in dots (four per cell).</summary>
    public int PixelHeight => CellHeight * 4;

    /// <summary>Clears every dot.</summary>
    public void Clear() => Array.Clear(_cells);

    /// <summary>
    /// Sets the dot at the given pixel coordinate. Out-of-range coordinates are ignored.
    /// </summary>
    public void Plot(int px, int py)
    {
        if (px < 0 || py < 0 || px >= PixelWidth || py >= PixelHeight)
        {
            return;
        }

        var cellX = px >> 1;
        var cellY = py >> 2;
        _cells[cellY * CellWidth + cellX] |= DotBits[py & 3, px & 1];
    }

    /// <summary>
    /// Draws a line between two pixel coordinates using Bresenham's algorithm.
    /// </summary>
    public void Line(int x0, int y0, int x1, int y1)
    {
        var dx = Math.Abs(x1 - x0);
        var dy = -Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx + dy;

        while (true)
        {
            Plot(x0, y0);
            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            var e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x0 += sx;
            }

            if (e2 <= dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    /// <summary>
    /// Composes the canvas onto <paramref name="buffer"/> with the top-left of the
    /// canvas placed at cell (<paramref name="originX"/>, <paramref name="originY"/>).
    /// Only cells that contain at least one dot are written, so anything already drawn
    /// (grid lines, axes) shows through empty regions. Background is left transparent.
    /// </summary>
    public void Blit(CellBuffer buffer, int originX, int originY, Color foreground)
    {
        for (var cy = 0; cy < CellHeight; cy++)
        {
            for (var cx = 0; cx < CellWidth; cx++)
            {
                var mask = _cells[cy * CellWidth + cx];
                if (mask == 0)
                {
                    continue;
                }

                var x = originX + cx;
                var y = originY + cy;
                if (buffer.IsInBounds(x, y))
                {
                    buffer.SetChar(x, y, BrailleBase + mask, foreground, Color.Transparent);
                }
            }
        }
    }
}
