using System.Windows.Markup;
using TerminalNinja.Aot;
using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// A layout container that arranges child elements in rows and columns.
/// Supports attached properties Grid.Row, Grid.Column, Grid.RowSpan, and Grid.ColumnSpan.
/// </summary>
[ContentProperty("Children")]
[RuntimeNameProperty("Name")]
public sealed class Grid : Panel, IChildContainer
{
    private readonly List<RowDefinition> _rowDefinitions = new();
    private readonly List<ColumnDefinition> _columnDefinitions = new();
    
    /// <summary>
    /// Gets the collection of row definitions for this grid.
    /// If empty, the grid has a single row that fills available height.
    /// </summary>
    public IList<RowDefinition> RowDefinitions => _rowDefinitions;
    
    /// <summary>
    /// Gets the collection of column definitions for this grid.
    /// If empty, the grid has a single column that fills available width.
    /// </summary>
    public IList<ColumnDefinition> ColumnDefinitions => _columnDefinitions;
    
    #region Attached Properties using AttachedPropertyStore
    
    private static readonly AttachedPropertyKey RowKey = new(typeof(Grid), "Row");
    private static readonly AttachedPropertyKey ColumnKey = new(typeof(Grid), "Column");
    private static readonly AttachedPropertyKey RowSpanKey = new(typeof(Grid), "RowSpan");
    private static readonly AttachedPropertyKey ColumnSpanKey = new(typeof(Grid), "ColumnSpan");
    
    /// <summary>
    /// Gets the Grid.Row attached property value for a control.
    /// </summary>
    public static int GetRow(object control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return AttachedPropertyStore.TryGetValue<int>(control, RowKey, out var value) ? value : 0;
    }
    
    /// <summary>
    /// Sets the Grid.Row attached property value for a control.
    /// </summary>
    public static void SetRow(object control, int value)
    {
        ArgumentNullException.ThrowIfNull(control);
        AttachedPropertyStore.SetValue(control, RowKey, Math.Max(0, value));
    }
    
    /// <summary>
    /// Gets the Grid.Column attached property value for a control.
    /// </summary>
    public static int GetColumn(object control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return AttachedPropertyStore.TryGetValue<int>(control, ColumnKey, out var value) ? value : 0;
    }
    
    /// <summary>
    /// Sets the Grid.Column attached property value for a control.
    /// </summary>
    public static void SetColumn(object control, int value)
    {
        ArgumentNullException.ThrowIfNull(control);
        AttachedPropertyStore.SetValue(control, ColumnKey, Math.Max(0, value));
    }
    
    /// <summary>
    /// Gets the Grid.RowSpan attached property value for a control.
    /// </summary>
    public static int GetRowSpan(object control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return AttachedPropertyStore.TryGetValue<int>(control, RowSpanKey, out var value) ? Math.Max(1, value) : 1;
    }
    
    /// <summary>
    /// Sets the Grid.RowSpan attached property value for a control.
    /// </summary>
    public static void SetRowSpan(object control, int value)
    {
        ArgumentNullException.ThrowIfNull(control);
        AttachedPropertyStore.SetValue(control, RowSpanKey, Math.Max(1, value));
    }
    
    /// <summary>
    /// Gets the Grid.ColumnSpan attached property value for a control.
    /// </summary>
    public static int GetColumnSpan(object control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return AttachedPropertyStore.TryGetValue<int>(control, ColumnSpanKey, out var value) ? Math.Max(1, value) : 1;
    }
    
    /// <summary>
    /// Sets the Grid.ColumnSpan attached property value for a control.
    /// </summary>
    public static void SetColumnSpan(object control, int value)
    {
        ArgumentNullException.ThrowIfNull(control);
        AttachedPropertyStore.SetValue(control, ColumnSpanKey, Math.Max(1, value));
    }
    
    #endregion
    
    /// <summary>
    /// Returns the preferred size of this grid.
    /// </summary>
    public override Size2D GetPreferredSize(Rect parent)
    {
        // For now, return the parent size (grid fills available space)
        return new Size2D(parent.Width, parent.Height);
    }
    
    /// <summary>
    /// Calculates bounds (Grid fills the parent).
    /// </summary>
    public override Rect CalculateBounds(Rect parent) => parent;
    
    /// <summary>
    /// Renders the grid and all its children.
    /// </summary>
    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        if (Children.Count == 0) return;
        
        // Ensure we have at least one row and column definition
        var rows = _rowDefinitions.Count > 0 ? _rowDefinitions : [new RowDefinition()];
        var cols = _columnDefinitions.Count > 0 ? _columnDefinitions : [new ColumnDefinition()];
        
        // Calculate row heights and column widths
        CalculateSizes(rows, bounds.Height, r => r.Height, (r, s) => r.ActualHeight = s, r => r.MinHeight, r => r.MaxHeight);
        CalculateSizes(cols, bounds.Width, c => c.Width, (c, s) => c.ActualWidth = s, c => c.MinWidth, c => c.MaxWidth);
        
        // Calculate offsets
        CalculateOffsets(rows, bounds.Y, (r, o) => r.Offset = o, r => r.ActualHeight);
        CalculateOffsets(cols, bounds.X, (c, o) => c.Offset = o, c => c.ActualWidth);
        
