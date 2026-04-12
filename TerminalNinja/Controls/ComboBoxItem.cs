using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Controls.Primitives;
using TerminalNinja.Input;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Represents a selectable item inside a <see cref="ComboBox"/>.
/// Renders with a highlight background when selected.
/// Corresponds to WPF's System.Windows.Controls.ComboBoxItem.
/// </summary>
[ContentProperty("Content")]
[RuntimeNameProperty("Name")]
public class ComboBoxItem : ContentControl, ISelectableContainer
{
    public ComboBoxItem()
    {
        DefaultStyleKey = typeof(ComboBoxItem);
        Focusable = false;
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(ComboBoxItem),
            new FrameworkPropertyMetadata(false, affectsRender: true));

    public static readonly DependencyProperty SelectedBackgroundProperty =
        DependencyProperty.Register(nameof(SelectedBackground), typeof(Color), typeof(ComboBoxItem),
            new FrameworkPropertyMetadata(Color.Blue, affectsRender: true));

    public static readonly DependencyProperty SelectedForegroundProperty =
        DependencyProperty.Register(nameof(SelectedForeground), typeof(Color), typeof(ComboBoxItem),
            new FrameworkPropertyMetadata(Color.White, affectsRender: true));

    /// <summary>Gets or sets whether this item is currently selected.</summary>
    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty)!;
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>Gets or sets the background color when selected.</summary>
    public Color SelectedBackground
    {
        get => (Color)GetValue(SelectedBackgroundProperty)!;
        set => SetValue(SelectedBackgroundProperty, value);
    }

    /// <summary>Gets or sets the foreground color when selected.</summary>
    public Color SelectedForeground
    {
        get => (Color)GetValue(SelectedForegroundProperty)!;
        set => SetValue(SelectedForegroundProperty, value);
    }

    // ─── Layout ──────────────────────────────────────────────────────

    /// <inheritdoc />
    public override Size2D GetPreferredSize(Rect parent)
    {
        var contentSize = base.GetPreferredSize(parent);
        return new Size2D(Math.Max(contentSize.Width, parent.Width), Math.Max(contentSize.Height, 1));
    }

    /// <inheritdoc />
    public override Rect CalculateBounds(Rect parent) => parent;

    // ─── Rendering ───────────────────────────────────────────────────

    /// <inheritdoc />
    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        var clipped = bounds.Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0) return;

        var bg = IsSelected ? SelectedBackground : Background;
        var fg = IsSelected ? SelectedForeground : Foreground;

        buffer.FillRect(clipped, new Cell(' ', fg, bg));

        // Render content with overridden colors if it's a TextBlock
        var visualChild = GetVisualChildForRendering(bounds);
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
        else if (visualChild != null)
        {
            visualChild.Render(buffer, bounds);
        }
        else
        {
            base.Render(buffer, bounds);
        }
    }

    private UIElement? GetVisualChildForRendering(Rect bounds)
    {
        foreach (var (child, childBounds) in GetChildrenWithBounds(bounds))
        {
            if (child is ContentPresenter cp)
            {
                cp.GetPreferredSize(childBounds);
                foreach (var (innerChild, _) in cp.GetChildrenWithBounds(childBounds))
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

    // ─── Input ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public override void OnMouseEvent(MouseEvent e)
    {
        if (e is { Action: MouseAction.Press, Button: MouseButton.Left })
        {
            var current = Parent;
            while (current != null)
            {
                if (current is Selector selector)
                {
                    selector.NotifyContainerClicked(this);
                    break;
                }
                current = current.Parent;
            }
        }
    }
}
