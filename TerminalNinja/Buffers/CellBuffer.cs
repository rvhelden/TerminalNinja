using System.Runtime.CompilerServices;
using TerminalNinja.Primitives;

namespace TerminalNinja.Buffers;

/// <summary>
/// Represents a change to a single cell in the buffer.
/// </summary>
public readonly record struct CellChange(int X, int Y, Cell Cell);

/// <summary>
/// A double-buffered cell buffer with dirty tracking for optimized rendering.
/// Zero-allocation diffing using struct enumerators.
/// </summary>
public sealed class CellBuffer
{
    private Cell[] _current;      // What we're rendering to
    private Cell[] _previous;     // What's currently on screen
    private DirtyRect _dirtyRect;
    
    /// <summary>Gets the width of the buffer in cells.</summary>
    public int Width { get; private set; }
    
    /// <summary>Gets the height of the buffer in cells.</summary>
    public int Height { get; private set; }
    
    /// <summary>
    /// Creates a new cell buffer with the specified dimensions.
    /// </summary>
    public CellBuffer(int width, int height)
    {
        Width = width;
        Height = height;
        var size = width * height;
        _current = new Cell[size];
        _previous = new Cell[size];
        
        // Initialize both buffers to Cell.Empty so they start in sync
        _current.AsSpan().Fill(Cell.Empty);
        _previous.AsSpan().Fill(Cell.Empty);
        
        // Clear() will set dirty rect for the entire buffer
        Clear();
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
        if (!IsInBounds(x, y)) return;
        
        var index = Index(x, y);
        if (_current[index] != cell)
        {
            _current[index] = cell;
            _dirtyRect.Expand(x, y);
        }
    }
    
    /// <summary>
    /// Gets the cell at the specified position.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cell GetCell(int x, int y)
    {
        if (!IsInBounds(x, y)) return Cell.Empty;
        return _current[Index(x, y)];
    }
    
    /// <summary>
    /// Sets a cell with individual character and colors.
    /// When <paramref name="bg"/> is transparent the existing cell's background is preserved.
    /// </summary>
    public void SetChar(int x, int y, char c, Color fg, Color bg)
    {
        if (bg.IsTransparent && IsInBounds(x, y))
            bg = _current[Index(x, y)].Background;
        SetCell(x, y, new Cell(c, fg, bg));
    }
    
    /// <summary>
    /// Clears the entire buffer to empty cells.
    /// </summary>
    public void Clear()
    {
        _current.AsSpan().Fill(Cell.Empty);
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
    /// Fills a rectangular region with the specified cell.
    /// </summary>
    public void FillRect(Rect bounds, Cell cell)
    {
        var clipped = bounds.Intersect(new Rect(0, 0, Width, Height));
        if (clipped.Width <= 0 || clipped.Height <= 0) return;
        
        for (var y = clipped.Y; y < clipped.Bottom; y++)
        {
            var rowStart = Index(clipped.X, y);
            _current.AsSpan(rowStart, clipped.Width).Fill(cell);
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
        (_current, _previous) = (_previous, _current);
        _dirtyRect.Reset();
    }
    
    /// <summary>
    /// Resizes the buffer to new dimensions.
    /// </summary>
    public void Resize(int newWidth, int newHeight)
    {
        if (newWidth == Width && newHeight == Height) return;
        
        var newSize = newWidth * newHeight;
        var newCurrent = new Cell[newSize];
        var newPrevious = new Cell[newSize];
        
        newCurrent.AsSpan().Fill(Cell.Empty);
        newPrevious.AsSpan().Fill(Cell.Empty);
        
        _current = newCurrent;
        _previous = newPrevious;
        Width = newWidth;
        Height = newHeight;
        
        _dirtyRect = new DirtyRect 
        { 
            MinX = 0, 
            MinY = 0, 
            MaxX = newWidth - 1, 
            MaxY = newHeight - 1, 
            IsDirty = true 
        };
    }
    
    /// <summary>
    /// Dumps the current buffer contents to a human-readable string format.
    /// Includes dimensions, and a visual representation with color information.
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
        for (int y = 0; y < Height; y++)
        {
            sb.Append($"{y,3}|");
            
            for (int x = 0; x < Width; x++)
            {
                var cell = GetCell(x, y);
                var ch = cell.Character;
                
                // Make certain control characters visible
                if (ch == '\0' || ch < ' ')
                    ch = '·'; // Middle dot for empty/control chars
                    
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
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var cell = GetCell(x, y);
                
                // Skip cells with default colors (white on black) and empty characters
                if (cell.Character == ' ' && 
                    cell.Foreground == Color.White && 
                    cell.Background == Color.Black)
                    continue;
                
                // Show cells with non-default styling
                if (cell.Foreground != Color.White || 
                    cell.Background != Color.Black)
                {
                    hasColors = true;
                    var colorName = GetColorName(cell.Foreground);
                    var bgColorName = GetColorName(cell.Background);
                    
                    sb.AppendLine($"  [{x,3},{y,3}] '{cell.Character}' " +
                                  $"FG:{colorName} BG:{bgColorName}");
                }
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
    /// Helper to get a human-readable color name or hex value.
    /// </summary>
    private static string GetColorName(Color color)
    {
        // Check known colors
        if (color == Color.Black) return "Black";
        if (color == Color.White) return "White";
        if (color == Color.Red) return "Red";
        if (color == Color.Green) return "Green";
        if (color == Color.Blue) return "Blue";
        if (color == Color.Cyan) return "Cyan";
        if (color == Color.Magenta) return "Magenta";
        if (color == Color.Yellow) return "Yellow";
        if (color == Color.Gray) return "Gray";
        if (color == Color.DarkGray) return "DarkGray";
        
        // Return hex for custom colors
        return color.ToHex();
    }
    
    /// <summary>
    /// Zero-allocation struct enumerator for cell differences.
    /// </summary>
    public ref struct CellDiffEnumerator
    {
        private readonly Cell[] _current;
        private readonly Cell[] _previous;
        private readonly int _width;
        private readonly Rect _dirtyRegion;
        private int _x;
        private int _y;
        private CellChange _currentChange;
        
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
            _currentChange = default;
        }
        
        /// <summary>Gets the current cell change.</summary>
        public CellChange Current => _currentChange;
        
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
                    _currentChange = new CellChange(_x, _y, _current[index]);
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>Gets the enumerator (for foreach support).</summary>
        public CellDiffEnumerator GetEnumerator() => this;
    }
}
