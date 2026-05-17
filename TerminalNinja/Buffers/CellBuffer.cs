using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TerminalNinja.Primitives;
using TerminalNinja.Rendering;

namespace TerminalNinja.Buffers;

/// <summary>
/// Represents a change to a single cell in the buffer.
/// </summary>
public readonly record struct CellChange(int X, int Y, Cell Cell);

/// <summary>
/// A double-buffered cell buffer backed by unmanaged <see cref="NativeMemory"/> with dirty
/// tracking for optimized rendering. Zero-allocation diffing using struct enumerators.
/// <para>
/// The backing allocation uses power-of-2 capacity so that incremental terminal resizes
/// (e.g. window drag) rarely trigger a reallocation. On resize the overlapping content
/// region is preserved; new cells are filled with <see cref="Cell.Empty"/>.
/// </para>
/// </summary>
public sealed unsafe class CellBuffer : IDisposable
{
    private Cell* _current;       // What we're rendering to
    private Cell* _previous;      // What's currently on screen
    private DirtyRect _dirtyRect;
    private bool _disposed;

    // Per-row grapheme cluster side tables: row index → (column → codepoint sequence).
    // Each row's dictionary is lazily allocated so ASCII frames pay no allocation cost.
    // The cell at (x, y) carries CellFlags.HasGrapheme when an entry exists at column x;
    // its Codepoint stores the cluster's lead codepoint (so width / fallback rendering still work).
    private Dictionary<int, uint[]>?[] _rowGraphemes;
    private Dictionary<int, uint[]>?[] _previousRowGraphemes;

    /// <summary>
    /// The <see cref="ICellSink"/> that will receive cells from this buffer during the
    /// current Draw call, or <see langword="null"/> when no draw is in progress. Set by
    /// <see cref="Rendering.Renderer.Draw"/> so controls can detect optional capabilities
    /// (e.g. <see cref="IShapedRunSink"/>) and emit higher-level operations alongside
    /// per-cell writes. Cleared at the end of the Draw call.
    /// </summary>
    public ICellSink? ActiveSink { get; internal set; }
    
    /// <summary>Gets the width of the buffer in cells.</summary>
    public int Width { get; private set; }
    
    /// <summary>Gets the height of the buffer in cells.</summary>
    public int Height { get; private set; }
    
    /// <summary>Gets the current backing capacity in total cell slots (always a power of 2).</summary>
    public int Capacity { get; private set; }

