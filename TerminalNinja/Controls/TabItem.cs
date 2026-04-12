using System.Windows.Markup;
using TerminalNinja.Controls.Primitives;
using TerminalNinja.Input;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Represents a tab inside a <see cref="TabControl"/>.
/// Has a <see cref="Header"/> for the tab label and <see cref="Content"/> for the tab body.
/// Corresponds to WPF's System.Windows.Controls.TabItem.
/// </summary>
[ContentProperty("Content")]
[RuntimeNameProperty("Name")]
public class TabItem : ContentControl, ISelectableContainer
{
    public TabItem()
    {
        DefaultStyleKey = typeof(TabItem);
        Focusable = false;
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(object), typeof(TabItem),
            new FrameworkPropertyMetadata((object?)null, affectsRender: true));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(TabItem),
            new FrameworkPropertyMetadata(false, affectsRender: true));

    public static readonly DependencyProperty SelectedBackgroundProperty =
        DependencyProperty.Register(nameof(SelectedBackground), typeof(Color), typeof(TabItem),
            new FrameworkPropertyMetadata(Color.Blue, affectsRender: true));

    public static readonly DependencyProperty SelectedForegroundProperty =
        DependencyProperty.Register(nameof(SelectedForeground), typeof(Color), typeof(TabItem),
            new FrameworkPropertyMetadata(Color.White, affectsRender: true));

    /// <summary>Gets or sets the header content displayed in the tab strip.</summary>
    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>Gets or sets whether this tab is currently selected.</summary>
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

    /// <summary>Gets the header text for display.</summary>
    internal string HeaderText => Header?.ToString() ?? "";

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
