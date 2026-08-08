using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// A panel that docks each child against one edge of the remaining space, in the order the
/// children are declared. The last child optionally fills whatever is left.
/// Children choose their edge with the <c>DockPanel.Dock</c> attached property.
/// </summary>
/// <remarks>
/// Every docked child consumes cells out of a shrinking rectangle, so declaration order is the
/// layout. A docked child is sized from its own preferred size along the dock axis and always
/// clamped to what is actually left — a header asking for ten rows in a four-row panel gets four
/// rather than overflowing its siblings, and once the rectangle is empty the remaining children
/// collapse to zero-size instead of drawing outside the panel.
/// <see cref="LastChildFill"/> is what spends the final remainder: with it on, no cell is left
/// unassigned.
/// </remarks>
[ContentProperty("Children")]
[RuntimeNameProperty("Name")]
public class DockPanel : Panel
{
    public DockPanel()
    {
        DefaultStyleKey = typeof(DockPanel);
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty LastChildFillProperty =
        DependencyProperty.Register(nameof(LastChildFill), typeof(bool), typeof(DockPanel),
            new FrameworkPropertyMetadata(true, affectsRender: true));

    // ─── Attached Dependency Properties ──────────────────────────────

    public static readonly DependencyProperty DockProperty =
        DependencyProperty.RegisterAttached("Dock", typeof(Dock), typeof(DockPanel),
            new PropertyMetadata(Dock.Left));

    /// <summary>
    /// Gets or sets whether the last non-collapsed child fills the space left over after all
    /// preceding children have been docked. Default is <c>true</c>.
    /// </summary>
    public bool LastChildFill
    {
        get => (bool)GetValue(LastChildFillProperty)!;
        set => SetValue(LastChildFillProperty, value);
    }

    // ─── Attached Property Accessors ─────────────────────────────────

    /// <summary>
    /// Gets the DockPanel.Dock attached property value for a control.
    /// </summary>
    public static Dock GetDock(DependencyObject control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return (Dock)control.GetValue(DockProperty)!;
    }

    /// <summary>
    /// Sets the DockPanel.Dock attached property value for a control.
    /// </summary>
    public static void SetDock(DependencyObject control, Dock dock)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(DockProperty, dock);
    }

    /// <summary>
    /// Returns the preferred size of this panel: the space its children need when docked in
    /// order. Left/Right children accumulate width, Top/Bottom children accumulate height, and
    /// each contributes its cross-axis extent on top of whatever has already been consumed.
    /// </summary>
    public override Size2D GetPreferredSize(Rect parent)
    {
        if (Children.Count == 0)
        {
            return new Size2D(0, 0);
        }

        var totalWidth = 0;
        var totalHeight = 0;
        var accumulatedWidth = 0;
        var accumulatedHeight = 0;

        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            var preferred = child.GetPreferredSize(parent);
            var margin = child is FrameworkElement fe ? fe.Margin : new Thickness(0);
            var childWidth = preferred.Width + margin.HorizontalTotal;
            var childHeight = preferred.Height + margin.VerticalTotal;

            switch (GetDock(child))
            {
                case Dock.Left:
                case Dock.Right:
                    totalHeight = Math.Max(totalHeight, accumulatedHeight + childHeight);
                    accumulatedWidth += childWidth;
                    break;

                default:
                    totalWidth = Math.Max(totalWidth, accumulatedWidth + childWidth);
                    accumulatedHeight += childHeight;
                    break;
            }
        }

        return new Size2D(
            Math.Max(totalWidth, accumulatedWidth),
            Math.Max(totalHeight, accumulatedHeight));
    }

    /// <summary>
    /// Calculates bounds (DockPanel always fills the parent).
    /// </summary>
    public override Rect CalculateBounds(Rect parent) => parent;

    /// <summary>
    /// Renders the dock panel and all its children.
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
    /// Computes the rectangle each child is docked into, in declaration order.
    /// </summary>
    /// <remarks>
    /// Collapsed children are handed a zero-size rectangle and take no cells at all, so their
    /// siblings close the gap. Hidden children take their normal allocation — the cells simply
    /// paint as background, because the public Render wrapper on UIElement skips OnRender.
    /// </remarks>
    internal Rect[] CalculateChildBounds(Rect bounds)
    {
        var result = new Rect[Children.Count];

        var x = bounds.X;
        var y = bounds.Y;
        var width = Math.Max(0, bounds.Width);
        var height = Math.Max(0, bounds.Height);

        // The filling child is the last one that is not collapsed — a collapsed trailing child
        // must not swallow the remainder that a visible sibling should have received.
        var fillIndex = -1;
        if (LastChildFill)
        {
            for (var i = Children.Count - 1; i >= 0; i--)
            {
                if (Children[i].Visibility != Visibility.Collapsed)
                {
                    fillIndex = i;
                    break;
                }
            }
        }

        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];

            if (child.Visibility == Visibility.Collapsed)
            {
                result[i] = new Rect(x, y, 0, 0);
                continue;
            }

            if (i == fillIndex)
            {
                result[i] = new Rect(x, y, width, height);
                width = 0;
                height = 0;
                continue;
            }

            var available = new Rect(x, y, width, height);
            var preferred = child.GetPreferredSize(available);
            var margin = child is FrameworkElement fe ? fe.Margin : new Thickness(0);

            switch (GetDock(child))
            {
                case Dock.Left:
                {
                    var childWidth = Math.Clamp(preferred.Width + margin.HorizontalTotal, 0, width);
                    result[i] = new Rect(x, y, childWidth, height);
                    x += childWidth;
                    width -= childWidth;
                    break;
                }

                case Dock.Right:
                {
                    var childWidth = Math.Clamp(preferred.Width + margin.HorizontalTotal, 0, width);
                    result[i] = new Rect(x + width - childWidth, y, childWidth, height);
                    width -= childWidth;
                    break;
                }

                case Dock.Top:
                {
                    var childHeight = Math.Clamp(preferred.Height + margin.VerticalTotal, 0, height);
                    result[i] = new Rect(x, y, width, childHeight);
                    y += childHeight;
                    height -= childHeight;
                    break;
                }

                default: // Dock.Bottom
                {
                    var childHeight = Math.Clamp(preferred.Height + margin.VerticalTotal, 0, height);
                    result[i] = new Rect(x, y + height - childHeight, width, childHeight);
                    height -= childHeight;
                    break;
                }
            }
        }

        return result;
    }
}
