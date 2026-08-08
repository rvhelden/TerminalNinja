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
        // Width still fills: a grid's columns are mostly proportional, and a star column has no
        // natural width to report. Height is measured, because a grid used as an item template has
        // to be able to say it is one row tall. Reporting the parent height — as this did — meant
        // the first row of a list swallowed the whole panel and nothing after it drew at all,
        // which is what made grids unusable inside an items panel.
        var rows = _rowDefinitions.Count > 0 ? _rowDefinitions : [new RowDefinition()];

        var content = MeasureContent(rows.Count, parent, horizontal: false, _ => true);
        var height = rows.Count > 1 ? RowSpacing * (rows.Count - 1) : 0;

        for (var i = 0; i < rows.Count; i++)
        {
            var length = rows[i].Height;

            height += length.IsAbsolute
                ? Math.Clamp((int)length.Value, rows[i].MinHeight, rows[i].MaxHeight)
                : Math.Clamp(Math.Max(rows[i].MinHeight, content[i]), rows[i].MinHeight, rows[i].MaxHeight);
        }

        return new Size2D(parent.Width, Math.Min(height, parent.Height));
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
    protected override void OnRender(CellBuffer buffer, Rect parentBounds)
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
        var rowContent = ResolveContentSizes(rows, bounds, horizontal: false, r => r.Height, r => r.SharedSizeGroup);
        var colContent = ResolveContentSizes(cols, bounds, horizontal: true, c => c.Width, c => c.SharedSizeGroup);
        CalculateSizes(rows, bounds.Height, r => r.Height, (r, s) => r.ActualHeight = s, r => r.MinHeight, r => r.MaxHeight, rowSpacing, rowContent);
        CalculateSizes(cols, bounds.Width, c => c.Width, (c, s) => c.ActualWidth = s, c => c.MinWidth, c => c.MaxWidth, columnSpacing, colContent);

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

        var rowContent = ResolveContentSizes(rows, myBounds, horizontal: false, r => r.Height, r => r.SharedSizeGroup);
        var colContent = ResolveContentSizes(cols, myBounds, horizontal: true, c => c.Width, c => c.SharedSizeGroup);
        CalculateSizes(rows, myBounds.Height, r => r.Height, (r, s) => r.ActualHeight = s, r => r.MinHeight, r => r.MaxHeight, rowSpacing, rowContent);
        CalculateSizes(cols, myBounds.Width, c => c.Width, (c, s) => c.ActualWidth = s, c => c.MinWidth, c => c.MaxWidth, columnSpacing, colContent);
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

    // ─── Shared sizing ───────────────────────────────────────────────

    /// <summary>
    /// Marks an element as the boundary within which <c>SharedSizeGroup</c> names are matched.
    /// </summary>
    /// <remarks>
    /// Attached rather than a Grid property because the grids that need to agree are siblings —
    /// typically rows of an ItemsControl — and the thing they have in common is an ancestor.
    /// The name is only shared within the nearest such ancestor, so two screens can both use
    /// "keys" without one setting the other's column width.
    /// </remarks>
    public static readonly DependencyProperty IsSharedSizeScopeProperty =
        DependencyProperty.RegisterAttached("IsSharedSizeScope", typeof(bool), typeof(Grid),
            new PropertyMetadata(false));

    private static readonly DependencyProperty SharedSizeScopeProperty =
        DependencyProperty.RegisterAttached("SharedSizeScope", typeof(SharedSizeScope), typeof(Grid),
            new PropertyMetadata(null!));

    public static bool GetIsSharedSizeScope(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (bool)element.GetValue(IsSharedSizeScopeProperty)!;
    }

    public static void SetIsSharedSizeScope(DependencyObject element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(IsSharedSizeScopeProperty, value);
    }

    /// <summary>The nearest ancestor scope, this grid included, or null when there is none.</summary>
    private (SharedSizeScope Scope, Visual Root)? FindScope()
    {
        for (Visual? visual = this; visual is not null; visual = visual.Parent)
        {
            if (visual is not DependencyObject element || !GetIsSharedSizeScope(element))
            {
                continue;
            }

            if (element.GetValue(SharedSizeScopeProperty) is SharedSizeScope existing)
            {
                return (existing, visual);
            }

            var created = new SharedSizeScope();
            element.SetValue(SharedSizeScopeProperty, created);
            return (created, visual);
        }

        return null;
    }

    /// <summary>The scope key for a group, kept distinct per axis.</summary>
    private static string Key(string group, bool horizontal) => horizontal ? "c:" + group : "r:" + group;

    /// <summary>
    /// Casts this grid's vote for every group it takes part in. Called on each grid under a scope
    /// before any of them reads a result, so no grid lays out against a half-collected answer.
    /// </summary>
    internal void PublishSharedContributions(SharedSizeScope scope, Rect bounds)
    {
        Publish(_columnDefinitions, bounds, horizontal: true, c => c.Width, c => c.SharedSizeGroup);
        Publish(_rowDefinitions, bounds, horizontal: false, r => r.Height, r => r.SharedSizeGroup);

        void Publish<T>(IList<T> definitions, Rect area, bool horizontal, Func<T, GridLength> getLength,
            Func<T, string?> getGroup)
        {
            var any = false;
            for (var i = 0; i < definitions.Count; i++)
            {
                if (!string.IsNullOrEmpty(getGroup(definitions[i])))
                {
                    any = true;
                    break;
                }
            }

            if (!any)
            {
                return;
            }

            var measured = MeasureContent(definitions.Count, area, horizontal,
                i => getLength(definitions[i]).IsAuto || !string.IsNullOrEmpty(getGroup(definitions[i])));

            for (var i = 0; i < definitions.Count; i++)
            {
                var group = getGroup(definitions[i]);
                if (!string.IsNullOrEmpty(group))
                {
                    scope.Publish(this, Key(group, horizontal), measured[i]);
                }
            }
        }
    }

    /// <summary>
    /// What each row or column needs for its own content, before the shared groups have their say.
    /// </summary>
    /// <remarks>
    /// Only children spanning a single row or column contribute. A spanned child's size cannot be
    /// attributed to any one definition without deciding how to split it, and guessing there is
    /// worse than ignoring it — WPF resolves spans in a later pass this layout has no room for.
    /// </remarks>
    private int[] MeasureContent(int count, Rect bounds, bool horizontal, Func<int, bool> participates)
    {
        var sizes = new int[count];

        foreach (var child in Children)
        {
            if (child is UIElement { Visibility: Visibility.Collapsed })
            {
                continue;
            }

            var span = horizontal ? GetColumnSpan(child) : GetRowSpan(child);
            if (span != 1)
            {
                continue;
            }

            var index = Math.Clamp(horizontal ? GetColumn(child) : GetRow(child), 0, count - 1);
            if (!participates(index))
            {
                continue;
            }

            var margin = child is FrameworkElement fe ? fe.Margin : new Thickness(0);
            var preferred = child.GetPreferredSize(bounds);

            sizes[index] = Math.Max(sizes[index], horizontal
                ? preferred.Width + margin.HorizontalTotal
                : preferred.Height + margin.VerticalTotal);
        }

        return sizes;
    }

    /// <summary>
    /// Measures the content of every Auto or shared definition, then lets the scope raise the
    /// shared ones to the widest anybody under it asked for.
    /// </summary>
    private int[] ResolveContentSizes<T>(IList<T> definitions, Rect bounds, bool horizontal,
        Func<T, GridLength> getLength, Func<T, string?> getGroup)
    {
        var groups = false;
        for (var i = 0; i < definitions.Count; i++)
        {
            if (!string.IsNullOrEmpty(getGroup(definitions[i])))
            {
                groups = true;
                break;
            }
        }

        var sizes = MeasureContent(definitions.Count, bounds, horizontal,
            i => getLength(definitions[i]).IsAuto || !string.IsNullOrEmpty(getGroup(definitions[i])));

        if (!groups)
        {
            return sizes;
        }

        if (FindScope() is not { } found)
        {
            // A SharedSizeGroup with no scope above it sizes to its own content, which is what
            // Auto would have done. Silently doing nothing at all would be harder to notice.
            return sizes;
        }

        // Everyone votes before anyone reads, so the first grid to lay out in a frame already
        // knows what its widest peer needs. Without this the frame would be a cell short and only
        // settle on the next one, which a single-frame capture never gets.
        found.Scope.Collect(found.Root, bounds);

        // Vote for ourselves as well. The walk only knows the containers it can reach, and a grid
        // hosted by a control it does not understand would otherwise read back a width of zero
        // and collapse the column rather than merely failing to align it.
        for (var i = 0; i < definitions.Count; i++)
        {
            var own = getGroup(definitions[i]);
            if (!string.IsNullOrEmpty(own))
            {
                found.Scope.Publish(this, Key(own, horizontal), sizes[i]);
            }
        }

        for (var i = 0; i < definitions.Count; i++)
        {
            var group = getGroup(definitions[i]);
            if (string.IsNullOrEmpty(group))
            {
                continue;
            }

            sizes[i] = found.Scope.Largest(Key(group, horizontal));
        }

        return sizes;
    }

    /// <summary>
    /// Calculates sizes for rows or columns based on their GridLength definitions.
    /// Uses a three-pass algorithm: Pixel -> Auto -> Star.
    /// </summary>
    private static void CalculateSizes<T>(IList<T> definitions, int availableSize, Func<T, GridLength> getLength, Action<T, int> setActualSize, Func<T, int> getMin,
        Func<T, int> getMax, int spacing = 0, int[]? contentSizes = null)
    {
        // Subtract total spacing from available size (gaps between items only)
        var totalSpacing = definitions.Count > 1 ? spacing * (definitions.Count - 1) : 0;
        var remaining = Math.Max(0, availableSize - totalSpacing);
        var totalStarWeight = 0.0;

        // First pass: allocate Pixel sizes
        for (var i = 0; i < definitions.Count; i++)
        {
            var def = definitions[i];
            var length = getLength(def);

            // A shared-size definition is content-sized whatever its GridLength says: sharing a
            // proportional width between grids means nothing, so the group drives it instead.
            var shared = contentSizes is not null && contentSizes[i] > 0 && !length.IsAuto;

            if (length.IsAbsolute && !shared)
            {
                var size = Math.Clamp((int)length.Value, getMin(def), getMax(def));
                size = Math.Min(size, remaining);
                setActualSize(def, size);
                remaining -= size;
            }
            else if (length.IsStar && !shared)
            {
                totalStarWeight += length.Value;
                setActualSize(def, 0); // Will be set in third pass
            }
            else // Auto, or a member of a shared-size group
            {
                // Auto is what the content asked for, floored by MinWidth/MinHeight. contentSizes
                // carries that measurement — and, for a shared group, the largest measurement
                // anyone under the scope asked for.
                var content = contentSizes is not null ? contentSizes[i] : 0;
                var size = Math.Clamp(Math.Max(getMin(def), content), getMin(def), getMax(def));
                size = Math.Min(size, remaining);
                setActualSize(def, size);
                remaining -= size;
            }
        }

        // Second pass: Auto and shared-size definitions were measured from their content in
        // ResolveContentSizes and allocated above.

        // Third pass: distribute remaining space to Star definitions
        if (totalStarWeight > 0 && remaining > 0)
        {
            var sizePerStar = remaining / totalStarWeight;
            var allocated = 0;

            // A star definition that belongs to a shared-size group was already sized from the
            // group above; letting it back into this pass would overwrite that with a share of
            // the leftovers, which is the whole thing the group exists to prevent.
            var starDefs = new List<T>();
            for (var j = 0; j < definitions.Count; j++)
            {
                var candidate = definitions[j];
                var isShared = contentSizes is not null && contentSizes[j] > 0 && !getLength(candidate).IsAuto;

                if (getLength(candidate).IsStar && !isShared)
                {
                    starDefs.Add(candidate);
                }
            }

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