    /// <summary>
    /// Creates a new cell buffer with the specified dimensions.
    /// </summary>
    public CellBuffer(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        
        Width = width;
        Height = height;
        
        Capacity = (int)BitOperations.RoundUpToPowerOf2((uint)(width * height));
        
        _current = AllocBuffer(Capacity);
        _previous = AllocBuffer(Capacity);
        _rowGraphemes = new Dictionary<int, uint[]>?[height];
        _previousRowGraphemes = new Dictionary<int, uint[]>?[height];

        // Initialize logical region to Cell.Empty
        FillEmpty(_current, width, height);
        FillEmpty(_previous, width, height);

        // Mark the entire buffer dirty so the first Present() flushes everything
        MarkFullDirty();
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Index(int x, int y) => y * Width + x;
    
    /// <summary>
    /// Checks if the specified coordinates are within bounds.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsInBounds(int x, int y) => 
        (uint)x < (uint)Width && (uint)y < (uint)Height;
    
    /// <summary>
    /// Sets a cell at the specified position. When the new cell does not carry
    /// <see cref="CellFlags.HasGrapheme"/>, any stale row-side grapheme entry at
    /// (<paramref name="x"/>, <paramref name="y"/>) is cleared so an overwriting
    /// single-codepoint write doesn't leave a multi-codepoint cluster behind.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetCell(int x, int y, Cell cell)
    {
        if (!IsInBounds(x, y))
        {
            return;
        }

        var index = Index(x, y);
        if (_current[index] == cell)
        {
            return;
        }

        _current[index] = cell;
        _dirtyRect.Expand(x, y);

        // If the new cell isn't a grapheme but the previous cell at this position was,
        // the row side-table entry is now stale — drop it.
        if ((cell.Flags & CellFlags.HasGrapheme) == 0)
        {
            _rowGraphemes[y]?.Remove(x);
        }
    }

    /// <summary>
    /// Stores a multi-codepoint grapheme cluster at the given position. The lead
    /// codepoint and styling go on the cell at (<paramref name="x"/>, <paramref name="y"/>)
    /// with <see cref="CellFlags.HasGrapheme"/> set; the full <paramref name="codepoints"/>
    /// sequence is kept in the row-side table. If the lead codepoint is wide, a
    /// <see cref="CellFlags.WideTrail"/> placeholder is written at (<paramref name="x"/> + 1, <paramref name="y"/>).
    /// </summary>
    public void SetGrapheme(int x, int y, ReadOnlySpan<uint> codepoints, Color fg, Color bg, TextDecorations decorations)
    {
        if (codepoints.IsEmpty || !IsInBounds(x, y))
        {
            return;
        }

        if (bg.IsTransparent)
        {
            bg = _current[Index(x, y)].Background;
        }

        var lead = codepoints[0];
        var isWide = WidthTable.IsWide(lead);

        if (isWide && x + 1 >= Width)
        {
            // No room for the trail — degrade to a space to keep the grid intact.
            SetCell(x, y, new Cell((uint)' ', fg, bg, decorations));
            return;
        }

        // Store the cluster sequence first; SetCell will then see HasGrapheme and skip
        // its own cleanup of the side-table entry.
        var row = _rowGraphemes[y] ??= new Dictionary<int, uint[]>();
        row[x] = codepoints.ToArray();

        var flags = CellFlags.HasGrapheme | (isWide ? CellFlags.WideLead : CellFlags.None);
        SetCell(x, y, new Cell(lead, fg, bg, decorations, flags));

        // SetCell short-circuits when the Cell-level value is unchanged. That's wrong here
        // if only the cluster sequence changed (same lead + colors but different combining
        // marks). Force-mark the row dirty so the diff path visits this cell and the
        // grapheme-aware comparison can detect the cluster change.
        _dirtyRect.Expand(x, y);

        if (isWide)
        {
            SetCell(x + 1, y, new Cell(0u, fg, bg, decorations, CellFlags.WideTrail));
        }
    }

    /// <summary>
    /// Returns the codepoint sequence stored at (<paramref name="x"/>, <paramref name="y"/>).
    /// For grapheme cells this is the full multi-codepoint cluster; otherwise it's a
    /// single-element array containing the cell's <see cref="Cell.Codepoint"/>.
    /// Out-of-bounds reads return an empty array.
    /// </summary>
    public uint[] GetGrapheme(int x, int y)
    {
        if (!IsInBounds(x, y))
        {
            return [];
        }

        var cell = _current[Index(x, y)];
        if ((cell.Flags & CellFlags.HasGrapheme) != 0
            && _rowGraphemes[y] is { } row
            && row.TryGetValue(x, out var seq))
        {
            return seq;
        }

        return [cell.Codepoint];
    }
    
    /// <summary>
    /// Gets the cell at the specified position.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cell GetCell(int x, int y)
    {
        if (!IsInBounds(x, y))
        {
            return Cell.Empty;
        }

        return _current[Index(x, y)];
    }
    
    /// <summary>
    /// Gets a <see cref="Span{T}"/> over a single row in the current buffer.
    /// </summary>
    /// <param name="y">The zero-based row index.</param>
    /// <returns>A span of <see cref="Cell"/> values for the requested row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Cell> GetRow(int y)
    {
        if ((uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y), y, $"Row index must be in [0, {Height}).");
        }

        return new Span<Cell>(_current + y * Width, Width);
    }
    
    /// <summary>
    /// Gets a reference to the cell at the specified position.
    /// </summary>
    public ref Cell this[int x, int y]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (!IsInBounds(x, y))
            {
                throw new ArgumentOutOfRangeException($"({x}, {y}) is out of bounds ({Width}x{Height}).");
            }

