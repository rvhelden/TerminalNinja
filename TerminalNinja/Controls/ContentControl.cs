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
    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(nameof(Content), typeof(UIElement), typeof(ContentControl),
            new FrameworkPropertyMetadata((object?)null, affectsRender: true,
                propertyChangedCallback: OnContentChanged));

    private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is UIElement oldChild)
            oldChild.Parent = null;
        if (e.NewValue is UIElement newChild)
            newChild.Parent = d as Visual;
    }

    /// <summary>
    /// Gets or sets the content of this control.
    /// </summary>
    public UIElement? Content
    {
        get => (UIElement?)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>
    /// Gets whether this control has content.
    /// </summary>
    public bool HasContent => Content != null;

    /// <inheritdoc />
    protected internal override IEnumerable<FrameworkElement> GetLogicalChildren()
    {
        if (Content is FrameworkElement fe)
            yield return fe;
    }

    /// <inheritdoc />
    public override IEnumerable<(Visual Child, Rect ChildParentBounds)> GetChildrenWithBounds(Rect myBounds)
    {
        if (Content != null)
            yield return (Content, myBounds);
    }

    /// <inheritdoc />
    public override Size2D GetPreferredSize(Rect parent)
    {
        return Content?.GetPreferredSize(parent) ?? new Size2D(0, 0);
    }

    /// <inheritdoc />
    public override Rect CalculateBounds(Rect parent) => parent;

    /// <inheritdoc />
    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        Content?.Render(buffer, bounds);
    }
}
