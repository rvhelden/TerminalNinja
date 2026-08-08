using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// A panel that flows children one after another along <see cref="Orientation"/> and starts a new
/// line whenever the next child would cross the panel's bound.
/// </summary>
/// <remarks>
/// Every child in a line is given that line's cross-axis extent — the tallest child in a
/// horizontal row, the widest in a vertical column — so a line reads as a line rather than a
/// ragged edge. Two things keep it honest at the bound: a child larger than the whole panel is
/// clamped to the panel instead of flowing outside it (a single over-long item would otherwise
/// never fit on any line and loop forever), and a line that starts past the far edge is given
/// zero extent so its children are skipped rather than drawn off-panel.
/// </remarks>
[ContentProperty("Children")]
[RuntimeNameProperty("Name")]
public class WrapPanel : Panel
{
    public WrapPanel()
    {
        DefaultStyleKey = typeof(WrapPanel);
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(WrapPanel),
            new FrameworkPropertyMetadata(Orientation.Horizontal, affectsRender: true));

    /// <summary>
    /// Gets or sets the direction children flow in before wrapping. Horizontal (the default)
    /// fills a row left to right and wraps downward; Vertical fills a column top to bottom and
    /// wraps rightward.
    /// </summary>
    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty)!;
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>
    /// Returns the preferred size of this panel: the longest line along the flow axis, and the
    /// sum of the line extents across it, as wrapped against <paramref name="parent"/>.
    /// </summary>
    public override Size2D GetPreferredSize(Rect parent)
    {
        if (Children.Count == 0)
        {
            return new Size2D(0, 0);
        }

        var horizontal = Orientation == Orientation.Horizontal;
        var mainLimit = horizontal ? parent.Width : parent.Height;
        var layout = MeasureLines(parent, mainLimit);

        if (layout.LineCross.Length == 0)
        {
            return new Size2D(0, 0);
        }

        var mainExtent = 0;
        for (var i = 0; i < Children.Count; i++)
        {
            if (layout.LineOf[i] < 0)
            {
                continue;
            }

            mainExtent = Math.Max(mainExtent, layout.MainPos[i] + layout.MainSize[i]);
        }

        var crossExtent = 0;
        foreach (var lineCross in layout.LineCross)
        {
            crossExtent += lineCross;
        }

        return horizontal
            ? new Size2D(mainExtent, crossExtent)
            : new Size2D(crossExtent, mainExtent);
    }

    /// <summary>
    /// Calculates bounds (WrapPanel always fills the parent).
    /// </summary>
    public override Rect CalculateBounds(Rect parent) => parent;

    /// <summary>
    /// Renders the wrap panel and all its children.
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
    /// Computes the rectangle for every child, flowed and wrapped inside <paramref name="bounds"/>.
    /// </summary>
    /// <remarks>
    /// Collapsed children are handed a zero-size rectangle and take no room in the flow, so the
    /// children after them close the gap. Hidden children flow normally; their cells paint as
    /// background because the public Render wrapper on UIElement skips OnRender.
    /// </remarks>
    internal Rect[] CalculateChildBounds(Rect bounds)
    {
        var result = new Rect[Children.Count];
        if (Children.Count == 0)
        {
            return result;
        }

        var horizontal = Orientation == Orientation.Horizontal;
        var mainLimit = Math.Max(0, horizontal ? bounds.Width : bounds.Height);
        var crossLimit = Math.Max(0, horizontal ? bounds.Height : bounds.Width);

        var layout = MeasureLines(bounds, mainLimit);

        // Cross-axis offset of each line, accumulated so the lines tile without gaps.
        var lineOffsets = new int[layout.LineCross.Length];
        var offset = 0;
        for (var l = 0; l < layout.LineCross.Length; l++)
        {
            lineOffsets[l] = offset;
            offset += layout.LineCross[l];
        }

        for (var i = 0; i < Children.Count; i++)
        {
            var line = layout.LineOf[i];
            if (line < 0)
            {
                result[i] = new Rect(bounds.X, bounds.Y, 0, 0);
                continue;
            }

            var crossPos = lineOffsets[line];
            var crossSize = Math.Clamp(layout.LineCross[line], 0, Math.Max(0, crossLimit - crossPos));

            result[i] = horizontal
                ? new Rect(bounds.X + layout.MainPos[i], bounds.Y + crossPos, layout.MainSize[i], crossSize)
                : new Rect(bounds.X + crossPos, bounds.Y + layout.MainPos[i], crossSize, layout.MainSize[i]);
        }

        return result;
    }

    /// <summary>
    /// Breaks the children into lines along the flow axis.
    /// </summary>
    /// <returns>
    /// Per-child flow-axis position and extent, the index of the line each child landed on
    /// (-1 for collapsed children, which take no room), and each line's cross-axis extent.
    /// </returns>
    private (int[] MainPos, int[] MainSize, int[] LineOf, int[] LineCross) MeasureLines(Rect available, int mainLimit)
    {
        var count = Children.Count;
        var mainPos = new int[count];
        var mainSize = new int[count];
        var lineOf = new int[count];
        var lineCross = new List<int>();

        var horizontal = Orientation == Orientation.Horizontal;
        var currentMain = 0;
        var currentCross = 0;
        var lineIndex = 0;
        var lineHasChild = false;

        for (var i = 0; i < count; i++)
        {
            var child = Children[i];

            if (child.Visibility == Visibility.Collapsed)
            {
                lineOf[i] = -1;
                continue;
            }

            var preferred = child.GetPreferredSize(available);
            var margin = child is FrameworkElement fe ? fe.Margin : new Thickness(0);

            var childMain = horizontal
                ? preferred.Width + margin.HorizontalTotal
                : preferred.Height + margin.VerticalTotal;
            var childCross = horizontal
                ? preferred.Height + margin.VerticalTotal
                : preferred.Width + margin.HorizontalTotal;

            // A child that is longer than the panel itself can never fit on a fresh line, so
            // clamp it rather than wrap forever looking for room that does not exist.
            childMain = Math.Clamp(childMain, 0, mainLimit);
            childCross = Math.Max(0, childCross);

            if (lineHasChild && currentMain + childMain > mainLimit)
            {
                lineCross.Add(currentCross);
                lineIndex++;
                currentMain = 0;
                currentCross = 0;
                lineHasChild = false;
            }

            mainPos[i] = currentMain;
            mainSize[i] = childMain;
            lineOf[i] = lineIndex;

            currentMain += childMain;
            currentCross = Math.Max(currentCross, childCross);
            lineHasChild = true;
        }

        if (lineHasChild)
        {
            lineCross.Add(currentCross);
        }

        return (mainPos, mainSize, lineOf, lineCross.ToArray());
    }
}
