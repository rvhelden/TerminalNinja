using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Internal root element for a <see cref="Popup"/>. Handles positioning the popup's
/// child content relative to a placement target based on <see cref="PlacementMode"/>,
/// offsets, and viewport bounds.
/// </summary>
internal sealed class PopupRoot : FrameworkElement
{
    private UIElement? _child;
    private UIElement? _placementTarget;
    private PlacementMode _placement = PlacementMode.Bottom;
    private int _horizontalOffset;
    private int _verticalOffset;

    /// <summary>
    /// Gets or sets the child content to display inside the popup.
    /// </summary>
    public UIElement? Child
    {
        get => _child;
        set
        {
            if (_child != null)
            {
                _child.Parent = null;
            }

            _child = value;

            if (_child != null)
            {
                _child.Parent = this;
            }
        }
    }

    /// <summary>
    /// Gets or sets the element that this popup is positioned relative to.
    /// </summary>
    public UIElement? PlacementTarget
    {
        get => _placementTarget;
        set => _placementTarget = value;
    }

    /// <summary>
    /// Gets or sets the placement mode.
    /// </summary>
    public PlacementMode Placement
    {
        get => _placement;
        set => _placement = value;
    }

    /// <summary>
    /// Gets or sets the horizontal offset from the calculated position.
    /// </summary>
    public int HorizontalOffset
    {
        get => _horizontalOffset;
        set => _horizontalOffset = value;
    }

    /// <summary>
    /// Gets or sets the vertical offset from the calculated position.
    /// </summary>
    public int VerticalOffset
    {
        get => _verticalOffset;
        set => _verticalOffset = value;
    }

    /// <summary>
    /// The cached bounds of the placement target in viewport coordinates.
    /// Set by <see cref="Popup"/> before pushing the overlay, since the
    /// target may be in the main visual tree and its bounds are not
    /// directly accessible from the overlay's render pass.
    /// </summary>
    public Rect TargetBounds { get; set; }

    public override Size2D GetPreferredSize(Rect parent)
    {
        return _child?.GetPreferredSize(parent) ?? new Size2D(0, 0);
    }

    public override Rect CalculateBounds(Rect viewport)
    {
        if (_child == null)
        {
            return new Rect(0, 0, 0, 0);
        }

        var childSize = _child.GetPreferredSize(viewport);
        var childW = Math.Max(childSize.Width, 1);
        var childH = Math.Max(childSize.Height, 1);

        var targetRect = TargetBounds;

        int x, y;
        switch (_placement)
        {
            case PlacementMode.Bottom:
                x = targetRect.X;
                y = targetRect.Bottom;
                break;

            case PlacementMode.Top:
                x = targetRect.X;
                y = targetRect.Y - childH;
                break;

            case PlacementMode.Right:
                x = targetRect.Right;
                y = targetRect.Y;
                break;

            case PlacementMode.Left:
                x = targetRect.X - childW;
                y = targetRect.Y;
                break;

            case PlacementMode.Center:
                x = targetRect.X + (targetRect.Width - childW) / 2;
                y = targetRect.Y + (targetRect.Height - childH) / 2;
                break;

            case PlacementMode.Relative:
                x = targetRect.X;
                y = targetRect.Y;
                break;

            case PlacementMode.Absolute:
                x = 0;
                y = 0;
                break;

            default:
                x = targetRect.X;
                y = targetRect.Bottom;
                break;
        }

        x += _horizontalOffset;
        y += _verticalOffset;

        // Clamp to viewport bounds — flip if the popup extends beyond edges
        if (x + childW > viewport.Right)
        {
            x = viewport.Right - childW;
        }

        if (y + childH > viewport.Bottom)
        {
            // Try flipping vertically for Bottom placement
            if (_placement == PlacementMode.Bottom)
            {
                y = targetRect.Y - childH + _verticalOffset;
            }
            else
            {
                y = viewport.Bottom - childH;
            }
        }

        if (x < viewport.X)
        {
            x = viewport.X;
        }

        if (y < viewport.Y)
        {
            y = viewport.Y;
        }

        return new Rect(x, y, childW, childH);
    }

    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        if (_child == null)
        {
            return;
        }

        var bounds = CalculateBounds(parentBounds);
        _child.Render(buffer, bounds);
    }

    public override IEnumerable<(Visual Child, Rect ChildParentBounds)> GetChildrenWithBounds(Rect myBounds)
    {
        if (_child != null)
        {
            var bounds = CalculateBounds(myBounds);
            yield return (_child, bounds);
        }
    }

    protected internal override IEnumerable<FrameworkElement> GetLogicalChildren()
    {
        if (_child is FrameworkElement fe)
        {
            yield return fe;
        }
    }
}
