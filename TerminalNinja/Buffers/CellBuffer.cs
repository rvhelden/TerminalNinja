using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TerminalNinja.Primitives;

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
    /// Sets a cell at the specified position.
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
    /// Sets a cell with individual character and colors.
    /// When <paramref name="bg"/> is transparent the existing cell's background is preserved.
    /// </summary>
    public void SetChar(int x, int y, char c, Color fg, Color bg)
    {
        if (bg.IsTransparent && IsInBounds(x, y))
        {
            bg = _current[Index(x, y)].Background;
        }

        SetCell(x, y, new Cell(c, fg, bg));
    }
    
    /// <summary>
    /// Sets a cell with individual character, colors, and text decorations.
    /// When <paramref name="bg"/> is transparent the existing cell's background is preserved.
    /// </summary>
    public void SetChar(int x, int y, char c, Color fg, Color bg, TextDecorations decorations)
    {
        if (bg.IsTransparent && IsInBounds(x, y))
        {
            bg = _current[Index(x, y)].Background;
        }

        SetCell(x, y, new Cell(c, fg, bg, decorations));
    }
    
    /// <summary>
    /// Clears the entire buffer to empty cells.
    /// </summary>
    public void Clear()
    {
        FillEmpty(_current, Width, Height);
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
                var ch = cell.Character;
                
                // Make certain control characters visible
                if (ch == '\0' || ch < ' ')
                {
                    ch = '\u00B7'; // Middle dot for empty/control chars
                }

                sb.Append(ch);
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
                if (cell.Character == ' ' && cell.Foreground == Color.White && cell.Background == Color.Black)
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
                    
                sb.AppendLine($"  [{x,3},{y,3}] '{cell.Character}' " +
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
    /// Zero-allocation struct enumerator for cell differences.
    /// Uses unmanaged pointers confined within this ref struct.
    /// </summary>
    public ref struct CellDiffEnumerator
    {
        private readonly Cell* _current;
        private readonly Cell* _previous;
        private readonly int _width;
        private readonly Rect _dirtyRegion;
        private int _x;
        private int _y;

        internal CellDiffEnumerator(CellBuffer buffer)
        {
            _current = buffer._current;
            _previous = buffer._previous;
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
                if (_current[index] != _previous[index])
                {
                    Current = new CellChange(_x, _y, _current[index]);
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>Gets the enumerator (for foreach support).</summary>
        public CellDiffEnumerator GetEnumerator() => this;
    }
}
