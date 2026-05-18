using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Base class for controls that contain a single child element.
/// Content can be any object: a UIElement (rendered directly), a string (rendered
/// as a TextBlock), or any other object (rendered via DataTemplate or ToString()).
/// Internally delegates rendering through a <see cref="ContentPresenter"/>.
/// Corresponds to WPF's System.Windows.Controls.ContentControl.
/// </summary>
[ContentProperty("Content")]
[RuntimeNameProperty("Name")]
public class ContentControl : Control
{
    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(nameof(Content), typeof(object), typeof(ContentControl),
            new FrameworkPropertyMetadata(null, affectsRender: true,
                propertyChangedCallback: OnContentChanged));

    public static readonly DependencyProperty ContentTemplateProperty =
        DependencyProperty.Register(nameof(ContentTemplate), typeof(DataTemplate), typeof(ContentControl),
            new FrameworkPropertyMetadata(null, affectsRender: true,
                propertyChangedCallback: OnContentTemplateChanged));

    private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // Manage parent relationships for Visual content
        if (e.OldValue is Visual oldChild)
        {
            oldChild.Parent = null;
        }

        if (e.NewValue is Visual newChild)
        {
            newChild.Parent = d as Visual;
        }

        // Forward to the internal ContentPresenter
        var cc = (ContentControl)d;
        cc._contentPresenter.Content = e.NewValue;
    }

    private static void OnContentTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var cc = (ContentControl)d;
        cc._contentPresenter.ContentTemplate = e.NewValue as DataTemplate;
    }

    // ─── Internal ContentPresenter ───────────────────────────────────

    private readonly ContentPresenter _contentPresenter;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentControl"/> class.
    /// </summary>
    public ContentControl()
    {
        DefaultStyleKey = typeof(ContentControl);
        _contentPresenter = new ContentPresenter();
        _contentPresenter.Parent = this;
        Focusable = false;
    }

    // ─── CLR wrappers ────────────────────────────────────────────────

    /// <summary>
    /// Gets or sets the content of this control. Can be a UIElement, a string,
    /// or any object (rendered via DataTemplate or ToString() fallback).
    /// </summary>
    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>
    /// Gets or sets the DataTemplate used to display the content.
    /// </summary>
    public DataTemplate? ContentTemplate
    {
        get => (DataTemplate?)GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    /// <summary>
    /// Gets whether this control has content.
    /// </summary>
    public bool HasContent => Content != null;

    // ─── Visual tree ─────────────────────────────────────────────────

    /// <inheritdoc />
    protected internal override IEnumerable<FrameworkElement> GetLogicalChildren()
    {
        // Expose the content presenter's logical children (the actual visual child)
        foreach (var child in _contentPresenter.GetLogicalChildren())
        {
            yield return child;
        }
    }

    /// <inheritdoc />
    public override IEnumerable<(Visual Child, Rect ChildParentBounds)> GetChildrenWithBounds(Rect myBounds)
    {
        yield return (_contentPresenter, myBounds);
    }

    /// <inheritdoc />
    public override Size2D GetPreferredSize(Rect parent)
    {
        return _contentPresenter.GetPreferredSize(parent);
    }

    /// <inheritdoc />
    public override Rect CalculateBounds(Rect parent) => parent;

    /// <inheritdoc />
    protected override void OnRender(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        _contentPresenter.Render(buffer, bounds);
    }
}
