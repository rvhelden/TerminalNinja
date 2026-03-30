using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// A layout container that arranges child elements in rows and columns.
/// Supports attached properties Grid.Row, Grid.Column, Grid.RowSpan, and Grid.ColumnSpan.
/// </summary>
[ContentProperty("Children")]
[RuntimeNameProperty("Name")]
public sealed class Grid : Panel
{
    public Grid()
    {
        DefaultStyleKey = typeof(Grid);
    }

    // ─── Attached Dependency Properties ──────────────────────────────

    public static readonly DependencyProperty RowProperty =
        DependencyProperty.RegisterAttached("Row", typeof(int), typeof(Grid),
            new PropertyMetadata(0));

    public static readonly DependencyProperty ColumnProperty =
        DependencyProperty.RegisterAttached("Column", typeof(int), typeof(Grid),
            new PropertyMetadata(0));

    public static readonly DependencyProperty RowSpanProperty =
        DependencyProperty.RegisterAttached("RowSpan", typeof(int), typeof(Grid),
            new PropertyMetadata(1));

    public static readonly DependencyProperty ColumnSpanProperty =
        DependencyProperty.RegisterAttached("ColumnSpan", typeof(int), typeof(Grid),
            new PropertyMetadata(1));

    // ─── Spacing Dependency Properties ───────────────────────────────

    public static readonly DependencyProperty RowSpacingProperty =
        DependencyProperty.Register(nameof(RowSpacing), typeof(int), typeof(Grid),
            new FrameworkPropertyMetadata(0, affectsRender: true));

    public static readonly DependencyProperty ColumnSpacingProperty =
        DependencyProperty.Register(nameof(ColumnSpacing), typeof(int), typeof(Grid),
            new FrameworkPropertyMetadata(0, affectsRender: true));

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

    /// <summary>
    /// Gets or sets the amount of space between rows in the grid.
    /// Spacing is inserted between rows only (not before the first or after the last).
    /// </summary>
    public int RowSpacing
    {
        get => (int)GetValue(RowSpacingProperty)!;
        set => SetValue(RowSpacingProperty, Math.Max(0, value));
    }

    /// <summary>
    /// Gets or sets the amount of space between columns in the grid.
    /// Spacing is inserted between columns only (not before the first or after the last).
    /// </summary>
    public int ColumnSpacing
    {
        get => (int)GetValue(ColumnSpacingProperty)!;
        set => SetValue(ColumnSpacingProperty, Math.Max(0, value));
    }

    /// <summary>
    /// Shorthand for defining rows as a space-separated list of GridLength values.
    /// Examples: "5 * auto", "15 * auto", "* 2*"
    /// Replaces any existing RowDefinitions.
    /// </summary>
    public string? Rows
    {
        set
        {
            _rowDefinitions.Clear();
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            foreach (var range in value.AsSpan().Split(' '))
            {
                var token = value.AsSpan()[range].Trim();
                if (!token.IsEmpty)
                {
                    _rowDefinitions.Add(new RowDefinition { Height = GridLength.Parse(token) });
                }
            }
        }
    }

    /// <summary>
    /// Shorthand for defining columns as a space-separated list of GridLength values.
    /// Examples: "* * auto", "* 35 24", "2* *"
    /// Replaces any existing ColumnDefinitions.
    /// </summary>
    public string? Columns
    {
        set
        {
            _columnDefinitions.Clear();
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            foreach (var range in value.AsSpan().Split(' '))
            {
                var token = value.AsSpan()[range].Trim();
                if (!token.IsEmpty)
                {
                    _columnDefinitions.Add(new ColumnDefinition { Width = GridLength.Parse(token) });
                }
            }
        }
    }

    // ─── Attached Property Accessors ─────────────────────────────────

    /// <summary>
    /// Gets the Grid.Row attached property value for a control.
    /// </summary>
    public static int GetRow(DependencyObject control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return (int)control.GetValue(RowProperty)!;
    }

    /// <summary>
    /// Sets the Grid.Row attached property value for a control.
    /// </summary>
    public static void SetRow(DependencyObject control, int value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(RowProperty, Math.Max(0, value));
    }

    /// <summary>
    /// Gets the Grid.Column attached property value for a control.
    /// </summary>
    public static int GetColumn(DependencyObject control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return (int)control.GetValue(ColumnProperty)!;
    }

    /// <summary>
    /// Sets the Grid.Column attached property value for a control.
    /// </summary>
    public static void SetColumn(DependencyObject control, int value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(ColumnProperty, Math.Max(0, value));
    }

    /// <summary>
    /// Gets the Grid.RowSpan attached property value for a control.
    /// </summary>
    public static int GetRowSpan(DependencyObject control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return Math.Max(1, (int)control.GetValue(RowSpanProperty)!);
    }

    /// <summary>
    /// Sets the Grid.RowSpan attached property value for a control.
    /// </summary>
    public static void SetRowSpan(DependencyObject control, int value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(RowSpanProperty, Math.Max(1, value));
    }

    /// <summary>
    /// Gets the Grid.ColumnSpan attached property value for a control.
    /// </summary>
    public static int GetColumnSpan(DependencyObject control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return Math.Max(1, (int)control.GetValue(ColumnSpanProperty)!);
    }

    /// <summary>
    /// Sets the Grid.ColumnSpan attached property value for a control.
    /// </summary>
    public static void SetColumnSpan(DependencyObject control, int value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(ColumnSpanProperty, Math.Max(1, value));
    }

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
    public override Rect CalculateBounds(Rect parent)
    {
        return parent;
    }

    /// <summary>
    /// Renders the grid and all its children.
    /// </summary>
    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        if (Children.Count == 0)
        {
            return;
        }

        var rowSpacing = RowSpacing;
        var columnSpacing = ColumnSpacing;