            return ref _current[Index(x, y)];
        }
    }
    
    /// <summary>
    /// Sets a cell with the given Unicode codepoint and colors.
    /// When <paramref name="bg"/> is transparent the existing cell's background is preserved.
    /// Wide East Asian / emoji codepoints automatically occupy two cells:
    /// a leading cell flagged <see cref="CellFlags.WideLead"/> at <c>(x, y)</c> and a
    /// trailing placeholder flagged <see cref="CellFlags.WideTrail"/> at <c>(x + 1, y)</c>.
    /// </summary>
    public void SetChar(int x, int y, uint codepoint, Color fg, Color bg)
    {
        if (bg.IsTransparent && IsInBounds(x, y))
        {
            bg = _current[Index(x, y)].Background;
        }

        SetCharCore(x, y, codepoint, fg, bg, TextDecorations.None);
    }

    /// <summary>
    /// Sets a cell with the given Unicode codepoint, colors, and text decorations.
    /// See <see cref="SetChar(int, int, uint, Color, Color)"/> for wide-character handling.
    /// </summary>
    public void SetChar(int x, int y, uint codepoint, Color fg, Color bg, TextDecorations decorations)
    {
        if (bg.IsTransparent && IsInBounds(x, y))
        {
            bg = _current[Index(x, y)].Background;
        }

        SetCharCore(x, y, codepoint, fg, bg, decorations);
    }

    /// <summary>
    /// Convenience overload for BMP <see cref="char"/> input. Forwards to the
    /// <see cref="uint"/> overload via implicit widening.
    /// </summary>
    public void SetChar(int x, int y, char c, Color fg, Color bg)
        => SetChar(x, y, (uint)c, fg, bg);

    /// <summary>
    /// Convenience overload for BMP <see cref="char"/> input with decorations.
    /// </summary>
    public void SetChar(int x, int y, char c, Color fg, Color bg, TextDecorations decorations)
        => SetChar(x, y, (uint)c, fg, bg, decorations);

    private void SetCharCore(int x, int y, uint codepoint, Color fg, Color bg, TextDecorations deco)
    {
        if (WidthTable.IsWide(codepoint))
        {
            // Wide characters occupy two cells. If the trail would land outside the buffer
            // we drop back to a space — rendering a partial wide glyph corrupts the grid.
            if (x + 1 < Width)
            {
                SetCell(x, y, new Cell(codepoint, fg, bg, deco, CellFlags.WideLead));
                SetCell(x + 1, y, new Cell(0u, fg, bg, deco, CellFlags.WideTrail));
            }
            else
            {
                SetCell(x, y, new Cell((uint)' ', fg, bg, deco));
            }
        }
        else
        {
            SetCell(x, y, new Cell(codepoint, fg, bg, deco));
        }
    }
    
    /// <summary>
    /// Clears the entire buffer to empty cells. All row-side grapheme entries are dropped
    /// (they only make sense alongside a HasGrapheme-flagged cell).
    /// </summary>
    public void Clear()
    {
        FillEmpty(_current, Width, Height);
        for (var y = 0; y < _rowGraphemes.Length; y++)
        {
            _rowGraphemes[y] = null;
        }

        MarkFullDirty();
    }
    
    /// <summary>
    /// Fills a rectangular region with the specified cell.
    /// </summary>
    public void FillRect(Rect bounds, Cell cell)
    {
        var clipped = bounds.Intersect(new Rect(0, 0, Width, Height));
        if (clipped.Width <= 0 || clipped.Height <= 0)
        {
            return;
        }

        for (var y = clipped.Y; y < clipped.Bottom; y++)
        {
            var rowStart = Index(clipped.X, y);
            new Span<Cell>(_current + rowStart, clipped.Width).Fill(cell);
        }
        
        _dirtyRect.Expand(clipped.X, clipped.Y);
        _dirtyRect.Expand(clipped.Right - 1, clipped.Bottom - 1);
    }
    
    /// <summary>
    /// Dims all cells in the specified rectangular region by halving their RGB values.
    /// This creates a darkened overlay effect suitable for modal dialog backdrops.
    /// Foreground colors are also dimmed to maintain relative contrast.
    /// </summary>
    /// <param name="bounds">The rectangular region to dim.</param>
    public void DimRect(Rect bounds)
    {
        var clipped = bounds.Intersect(new Rect(0, 0, Width, Height));
        if (clipped.Width <= 0 || clipped.Height <= 0)
        {
            return;
        }

        for (var y = clipped.Y; y < clipped.Bottom; y++)
        {
            for (var x = clipped.X; x < clipped.Right; x++)
            {
                var index = Index(x, y);
                var cell = _current[index];
                var dimmedFg = DimColor(cell.Foreground);
                var dimmedBg = DimColor(cell.Background);
                var dimmed = new Cell(cell.Codepoint, dimmedFg, dimmedBg, cell.Decorations);
                if (_current[index] != dimmed)
                {
                    _current[index] = dimmed;
                    _dirtyRect.Expand(x, y);
                }
            }
        }
    }

    /// <summary>
    /// Dims all cells in the entire buffer by halving their RGB values.
    /// </summary>
    public void DimAll()
    {
        DimRect(new Rect(0, 0, Width, Height));
    }

    /// <summary>
    /// Halves the RGB channels of a color to produce a dimmed version.
    /// Transparent colors are returned unchanged.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Color DimColor(Color color)
    {
        if (color.IsTransparent)
        {
            return color;
        }

        return new Color((byte)(color.R >> 1), (byte)(color.G >> 1), (byte)(color.B >> 1), color.A);
    }
    
    /// <summary>
    /// Copies a rectangular region from this buffer to a target buffer.
    /// Both source and target regions are independently clipped to their respective buffer bounds.
    /// </summary>
    /// <param name="target">The destination buffer.</param>
    /// <param name="sourceRect">The region to copy from this buffer.</param>
    /// <param name="targetX">The X position in the target buffer to copy to.</param>
    /// <param name="targetY">The Y position in the target buffer to copy to.</param>
    public void CopyRegionTo(CellBuffer target, Rect sourceRect, int targetX, int targetY)
    {
        // Clip source rect to this buffer's bounds
        var clippedSource = sourceRect.Intersect(new Rect(0, 0, Width, Height));
        if (clippedSource.Width <= 0 || clippedSource.Height <= 0)
        {
            return;
        }

        // Calculate the target region
        var targetRect = new Rect(targetX, targetY, clippedSource.Width, clippedSource.Height);
        var clippedTarget = targetRect.Intersect(new Rect(0, 0, target.Width, target.Height));
        if (clippedTarget.Width <= 0 || clippedTarget.Height <= 0)
        {
            return;
        }

        // Adjust source start if the target was clipped on the left or top
        var srcStartX = clippedSource.X + (clippedTarget.X - targetX);
        var srcStartY = clippedSource.Y + (clippedTarget.Y - targetY);
        var copyWidth = clippedTarget.Width;
        var copyHeight = clippedTarget.Height;

        for (var row = 0; row < copyHeight; row++)
        {
            var srcRow = new ReadOnlySpan<Cell>(_current + (srcStartY + row) * Width + srcStartX, copyWidth);
            var dstRow = new Span<Cell>(target._current + (clippedTarget.Y + row) * target.Width + clippedTarget.X, copyWidth);
            srcRow.CopyTo(dstRow);
        }

        // Expand the target's dirty rect
        target._dirtyRect.Expand(clippedTarget.X, clippedTarget.Y);
        target._dirtyRect.Expand(clippedTarget.Right - 1, clippedTarget.Bottom - 1);
    }

    /// <summary>
    /// Gets a zero-allocation enumerator for changed cells.
    /// </summary>
    public CellDiffEnumerator GetChanges() => new(this);
    
    /// <summary>
    /// Swaps the front and back buffers (zero-copy operation).
    /// </summary>
    public void SwapBuffers()
    {
        var tmp = _current;
        _current = _previous;
        _previous = tmp;

        // Swap the row grapheme tables in lockstep so the diff path can compare
        // both frames' cluster sequences alongside the cell pointers.
        (_rowGraphemes, _previousRowGraphemes) = (_previousRowGraphemes, _rowGraphemes);

        _dirtyRect.Reset();
    }
    
    /// <summary>
    /// Resizes the buffer to new dimensions, preserving content in the current (render)
    /// buffer's overlapping region. The previous (screen-state) buffer is reset to
    /// <see cref="Cell.Empty"/> because after a terminal resize the actual screen content
    /// is unpredictable — the Renderer sends <c>\e[2J</c> (clear screen), so <c>_previous</c>
    /// must reflect that blank state. This ensures the next <see cref="GetChanges"/> diff
    /// forces a full repaint of every cell.
    /// <para>
    /// Uses power-of-2 capacity to minimize reallocations during incremental resizes.
    /// </para>
    /// </summary>
    public void Resize(int newWidth, int newHeight)
    {
        if (newWidth == Width && newHeight == Height)
        {
            return;
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newHeight);

        var newSize = newWidth * newHeight;
        var newCapacity = (int)BitOperations.RoundUpToPowerOf2((uint)newSize);

        var oldWidth = Width;
        var oldHeight = Height;
        var copyWidth = Math.Min(oldWidth, newWidth);
        var copyHeight = Math.Min(oldHeight, newHeight);

        if (newCapacity > Capacity)
        {
            // Need larger allocation — allocate, copy overlapping region, free old
            _current = ResizeBuffer(_current, oldWidth, newWidth, newHeight, newCapacity, copyWidth, copyHeight);

            // _previous: allocate fresh and fill with Cell.Empty (no content preservation)
            NativeMemory.Free(_previous);
            _previous = AllocBuffer(newCapacity);
            FillEmpty(_previous, newWidth, newHeight);

            Capacity = newCapacity;
        }
        else
        {
            // Capacity is sufficient — reshuffle rows in-place for _current only
            ReshuffleInPlace(_current, oldWidth, oldHeight, newWidth, newHeight, copyWidth, copyHeight);

            // _previous: just reset to Cell.Empty (terminal screen is blank after resize)
            FillEmpty(_previous, newWidth, newHeight);
        }

        // Row-side grapheme tables: drop entries whose column is now out of range, then
        // reshuffle the per-row arrays. _previousRowGraphemes is wiped because _previous
        // was reset to Cell.Empty (no clusters survive a resize).
        var newRowGraphemes = new Dictionary<int, uint[]>?[newHeight];
        var rowsToCopy = Math.Min(copyHeight, _rowGraphemes.Length);
        for (var y = 0; y < rowsToCopy; y++)
        {
            var oldRow = _rowGraphemes[y];
            if (oldRow == null)
            {
                continue;
            }

            Dictionary<int, uint[]>? newRow = null;
            foreach (var (col, seq) in oldRow)
            {
                if (col >= newWidth)
                {
                    continue;
                }

                newRow ??= new Dictionary<int, uint[]>();
                newRow[col] = seq;
            }

            newRowGraphemes[y] = newRow;
        }

        _rowGraphemes = newRowGraphemes;
        _previousRowGraphemes = new Dictionary<int, uint[]>?[newHeight];

        Width = newWidth;
        Height = newHeight;
        MarkFullDirty();
    }
    
    /// <summary>
    /// Dumps the current buffer contents to a human-readable string format.
    /// Includes dimensions and a visual representation with color information.
    /// </summary>
    public string DumpToString()
    {
        var sb = new System.Text.StringBuilder();
        
        // Header with size information
        sb.AppendLine("=== TERMINAL BUFFER DUMP ===");
        sb.AppendLine($"Dimensions: {Width} x {Height}");
        sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        
        // Horizontal ruler
        sb.AppendLine("    " + new string('-', Width));
        
        // Buffer content with row numbers
        for (var y = 0; y < Height; y++)
        {
            sb.Append($"{y,3}|");
            
            for (var x = 0; x < Width; x++)
            {
                var cell = GetCell(x, y);
                var ch = cell.Codepoint;

                // Make certain control characters visible
                if (ch is '\0' or < ' ')
                {
                    ch = '\u00B7'; // Middle dot for empty/control chars
                }

                if (System.Text.Rune.TryCreate(ch, out var rune))
                {
                    sb.Append(rune.ToString());
                }
                else
                {
                    sb.Append('?');
                }
            }
            
            sb.AppendLine("|");
        }
        
        // Bottom ruler
        sb.AppendLine("    " + new string('-', Width));
        
        // Color information section (only non-empty cells with non-default colors)
        sb.AppendLine();
        sb.AppendLine("=== COLOR INFORMATION ===");
        
        var hasColors = false;
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var cell = GetCell(x, y);
                
                // Skip cells with default colors (white on black) and empty characters
                if (cell.Codepoint == ' ' && cell.Foreground == Color.White && cell.Background == Color.Black)
                {
                    continue;
                }

                // Show cells with non-default styling
                if (cell.Foreground == Color.White && cell.Background == Color.Black)
                {
                    continue;
                }

                hasColors = true;
                var colorName = GetColorName(cell.Foreground);
                var bgColorName = GetColorName(cell.Background);
                    
                var displayChar = System.Text.Rune.TryCreate(cell.Codepoint, out var r) ? r.ToString() : "?";
                sb.AppendLine($"  [{x,3},{y,3}] '{displayChar}' " +
                              $"FG:{colorName} BG:{bgColorName}");
            }
        }
        
        if (!hasColors)
        {
            sb.AppendLine("  (All cells use default colors: White on Black)");
        }
        
        sb.AppendLine();
        sb.AppendLine("=== END DUMP ===");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Releases the unmanaged memory backing both buffers.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        
        if (_current != null)
        {
            NativeMemory.Free(_current);
            _current = null;
        }
        
        if (_previous != null)
        {
            NativeMemory.Free(_previous);
            _previous = null;
        }
        
        Width = 0;
        Height = 0;
        Capacity = 0;
    }
    
    // ──────────────────────────────────────────────────────────────────
    //  Private helpers
    // ──────────────────────────────────────────────────────────────────
    
    /// <summary>
    /// Allocates a zeroed unmanaged buffer with the given capacity in cell slots.
    /// </summary>
    private static Cell* AllocBuffer(int capacity)
    {
        return (Cell*)NativeMemory.AllocZeroed((nuint)capacity, (nuint)sizeof(Cell));
    }
    
    /// <summary>
    /// Fills the logical region (width × height) of a buffer with <see cref="Cell.Empty"/>.
    /// </summary>
    private static void FillEmpty(Cell* buffer, int width, int height)
    {
        new Span<Cell>(buffer, width * height).Fill(Cell.Empty);
    }
    
    /// <summary>
    /// Allocates a new buffer with <paramref name="newCapacity"/> slots, copies the overlapping
    /// region from <paramref name="old"/>, fills new cells with <see cref="Cell.Empty"/>,
    /// and frees the old block. Returns the new pointer.
    /// </summary>
    private static Cell* ResizeBuffer(
        Cell* old, int oldWidth,
        int newWidth, int newHeight, int newCapacity,
        int copyWidth, int copyHeight)
    {
        var dest = AllocBuffer(newCapacity);
        
        // Fill entire logical region with Cell.Empty first
        FillEmpty(dest, newWidth, newHeight);
        
        // Copy overlapping rows
        for (var y = 0; y < copyHeight; y++)
        {
            var src = new ReadOnlySpan<Cell>(old + y * oldWidth, copyWidth);
            var dst = new Span<Cell>(dest + y * newWidth, copyWidth);
            src.CopyTo(dst);
        }
        
        NativeMemory.Free(old);
        return dest;
    }
    
    /// <summary>
    /// Reshuffles rows in-place when the new dimensions fit within the existing capacity.
    /// Handles both width growth (bottom-up to avoid overwrites) and width shrinkage (top-down).
    /// Fills newly exposed cells/rows with <see cref="Cell.Empty"/>.
    /// </summary>
    private static void ReshuffleInPlace(Cell* buffer, int oldWidth, int oldHeight, int newWidth, int newHeight, int copyWidth, int copyHeight)
    {
        if (newWidth > oldWidth)
        {
            // Width grew: iterate bottom-up to avoid overwriting data that hasn't been moved yet.
            // Each row's start offset in the flat array shifts right (y * newWidth > y * oldWidth),
            // so we must process the last row first.
            for (var y = copyHeight - 1; y >= 0; y--)
            {
                var src = new ReadOnlySpan<Cell>(buffer + y * oldWidth, copyWidth);
                var dst = new Span<Cell>(buffer + y * newWidth, newWidth);
                
                // Copy the preserved columns (may overlap, so use Memmove semantics via CopyTo)
                // CopyTo handles overlapping regions correctly.
                src.CopyTo(dst);
                
                // Clear the new columns to the right
                if (newWidth > copyWidth)
                {
                    new Span<Cell>(buffer + y * newWidth + copyWidth, newWidth - copyWidth).Fill(Cell.Empty);
                }
            }
        }
        else if (newWidth < oldWidth)
        {
            // Width shrunk: iterate top-down (each row shifts left, no overlap conflict going forward)
            for (var y = 0; y < copyHeight; y++)
            {
                var src = new ReadOnlySpan<Cell>(buffer + y * oldWidth, copyWidth);
                var dst = new Span<Cell>(buffer + y * newWidth, copyWidth);
                src.CopyTo(dst);
            }
        }
        // else: width unchanged, rows stay in place — nothing to move
        
        // Clear any newly visible rows below the old content
        if (newHeight > oldHeight)
        {
            var newCellsStart = oldHeight * newWidth;
            var newCellsCount = (newHeight - oldHeight) * newWidth;
            new Span<Cell>(buffer + newCellsStart, newCellsCount).Fill(Cell.Empty);
        }
    }
    
    private void MarkFullDirty()
    {
        _dirtyRect = new DirtyRect 
        { 
            MinX = 0, 
            MinY = 0, 
            MaxX = Width - 1, 
            MaxY = Height - 1, 
            IsDirty = true 
        };
    }
    
    /// <summary>
    /// Helper to get a human-readable color name or hex value.
    /// </summary>
    private static string GetColorName(Color color)
    {
        // Check known colors
        if (color == Color.Black)
        {
            return "Black";
        }

        if (color == Color.White)
        {
            return "White";
        }

        if (color == Color.Red)
        {
            return "Red";
        }

        if (color == Color.Green)
        {
            return "Green";
        }

        if (color == Color.Blue)
        {
            return "Blue";
        }

        if (color == Color.Cyan)
        {
            return "Cyan";
        }

        if (color == Color.Magenta)
        {
            return "Magenta";
        }

        if (color == Color.Yellow)
        {
            return "Yellow";
        }

        if (color == Color.Gray)
        {
            return "Gray";
        }

        if (color == Color.DarkGray)
        {
            return "DarkGray";
        }

        // Return hex for custom colors
        return color.ToHex();
    }
    
    /// <summary>
    /// Enumerates the row indices that intersect the buffer's dirty rectangle.
    /// Used by the renderer's row-level shaped path (<see cref="IShapedRunSink"/>) to walk
    /// only the rows that may need re-rendering. Returns nothing when nothing is dirty.
    /// </summary>
    public DirtyRowEnumerator GetDirtyRows() => new(_dirtyRect);

    /// <summary>
    /// Reads the current dirty rectangle in cell coordinates. Returns <see langword="false"/>
    /// and writes <see langword="default"/> when no cells have been marked dirty.
    /// </summary>
    /// <param name="rect">Receives the dirty region as an inclusive-min, exclusive-max <see cref="Rect"/>.</param>
    public bool TryGetDirtyRect(out Rect rect)
    {
        if (!_dirtyRect.IsDirty)
        {
            rect = default;
            return false;
        }

        rect = _dirtyRect.ToRect();
        return true;
    }

    /// <summary>
    /// Zero-allocation enumerator over the dirty row range.
    /// </summary>
    public ref struct DirtyRowEnumerator
    {
        private readonly int _maxY;
        private readonly bool _empty;
        private int _y;

        internal DirtyRowEnumerator(DirtyRect rect)
        {
            if (!rect.IsDirty)
            {
                _empty = true;
                _maxY = 0;
                _y = 0;
            }
            else
            {
                _empty = false;
                _maxY = rect.MaxY;
                _y = rect.MinY - 1;
            }

            Current = 0;
        }

        /// <summary>The current dirty row index.</summary>
        public int Current { get; private set; }

        /// <summary>Advances to the next dirty row; returns false past the dirty range.</summary>
        public bool MoveNext()
        {
            if (_empty)
            {
                return false;
            }

            _y++;
            if (_y > _maxY)
            {
                return false;
            }

            Current = _y;
            return true;
        }

        /// <summary>Enables foreach.</summary>
        public DirtyRowEnumerator GetEnumerator() => this;
    }

    /// <summary>
    /// Zero-allocation struct enumerator for cell differences.
    /// Uses unmanaged pointers confined within this ref struct.
    /// </summary>
    public ref struct CellDiffEnumerator
    {
        private readonly Cell* _current;
        private readonly Cell* _previous;
        private readonly Dictionary<int, uint[]>?[] _currentGraphemes;
        private readonly Dictionary<int, uint[]>?[] _previousGraphemes;
        private readonly int _width;
        private readonly Rect _dirtyRegion;
        private int _x;
        private int _y;

        internal CellDiffEnumerator(CellBuffer buffer)
        {
            _current = buffer._current;
            _previous = buffer._previous;
            _currentGraphemes = buffer._rowGraphemes;
            _previousGraphemes = buffer._previousRowGraphemes;
            _width = buffer.Width;
            _dirtyRegion = buffer._dirtyRect.IsDirty
                ? buffer._dirtyRect.ToRect()
                : new Rect(0, 0, 0, 0);
            _x = _dirtyRegion.X - 1;
            _y = _dirtyRegion.Y;
            Current = default;
        }

        /// <summary>Gets the current cell change.</summary>
        public CellChange Current { get; private set; }

        /// <summary>Moves to the next changed cell.</summary>
        public bool MoveNext()
        {
            while (_y < _dirtyRegion.Bottom)
            {
                _x++;
                if (_x >= _dirtyRegion.Right)
                {
                    _x = _dirtyRegion.X - 1;  // Set to -1 so next _x++ makes it 0
                    _y++;
                    continue;
                }

                var index = _y * _width + _x;
                var cellChanged = _current[index] != _previous[index];

                // Grapheme cells need an array-level comparison even when the Cell value
                // matches — the lead codepoint and colors may be identical but the cluster
                // sequence (combining marks, ZWJ continuation) may have changed.
                if (!cellChanged && (_current[index].Flags & CellFlags.HasGrapheme) != 0)
                {
                    var currSeq = _currentGraphemes[_y]?.GetValueOrDefault(_x);
                    var prevSeq = _previousGraphemes[_y]?.GetValueOrDefault(_x);
                    if (!SequencesEqual(currSeq, prevSeq))
                    {
                        cellChanged = true;
                    }
                }

                if (cellChanged)
                {
                    // Skip wide-character trailing cells — they're placeholders, and the
                    // leading cell at (x-1, y) carried the actual codepoint and advanced
                    // the cursor by 2 already. Emitting them would corrupt the grid.
                    if ((_current[index].Flags & CellFlags.WideTrail) != 0)
                    {
                        continue;
                    }

                    Current = new CellChange(_x, _y, _current[index]);
                    return true;
                }
            }
            return false;
        }

        private static bool SequencesEqual(uint[]? a, uint[]? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            if (a.Length != b.Length) return false;
            for (var i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        /// <summary>Gets the enumerator (for foreach support).</summary>
        public CellDiffEnumerator GetEnumerator() => this;
    }
}
