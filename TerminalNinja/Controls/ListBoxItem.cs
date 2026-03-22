using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Input;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Represents a selectable item inside a <see cref="ListBox"/>.
/// Renders a highlight bar when selected. On mouse click, notifies
/// the parent <see cref="Selector"/> to update the selection.
/// Corresponds to WPF's System.Windows.Controls.ListBoxItem.
/// </summary>
[ContentProperty("Content")]
[RuntimeNameProperty("Name")]
public class ListBoxItem : ContentControl
{
    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(ListBoxItem),
            new FrameworkPropertyMetadata(false, affectsRender: true));

    public static readonly DependencyProperty SelectedBackgroundProperty =
        DependencyProperty.Register(nameof(SelectedBackground), typeof(Color), typeof(ListBoxItem),
            new FrameworkPropertyMetadata(Color.Blue, affectsRender: true));

    public static readonly DependencyProperty SelectedForegroundProperty =
        DependencyProperty.Register(nameof(SelectedForeground), typeof(Color), typeof(ListBoxItem),
            new FrameworkPropertyMetadata(Color.White, affectsRender: true));

    /// <summary>
    /// Initializes a new instance of the <see cref="ListBoxItem"/> class.
    /// ListBoxItems are NOT individually focusable — the parent ListBox handles focus.
    /// </summary>
    public ListBoxItem()
    {
        Focusable = false;
    }

    /// <summary>
    /// Gets or sets whether this item is currently selected.
    /// </summary>
    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty)!;
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>
    /// Gets or sets the background color when this item is selected.
    /// </summary>
    public Color SelectedBackground
    {
        get => (Color)GetValue(SelectedBackgroundProperty)!;
        set => SetValue(SelectedBackgroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the foreground color when this item is selected.
    /// </summary>
    public Color SelectedForeground
    {
        get => (Color)GetValue(SelectedForegroundProperty)!;
        set => SetValue(SelectedForegroundProperty, value);
    }

    /// <inheritdoc />
    public override Size2D GetPreferredSize(Rect parent)
    {
        // Delegate to the ContentPresenter via base, default to 1 row high spanning the full width
        var contentSize = base.GetPreferredSize(parent);
        return new Size2D(Math.Max(contentSize.Width, parent.Width), Math.Max(contentSize.Height, 1));
    }

    /// <inheritdoc />
    public override Rect CalculateBounds(Rect parent) => parent;

    /// <inheritdoc />
    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);

        // Clip to buffer
        var clipped = bounds.Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0)
        {
            return;
        }

        // Determine colors based on selection state
        var bg = IsSelected ? SelectedBackground : Background;
        var fg = IsSelected ? SelectedForeground : Foreground;

        // Fill the row with the background color
        var bgCell = new Cell(' ', fg, bg);
        buffer.FillRect(clipped, bgCell);

        // Resolve the visual child to render.
        // The ContentPresenter (via base) handles UIElement/string/object content.
        // We need to access the actual visual to override its colors when selected.
        var visualChild = GetVisualChildForRendering();

        if (visualChild != null)
        {
            // Override child foreground/background if it's a TextBlock
            if (visualChild is TextBlock tb)
            {
                var origFg = tb.Foreground;
                var origBg = tb.Background;
                tb.Foreground = fg;
                tb.Background = bg;
                visualChild.Render(buffer, bounds);
                tb.Foreground = origFg;
                tb.Background = origBg;
            }
            else
            {
                visualChild.Render(buffer, bounds);
            }
        }
    }

    /// <summary>
    /// Gets the actual UIElement visual child for rendering purposes.
    /// Walks through the internal ContentPresenter to find the renderable element.
    /// </summary>
    private UIElement? GetVisualChildForRendering()
    {
        // GetChildrenWithBounds yields the ContentPresenter.
        // The ContentPresenter's GetChildrenWithBounds yields the actual visual child.
        foreach (var (child, _) in GetChildrenWithBounds(new Rect(0, 0, 0, 0)))
        {
            if (child is ContentPresenter cp)
            {
                foreach (var (innerChild, _) in cp.GetChildrenWithBounds(new Rect(0, 0, 0, 0)))
                {
                    return innerChild as UIElement;
                }
            }
            else if (child is UIElement uie)
            {
                return uie;
            }
        }
        return null;
    }

    /// <inheritdoc />
    public override void OnMouseEvent(MouseEvent e)
    {
        if (e.Action == MouseAction.Press && e.Button == MouseButton.Left)
        {
            // Walk up the parent tree to find the parent Selector
            var parentSelector = FindParentSelector();
            if (parentSelector != null)
            {
                parentSelector.NotifyContainerClicked(this);
            }
        }
    }

    /// <summary>
    /// Walks up the visual tree to find the nearest <see cref="Selector"/> ancestor.
    /// </summary>
    private Selector? FindParentSelector()
    {
        var current = Parent;
        while (current != null)
        {
            if (current is Selector selector)
            {
                return selector;
            }

            current = current.Parent;
        }
        return null;
    }
}
