using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Base class for controls that contain a single child element.
/// Corresponds to WPF's System.Windows.Controls.ContentControl.
/// </summary>
[ContentProperty("Content")]
[RuntimeNameProperty("Name")]
public class ContentControl : Control
{
    private UIElement? _content;

    /// <summary>
    /// Gets or sets the content of this control.
    /// </summary>
    public UIElement? Content
    {
        get => _content;
        set
        {
            if (ReferenceEquals(_content, value))
                return;

            if (_content != null)
                _content.Parent = null;

            _content = value;

            if (_content != null)
                _content.Parent = this;

            InvalidateVisual();
        }
    }

    /// <summary>
    /// Gets whether this control has content.
    /// </summary>
    public bool HasContent => _content != null;

    /// <inheritdoc />
    protected internal override IEnumerable<FrameworkElement> GetLogicalChildren()
    {
        if (_content is FrameworkElement fe)
            yield return fe;
    }

    /// <inheritdoc />
    public override IEnumerable<(Visual Child, Rect ChildParentBounds)> GetChildrenWithBounds(Rect myBounds)
    {
        if (_content != null)
            yield return (_content, myBounds);
    }

    /// <inheritdoc />
    public override Size2D GetPreferredSize(Rect parent)
    {
        return _content?.GetPreferredSize(parent) ?? new Size2D(0, 0);
    }

    /// <inheritdoc />
    public override Rect CalculateBounds(Rect parent) => parent;

    /// <inheritdoc />
    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        _content?.Render(buffer, bounds);
    }
}
