using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Displays the content of a ContentControl.
/// Handles template selection and content visualization — UIElement content is
/// rendered directly; non-UIElement content (strings, view models) is wrapped
/// in a TextBlock or rendered via a DataTemplate.
/// Corresponds to WPF's System.Windows.Controls.ContentPresenter.
/// </summary>
public class ContentPresenter : FrameworkElement
{
    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(nameof(Content), typeof(object), typeof(ContentPresenter),
            new FrameworkPropertyMetadata((object?)null, affectsRender: true,
                propertyChangedCallback: OnContentChanged));

    public static readonly DependencyProperty ContentTemplateProperty =
        DependencyProperty.Register(nameof(ContentTemplate), typeof(DataTemplate), typeof(ContentPresenter),
            new FrameworkPropertyMetadata((object?)null, affectsRender: true,
                propertyChangedCallback: (d, _) => ((ContentPresenter)d).InvalidateTemplatedChild()));

    private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var cp = (ContentPresenter)d;

        // Detach old visual child (only if we own its parentage)
        if (cp._visualChild != null && cp._ownsVisualChildParent)
        {
            cp._visualChild.Parent = null;
        }
        cp._visualChild = null;
        cp._ownsVisualChildParent = false;

        // Sync DataContext = Content (WPF convention for ContentPresenter)
        cp.DataContext = e.NewValue;

        // Regenerate the visual child
        cp.EnsureTemplatedChild();
    }

    // ─── CLR wrappers ────────────────────────────────────────────────

    /// <summary>
    /// Gets or sets the content to display. Can be a UIElement (rendered directly),
    /// a string (rendered as a TextBlock), or any object (rendered via DataTemplate
    /// or ToString() fallback).
    /// </summary>
    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>
    /// Gets or sets the DataTemplate used to display the content.
    /// When set, this takes priority over all other template selection logic.
    /// </summary>
    public DataTemplate? ContentTemplate
    {
        get => (DataTemplate?)GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    // ─── Visual child management ─────────────────────────────────────

    /// <summary>
    /// The actual UIElement being rendered. This is either the Content itself
    /// (when Content is a UIElement) or a generated element from a template/fallback.
    /// </summary>
    private UIElement? _visualChild;

    /// <summary>
    /// Whether we set the visual child's Parent to ourselves.
    /// When Content is a UIElement passed through from ContentControl, the
    /// ContentControl already owns the parent relationship — we must not
    /// overwrite it.
    /// </summary>
    private bool _ownsVisualChildParent;

    /// <summary>
    /// Invalidates the current templated child, forcing re-evaluation on next access.
    /// </summary>
    private void InvalidateTemplatedChild()
    {
        if (_visualChild != null && _ownsVisualChildParent)
        {
            _visualChild.Parent = null;
        }
        _visualChild = null;
        _ownsVisualChildParent = false;
        EnsureTemplatedChild();
    }

    /// <summary>
    /// Ensures that the visual child is created based on Content and ContentTemplate.
    /// </summary>
    private void EnsureTemplatedChild()
    {
        if (_visualChild != null)
        {
            return;
        }

        var content = Content;
        if (content == null)
        {
            return;
        }

        _visualChild = ChooseAndCreateVisualChild(content);

        if (_visualChild != null)
        {
            // If the visual child IS the content itself (a UIElement), its Parent
            // is managed by the ContentControl that owns us — don't overwrite it.
            // If it's a generated element (TextBlock for string content, template output),
            // we own it and set ourselves as parent.
            if (!ReferenceEquals(_visualChild, content))
            {
                _visualChild.Parent = this;
                _ownsVisualChildParent = true;
            }
        }

        InvalidateVisual();
    }

    /// <summary>
    /// Selects the appropriate template and creates the visual child for the given content.
    /// Priority:
    /// 1. Content is UIElement — use directly (no template needed)
    /// 2. Explicit ContentTemplate — use it to create content
    /// 3. String content — create a TextBlock
    /// 4. Any other object — create a TextBlock with ToString()
    /// </summary>
    protected virtual UIElement? ChooseAndCreateVisualChild(object content)
    {
        // 1. If content is already a UIElement, use it directly
        if (content is UIElement uiElement)
        {
            return uiElement;
        }

        // 2. If an explicit ContentTemplate is set, use it
        var template = ChooseTemplate();
        if (template != null)
        {
            var created = template.CreateContent();
            if (created is FrameworkElement fe)
            {
                fe.DataContext = content;
            }
            return created;
        }

        // 3. String content → TextBlock
        if (content is string str)
        {
            return new TextBlock { Text = str };
        }

        // 4. Fallback: any object → TextBlock via ToString()
        return new TextBlock { Text = content.ToString() ?? string.Empty };
    }

    /// <summary>
    /// Selects the DataTemplate to use for displaying the content.
    /// Override in subclasses to provide custom template selection logic.
    /// </summary>
    /// <returns>The selected DataTemplate, or null to use default fallback.</returns>
    protected virtual DataTemplate? ChooseTemplate()
    {
        // Explicit ContentTemplate takes priority
        if (ContentTemplate != null)
        {
            return ContentTemplate;
        }

        // Future: implicit DataTemplate by DataType lookup in resources
        // var content = Content;
        // if (content != null)
        // {
        //     var dt = TryFindResource(content.GetType()) as DataTemplate;
        //     if (dt != null) return dt;
        // }

        return null;
    }

    // ─── Layout & Rendering ──────────────────────────────────────────

    /// <inheritdoc />
    public override IEnumerable<(Visual Child, Rect ChildParentBounds)> GetChildrenWithBounds(Rect myBounds)
    {
        if (_visualChild != null)
        {
            yield return (_visualChild, myBounds);
        }
    }

    /// <inheritdoc />
    protected internal override IEnumerable<FrameworkElement> GetLogicalChildren()
    {
        if (_visualChild is FrameworkElement fe)
        {
            yield return fe;
        }
    }

    /// <inheritdoc />
    public override Size2D GetPreferredSize(Rect parent)
    {
        EnsureTemplatedChild();
        return _visualChild?.GetPreferredSize(parent) ?? new Size2D(0, 0);
    }

    /// <inheritdoc />
    public override Rect CalculateBounds(Rect parent) => parent;

    /// <inheritdoc />
    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        EnsureTemplatedChild();
        var bounds = CalculateBounds(parentBounds);
        _visualChild?.Render(buffer, bounds);
    }
}
