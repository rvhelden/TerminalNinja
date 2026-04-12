using System.Windows.Markup;
using TerminalNinja.Controls.Primitives;
using TerminalNinja.Input;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Represents a row inside a <see cref="ListView"/>.
/// The ListView handles all rendering; ListViewItem is a data/selection container.
/// Corresponds to WPF's System.Windows.Controls.ListViewItem.
/// </summary>
[ContentProperty("Content")]
[RuntimeNameProperty("Name")]
public class ListViewItem : ContentControl, ISelectableContainer
{
    public ListViewItem()
    {
        DefaultStyleKey = typeof(ListViewItem);
        Focusable = false;
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(ListViewItem),
            new FrameworkPropertyMetadata(false, affectsRender: true));

    public static readonly DependencyProperty SelectedBackgroundProperty =
        DependencyProperty.Register(nameof(SelectedBackground), typeof(Color), typeof(ListViewItem),
            new FrameworkPropertyMetadata(Color.Blue, affectsRender: true));

    public static readonly DependencyProperty SelectedForegroundProperty =
        DependencyProperty.Register(nameof(SelectedForeground), typeof(Color), typeof(ListViewItem),
            new FrameworkPropertyMetadata(Color.White, affectsRender: true));

    /// <summary>Gets or sets whether this row is currently selected.</summary>
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
