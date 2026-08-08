using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// A panel that lays its children out in equally sized cells, filling row by row.
/// <see cref="Rows"/> and <see cref="Columns"/> are derived from the child count when left at
/// their default of zero.
/// </summary>
/// <remarks>
/// "Equal" has to mean something exact in a terminal: cells are integers, so a 10-cell panel
/// across 3 columns cannot give each column 3⅓. The remainder is handed out one cell at a time to
/// the leading columns (4, 3, 3) rather than truncated, so the column widths always sum to the
/// panel width and no cell is lost or drawn twice. When there are more columns than cells the
/// trailing columns legitimately get zero width and their children are skipped — the panel
/// renders as much as fits instead of rounding everything to zero and rendering nothing.
/// </remarks>
[ContentProperty("Children")]
[RuntimeNameProperty("Name")]
public class UniformGrid : Panel
{
    public UniformGrid()
    {
        DefaultStyleKey = typeof(UniformGrid);
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty RowsProperty =
        DependencyProperty.Register(nameof(Rows), typeof(int), typeof(UniformGrid),
            new FrameworkPropertyMetadata(0, affectsRender: true));

    public static readonly DependencyProperty ColumnsProperty =
        DependencyProperty.Register(nameof(Columns), typeof(int), typeof(UniformGrid),
            new FrameworkPropertyMetadata(0, affectsRender: true));

    /// <summary>
    /// Gets or sets the number of rows. Zero (the default) derives the row count from
    /// <see cref="Columns"/> and the number of non-collapsed children.
    /// </summary>
    public int Rows
    {
        get => (int)GetValue(RowsProperty)!;
        set => SetValue(RowsProperty, Math.Max(0, value));
    }

    /// <summary>
    /// Gets or sets the number of columns. Zero (the default) derives the column count from
    /// <see cref="Rows"/> and the number of non-collapsed children.
    /// </summary>
    public int Columns
    {
        get => (int)GetValue(ColumnsProperty)!;
        set => SetValue(ColumnsProperty, Math.Max(0, value));
    }

    /// <summary>
    /// Returns the preferred size of this panel: the largest child's preferred size multiplied
    /// out across the resolved grid shape.
    /// </summary>
    public override Size2D GetPreferredSize(Rect parent)
    {
        if (Children.Count == 0)
        {
            return new Size2D(0, 0);
        }

        var cellCount = 0;
        var maxWidth = 0;
        var maxHeight = 0;

        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            cellCount++;
            var preferred = child.GetPreferredSize(parent);
            var margin = child is FrameworkElement fe ? fe.Margin : new Thickness(0);
            maxWidth = Math.Max(maxWidth, preferred.Width + margin.HorizontalTotal);
            maxHeight = Math.Max(maxHeight, preferred.Height + margin.VerticalTotal);
        }

        if (cellCount == 0)
        {
            return new Size2D(0, 0);
        }

        var (rows, columns) = ResolveShape(cellCount);
        return new Size2D(maxWidth * columns, maxHeight * rows);
    }

    /// <summary>
    /// Calculates bounds (UniformGrid always fills the parent).
    /// </summary>
    public override Rect CalculateBounds(Rect parent) => parent;