        // Ensure we have at least one row and column definition
        var rows = _rowDefinitions.Count > 0 ? _rowDefinitions : [new RowDefinition()];
        var cols = _columnDefinitions.Count > 0 ? _columnDefinitions : [new ColumnDefinition()];

        // Calculate row heights and column widths (spacing reduces available space)
        CalculateSizes(rows, bounds.Height, r => r.Height, (r, s) => r.ActualHeight = s, r => r.MinHeight, r => r.MaxHeight, rowSpacing);
        CalculateSizes(cols, bounds.Width, c => c.Width, (c, s) => c.ActualWidth = s, c => c.MinWidth, c => c.MaxWidth, columnSpacing);

        // Calculate offsets (spacing inserted between items)
        CalculateOffsets(rows, bounds.Y, (r, o) => r.Offset = o, r => r.ActualHeight, rowSpacing);
        CalculateOffsets(cols, bounds.X, (c, o) => c.Offset = o, c => c.ActualWidth, columnSpacing);

        // Render each child
        foreach (var child in Children)
        {
            var row = Math.Min(GetRow(child), rows.Count - 1);
            var col = Math.Min(GetColumn(child), cols.Count - 1);
            var rowSpan = Math.Min(GetRowSpan(child), rows.Count - row);
            var colSpan = Math.Min(GetColumnSpan(child), cols.Count - col);

            var cellBounds = GetCellBounds(rows, cols, row, col, rowSpan, colSpan, bounds, rowSpacing, columnSpacing);
            child.Render(buffer, cellBounds);
        }
    }

    /// <inheritdoc />
    public override IEnumerable<(Visual Child, Rect ChildParentBounds)> GetChildrenWithBounds(Rect myBounds)
    {
        if (Children.Count == 0)
        {
            yield break;
        }

        var rowSpacing = RowSpacing;
        var columnSpacing = ColumnSpacing;

        var rows = _rowDefinitions.Count > 0 ? _rowDefinitions : [new RowDefinition()];
        var cols = _columnDefinitions.Count > 0 ? _columnDefinitions : [new ColumnDefinition()];

        CalculateSizes(rows, myBounds.Height, r => r.Height, (r, s) => r.ActualHeight = s, r => r.MinHeight, r => r.MaxHeight, rowSpacing);
        CalculateSizes(cols, myBounds.Width, c => c.Width, (c, s) => c.ActualWidth = s, c => c.MinWidth, c => c.MaxWidth, columnSpacing);
        CalculateOffsets(rows, myBounds.Y, (r, o) => r.Offset = o, r => r.ActualHeight, rowSpacing);
        CalculateOffsets(cols, myBounds.X, (c, o) => c.Offset = o, c => c.ActualWidth, columnSpacing);

        foreach (var child in Children)
        {
            var row = Math.Min(GetRow(child), rows.Count - 1);
            var col = Math.Min(GetColumn(child), cols.Count - 1);
            var rowSpan = Math.Min(GetRowSpan(child), rows.Count - row);
            var colSpan = Math.Min(GetColumnSpan(child), cols.Count - col);
            yield return (child, GetCellBounds(rows, cols, row, col, rowSpan, colSpan, myBounds, rowSpacing, columnSpacing));
        }
    }

    /// <summary>
    /// Calculates sizes for rows or columns based on their GridLength definitions.
    /// Uses a three-pass algorithm: Pixel -> Auto -> Star.
    /// </summary>
    private static void CalculateSizes<T>(IList<T> definitions, int availableSize, Func<T, GridLength> getLength, Action<T, int> setActualSize, Func<T, int> getMin,
        Func<T, int> getMax, int spacing = 0)
    {
        // Subtract total spacing from available size (gaps between items only)
        var totalSpacing = definitions.Count > 1 ? spacing * (definitions.Count - 1) : 0;
        var remaining = Math.Max(0, availableSize - totalSpacing);
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
    /// Calculates cumulative offsets for rows or columns, inserting spacing between items.
    /// </summary>
    private static void CalculateOffsets<T>(
        IList<T> definitions,
        int startOffset,
        Action<T, int> setOffset,
        Func<T, int> getActualSize,
        int spacing = 0)
    {
        var offset = startOffset;
        for (var i = 0; i < definitions.Count; i++)
        {
            var def = definitions[i];
            setOffset(def, offset);
            offset += getActualSize(def);
            // Add spacing between items (not after the last)
            if (i < definitions.Count - 1)
            {
                offset += spacing;
            }
        }
    }

    /// <summary>
    /// Gets the bounds for a cell (or spanned cells) in the grid,
    /// including any inter-cell spacing within the spanned range.
    /// </summary>
    private static Rect GetCellBounds(
        IList<RowDefinition> rows,
        IList<ColumnDefinition> cols,
        int row, int col,
        int rowSpan, int colSpan,
        Rect gridBounds,
        int rowSpacing = 0,
        int columnSpacing = 0)
    {
        var x = cols[col].Offset;
        var y = rows[row].Offset;
        
        var width = 0;
        var spannedCols = 0;
        for (var c = col; c < col + colSpan && c < cols.Count; c++)
        {
            width += cols[c].ActualWidth;
            spannedCols++;
        }
        // Include spacing between spanned columns
        if (spannedCols > 1)
        {
            width += columnSpacing * (spannedCols - 1);
        }

        var height = 0;
        var spannedRows = 0;
        for (var r = row; r < row + rowSpan && r < rows.Count; r++)
        {
            height += rows[r].ActualHeight;
            spannedRows++;
        }
        // Include spacing between spanned rows
        if (spannedRows > 1)
        {
            height += rowSpacing * (spannedRows - 1);
        }

        return new Rect(x, y, width, height);
    }
}