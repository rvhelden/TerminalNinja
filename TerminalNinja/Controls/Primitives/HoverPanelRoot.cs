using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls.Primitives;

/// <summary>
/// Internal root element for a <see cref="HoverPanel"/>. Positions the panel's
/// child content relative to a single anchor cell using <see cref="PlacementMode"/>,
/// clamps to the viewport, and flips when it would overflow.
/// </summary>
internal sealed class HoverPanelRoot : FrameworkElement
{
    private UIElement? _child;

    /// <summary>The single-cell anchor in viewport coordinates.</summary>
    public int AnchorX { get; set; }

    /// <summary>The single-cell anchor in viewport coordinates.</summary>
    public int AnchorY { get; set; }

    /// <summary>Where the panel sits relative to the anchor cell.</summary>
    public PlacementMode Placement { get; set; } = PlacementMode.Bottom;

    /// <summary>Extra horizontal nudge after placement is computed.</summary>
    public int HorizontalOffset { get; set; }

    /// <summary>Extra vertical nudge after placement is computed.</summary>
    public int VerticalOffset { get; set; }

    /// <summary>The single content element.</summary>
    public UIElement? Child
    {
        get => _child;
        set
        {
            if (_child != null) _child.Parent = null;
            _child = value;
            if (_child != null) _child.Parent = this;
        }
    }

    /// <inheritdoc />
    public override Size2D GetPreferredSize(Rect parent)
        => _child?.GetPreferredSize(parent) ?? new Size2D(0, 0);

    /// <inheritdoc />
    public override Rect CalculateBounds(Rect viewport)
    {
        if (_child == null) return new Rect(0, 0, 0, 0);

        var size = _child.GetPreferredSize(viewport);
        int w = Math.Max(size.Width, 1);
        int h = Math.Max(size.Height, 1);

        // Treat the anchor as a 1×1 target rect; reuse the same placement math
        // PopupRoot uses, so the two controls position consistently.
        var targetRect = new Rect(AnchorX, AnchorY, 1, 1);
        int x, y;
        switch (Placement)
        {
            case PlacementMode.Top:
                x = targetRect.X;
                y = targetRect.Y - h;
                break;
            case PlacementMode.Right:
                x = targetRect.Right;
                y = targetRect.Y;
                break;
            case PlacementMode.Left:
                x = targetRect.X - w;
                y = targetRect.Y;
                break;
            case PlacementMode.Center:
                x = targetRect.X - w / 2;
                y = targetRect.Y - h / 2;
                break;
            case PlacementMode.Absolute:
                x = HorizontalOffset;
                y = VerticalOffset;
                break;
            case PlacementMode.Relative:
            case PlacementMode.Bottom:
            default:
                x = targetRect.X;
                y = targetRect.Bottom;
                break;
        }

        if (Placement != PlacementMode.Absolute)
        {
            x += HorizontalOffset;
            y += VerticalOffset;
        }

        // Flip / clamp to viewport. For Bottom placement that would overflow,
        // try flipping above the anchor before clamping.
        if (y + h > viewport.Bottom && Placement == PlacementMode.Bottom)
            y = targetRect.Y - h + VerticalOffset;

        if (x + w > viewport.Right) x = viewport.Right - w;
        if (y + h > viewport.Bottom) y = viewport.Bottom - h;
        if (x < viewport.X) x = viewport.X;
        if (y < viewport.Y) y = viewport.Y;

        return new Rect(x, y, w, h);
    }

    /// <inheritdoc />
    protected override void OnRender(CellBuffer buffer, Rect parentBounds)
    {
        if (_child == null) return;
        var bounds = CalculateBounds(parentBounds);
        _child.Render(buffer, bounds);
    }

    /// <inheritdoc />
    public override IEnumerable<(Visual Child, Rect ChildParentBounds)> GetChildrenWithBounds(Rect myBounds)
    {
        if (_child != null)
        {
            var bounds = CalculateBounds(myBounds);
            yield return (_child, bounds);
        }
    }

    /// <inheritdoc />
    protected internal override IEnumerable<FrameworkElement> GetLogicalChildren()
    {
        if (_child is FrameworkElement fe) yield return fe;
    }
}
