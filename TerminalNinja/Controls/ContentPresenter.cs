using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Displays the content of a ContentControl within a control template.
/// This is a stub — full template rendering is not yet implemented.
/// Corresponds to WPF's System.Windows.Controls.ContentPresenter.
/// </summary>
public class ContentPresenter : FrameworkElement
{
    private UIElement? _content;

    /// <summary>
    /// Gets or sets the content to display.
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