    /// <summary>
    /// Renders the uniform grid and all its children.
    /// </summary>
    protected override void OnRender(CellBuffer buffer, Rect parentBounds)
    {
        if (Children.Count == 0)
        {
            return;
        }

        var bounds = CalculateBounds(parentBounds);
        var childBounds = CalculateChildBounds(bounds);

        // Bound the loop by the computed array as well as the live collection: rendering a child
        // can mutate Children (an ItemsControl regenerating its containers, for instance).
        var count = Math.Min(childBounds.Length, Children.Count);
        for (var i = 0; i < count; i++)
        {
            var rect = childBounds[i];
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                continue;
            }

            Children[i].Render(buffer, rect);
        }
    }

    /// <inheritdoc />
    public override IEnumerable<(Visual Child, Rect ChildParentBounds)> GetChildrenWithBounds(Rect myBounds)
    {
        if (Children.Count == 0)
        {
            yield break;
        }

        var childBounds = CalculateChildBounds(myBounds);

        var count = Math.Min(childBounds.Length, Children.Count);
        for (var i = 0; i < count; i++)
        {
            var rect = childBounds[i];
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                continue;
            }

            yield return (Children[i], rect);
        }
    }

    /// <summary>
    /// Resolves the effective (rows, columns) for a given number of occupied cells.
    /// Both zero derives a near-square shape; one zero is derived from the other.
    /// </summary>
    internal (int Rows, int Columns) ResolveShape(int cellCount)
    {
        var rows = Rows;
        var columns = Columns;

        if (cellCount <= 0)
        {
            return (Math.Max(rows, 0), Math.Max(columns, 0));
        }

        if (rows <= 0 && columns <= 0)
        {
            columns = (int)Math.Ceiling(Math.Sqrt(cellCount));
            rows = CeilDiv(cellCount, columns);
        }
        else if (columns <= 0)
        {
            columns = CeilDiv(cellCount, rows);
        }
        else if (rows <= 0)
        {
            rows = CeilDiv(cellCount, columns);
        }

        return (Math.Max(1, rows), Math.Max(1, columns));
    }

    /// <summary>
    /// Computes the rectangle for every child, filling the grid row by row.
    /// </summary>
    /// <remarks>
    /// Collapsed children occupy no cell at all — they are handed a zero-size rectangle and their
    /// siblings shift up to close the gap. Hidden children take their cell as normal; the cells
    /// paint as background because the public Render wrapper on UIElement skips OnRender.
    /// Children beyond <c>rows * columns</c> get a zero-size rectangle rather than spilling out.
    /// </remarks>
    internal Rect[] CalculateChildBounds(Rect bounds)
    {
        var result = new Rect[Children.Count];

        var cellCount = 0;
        foreach (var child in Children)
        {
            if (child.Visibility != Visibility.Collapsed)
            {
                cellCount++;
            }
        }

        if (cellCount == 0)
        {
            return result;
        }

        var (rows, columns) = ResolveShape(cellCount);

        var columnWidths = DistributeEvenly(Math.Max(0, bounds.Width), columns);
        var rowHeights = DistributeEvenly(Math.Max(0, bounds.Height), rows);

        var columnOffsets = new int[columns];
        var offset = bounds.X;
        for (var c = 0; c < columns; c++)
        {
            columnOffsets[c] = offset;
            offset += columnWidths[c];
        }

        var rowOffsets = new int[rows];
        offset = bounds.Y;
        for (var r = 0; r < rows; r++)
        {
            rowOffsets[r] = offset;
            offset += rowHeights[r];
        }

        var cell = 0;
        for (var i = 0; i < Children.Count; i++)
        {
            if (Children[i].Visibility == Visibility.Collapsed)
            {
                result[i] = new Rect(bounds.X, bounds.Y, 0, 0);
                continue;
            }

            if (cell >= rows * columns)
            {
                // More children than cells: the overflow is dropped rather than drawn on top of
                // the last cell, which would silently stack unreadable content.
                result[i] = new Rect(bounds.X, bounds.Y, 0, 0);
                continue;
            }

            var row = cell / columns;
            var column = cell % columns;
            cell++;

            result[i] = new Rect(columnOffsets[column], rowOffsets[row], columnWidths[column], rowHeights[row]);
        }

        return result;
    }

    /// <summary>
    /// Splits <paramref name="total"/> cells across <paramref name="parts"/> slots exactly: every
    /// slot gets the floor, and the remainder is spread one cell at a time over the leading slots
    /// so that the result always sums back to <paramref name="total"/>.
    /// </summary>
    private static int[] DistributeEvenly(int total, int parts)
    {
        var sizes = new int[parts];
        if (parts <= 0)
        {
            return sizes;
        }

        var basis = total / parts;
        var remainder = total % parts;

        for (var i = 0; i < parts; i++)
        {
            sizes[i] = basis;
            if (remainder > 0)
            {
                sizes[i]++;
                remainder--;
            }
        }

        return sizes;
    }

    private static int CeilDiv(int value, int divisor) => divisor <= 0 ? value : (value + divisor - 1) / divisor;
}