        // Render each child
        foreach (var child in Children)
        {
            var row = Math.Min(GetRow(child), rows.Count - 1);
            var col = Math.Min(GetColumn(child), cols.Count - 1);
            var rowSpan = Math.Min(GetRowSpan(child), rows.Count - row);
            var colSpan = Math.Min(GetColumnSpan(child), cols.Count - col);
            
            var cellBounds = GetCellBounds(rows, cols, row, col, rowSpan, colSpan, bounds);
            child.Render(buffer, cellBounds);
        }
    }
    
    /// <inheritdoc />
    public IEnumerable<(IControl Child, Rect ChildParentBounds)> GetChildrenWithBounds(Rect myBounds)
    {
        if (Children.Count == 0) yield break;

        var rows = _rowDefinitions.Count > 0 ? _rowDefinitions : [new RowDefinition()];
        var cols = _columnDefinitions.Count > 0 ? _columnDefinitions : [new ColumnDefinition()];

        CalculateSizes(rows, myBounds.Height, r => r.Height, (r, s) => r.ActualHeight = s, r => r.MinHeight, r => r.MaxHeight);
        CalculateSizes(cols, myBounds.Width, c => c.Width, (c, s) => c.ActualWidth = s, c => c.MinWidth, c => c.MaxWidth);
        CalculateOffsets(rows, myBounds.Y, (r, o) => r.Offset = o, r => r.ActualHeight);
        CalculateOffsets(cols, myBounds.X, (c, o) => c.Offset = o, c => c.ActualWidth);

        foreach (var child in Children)
        {
            var row = Math.Min(GetRow(child), rows.Count - 1);
            var col = Math.Min(GetColumn(child), cols.Count - 1);
            var rowSpan = Math.Min(GetRowSpan(child), rows.Count - row);
            var colSpan = Math.Min(GetColumnSpan(child), cols.Count - col);
            yield return (child, GetCellBounds(rows, cols, row, col, rowSpan, colSpan, myBounds));
        }
    }

    /// <summary>
    /// Calculates sizes for rows or columns based on their GridLength definitions.
    /// Uses a three-pass algorithm: Pixel → Auto → Star.
    /// </summary>
    private static void CalculateSizes<T>(
        IList<T> definitions,
        int availableSize,
        Func<T, GridLength> getLength,
        Action<T, int> setActualSize,
        Func<T, int> getMin,
        Func<T, int> getMax)
    {
        var remaining = availableSize;
        var totalStarWeight = 0.0;
        
        // First pass: allocate Pixel sizes
        foreach (var def in definitions)
        {
            var length = getLength(def);
            if (length.IsAbsolute)
            {
                var size = Math.Clamp((int)length.Value, getMin(def), getMax(def));
                size = Math.Min(size, remaining);
                setActualSize(def, size);
                remaining -= size;
            }
            else if (length.IsStar)
            {
                totalStarWeight += length.Value;
                setActualSize(def, 0); // Will be set in third pass
            }
            else // Auto
            {
                // For Auto, we'd ideally measure children, but for simplicity
                // we'll treat Auto as min size for now
                var size = Math.Clamp(getMin(def), getMin(def), getMax(def));
                size = Math.Min(size, remaining);
                setActualSize(def, size);
                remaining -= size;
            }
        }
        
        // Second pass: For Auto rows/columns, we should measure children
        // This is simplified - in a full implementation we'd need to measure children
        // For now, Auto is treated as minimum size (handled above)
        
        // Third pass: distribute remaining space to Star definitions
        if (totalStarWeight > 0 && remaining > 0)
        {
            var sizePerStar = remaining / totalStarWeight;
            var allocated = 0;
            var starDefs = definitions.Where(d => getLength(d).IsStar).ToList();
            
            for (var i = 0; i < starDefs.Count; i++)
            {
                var def = starDefs[i];
                var length = getLength(def);
                int size;
                
                if (i == starDefs.Count - 1)
                {
                    // Last star gets remaining to avoid rounding errors
                    size = remaining - allocated;
                }
                else
                {
                    size = (int)(length.Value * sizePerStar);
                }
                
                size = Math.Clamp(size, getMin(def), getMax(def));
                setActualSize(def, size);
                allocated += size;
            }
        }
    }
    
    /// <summary>
    /// Calculates cumulative offsets for rows or columns.
    /// </summary>
    private static void CalculateOffsets<T>(
        IList<T> definitions,
        int startOffset,
        Action<T, int> setOffset,
        Func<T, int> getActualSize)
    {
        var offset = startOffset;
        foreach (var def in definitions)
        {
            setOffset(def, offset);
            offset += getActualSize(def);
        }
    }
    
    /// <summary>
    /// Gets the bounds for a cell (or spanned cells) in the grid.
    /// </summary>
    private static Rect GetCellBounds(
        IList<RowDefinition> rows,
        IList<ColumnDefinition> cols,
        int row, int col,
        int rowSpan, int colSpan,
        Rect gridBounds)
    {
        var x = cols[col].Offset;
        var y = rows[row].Offset;
        
        var width = 0;
        for (var c = col; c < col + colSpan && c < cols.Count; c++)
            width += cols[c].ActualWidth;
        
        var height = 0;
        for (var r = row; r < row + rowSpan && r < rows.Count; r++)
            height += rows[r].ActualHeight;
        
        return new Rect(x, y, width, height);
    }
}
