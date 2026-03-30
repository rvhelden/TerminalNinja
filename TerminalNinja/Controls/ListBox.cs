using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Controls.Primitives;
using TerminalNinja.Input;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// A control that displays a list of selectable items with keyboard navigation.
/// Inherits from <see cref="Selector"/> and generates <see cref="ListBoxItem"/>
/// containers for each data item.
/// Corresponds to WPF's System.Windows.Controls.ListBox.
/// </summary>
[ContentProperty("Items")]
[RuntimeNameProperty("Name")]
public class ListBox : Selector
{
    public ListBox()
    {
        DefaultStyleKey = typeof(ListBox);
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty SelectedBackgroundProperty =
        DependencyProperty.Register(nameof(SelectedBackground), typeof(Color), typeof(ListBox),
            new FrameworkPropertyMetadata(Color.Blue, affectsRender: true));

    public static readonly DependencyProperty SelectedForegroundProperty =
        DependencyProperty.Register(nameof(SelectedForeground), typeof(Color), typeof(ListBox),
            new FrameworkPropertyMetadata(Color.White, affectsRender: true));

    public static readonly DependencyProperty SelectionIndicatorProperty =
        DependencyProperty.Register(nameof(SelectionIndicator), typeof(char), typeof(ListBox),
            new FrameworkPropertyMetadata('\u258C', affectsRender: true)); // '▌' left half block

    public static readonly DependencyProperty ShowSelectionIndicatorProperty =
        DependencyProperty.Register(nameof(ShowSelectionIndicator), typeof(bool), typeof(ListBox),
            new FrameworkPropertyMetadata(true, affectsRender: true));

    /// <summary>
    /// Gets or sets the background color for selected items.
    /// Applied to <see cref="ListBoxItem.SelectedBackground"/> on generated containers.
    /// </summary>
    public Color SelectedBackground
    {
        get => (Color)GetValue(SelectedBackgroundProperty)!;
        set => SetValue(SelectedBackgroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the foreground color for selected items.
    /// Applied to <see cref="ListBoxItem.SelectedForeground"/> on generated containers.
    /// </summary>
    public Color SelectedForeground
    {
        get => (Color)GetValue(SelectedForegroundProperty)!;
        set => SetValue(SelectedForegroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the character used as the selection indicator on generated items.
    /// Default is '▌' (left half block). Applied to <see cref="ListBoxItem.SelectionIndicator"/>.
    /// </summary>
    public char SelectionIndicator
    {
        get => (char)GetValue(SelectionIndicatorProperty)!;
        set => SetValue(SelectionIndicatorProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show a selection indicator character on selected items.
    /// Default is true. Applied to <see cref="ListBoxItem.ShowSelectionIndicator"/>.
    /// </summary>
    public bool ShowSelectionIndicator
    {
        get => (bool)GetValue(ShowSelectionIndicatorProperty)!;
        set => SetValue(ShowSelectionIndicatorProperty, value);
    }

    // ─── Container generation overrides ──────────────────────────────

    /// <inheritdoc />
    protected override bool IsItemItsOwnContainer(object item) => item is ListBoxItem;

    /// <inheritdoc />
    protected override UIElement CreateContainerForItem(object item)
    {
        var lbi = new ListBoxItem
        {
            Background = Background,
            Foreground = Foreground,
            SelectedBackground = SelectedBackground,
            SelectedForeground = SelectedForeground,
            SelectionIndicator = SelectionIndicator,
            ShowSelectionIndicator = ShowSelectionIndicator
        };

        // If there's an ItemTemplate, use it for the content
        if (ItemTemplate != null)
        {
            var content = ItemTemplate.CreateContent();
            if (content is FrameworkElement fe)
            {
                fe.DataContext = item;
            }
            lbi.Content = content;
        }
        else
        {
            // Default: create a TextBlock showing ToString()
            lbi.Content = new TextBlock { Text = item?.ToString() ?? string.Empty };
        }

        return lbi;
    }

    /// <inheritdoc />
    protected override void PrepareContainerForItem(UIElement container, object item)
    {
        base.PrepareContainerForItem(container, item);

        if (container is ListBoxItem lbi)
        {
            // Propagate selection colors and indicator settings from ListBox to each item
            lbi.SelectedBackground = SelectedBackground;
            lbi.SelectedForeground = SelectedForeground;
            lbi.SelectionIndicator = SelectionIndicator;
            lbi.ShowSelectionIndicator = ShowSelectionIndicator;

            // Mark as selected if this is the currently selected item
            lbi.IsSelected = SelectedItem == item;
        }
    }

    // ─── Keyboard navigation ─────────────────────────────────────────

    /// <inheritdoc />
    public override void OnKeyEvent(KeyEvent e)
    {
        switch (e.Key)
        {
            case ConsoleKey.DownArrow:
                MoveSelection(1);
                break;
            case ConsoleKey.UpArrow:
                MoveSelection(-1);
                break;
            case ConsoleKey.Home:
                SelectFirst();
                break;
            case ConsoleKey.End:
                SelectLast();
                break;
            case ConsoleKey.Enter or ConsoleKey.Spacebar:
                // Enter/Space confirms the current selection — no additional action needed
                // since SelectedItem is already set. Subclasses or bindings can react to SelectionChanged.
                break;
        }
    }

    /// <summary>
    /// Moves the selection by the specified delta (+1 = next, -1 = previous).
    /// </summary>
    private void MoveSelection(int delta)
    {
        var count = ItemsPanel.Children.Count;
        if (count == 0)
        {
            return;
        }

        var newIndex = SelectedIndex + delta;

        // Clamp to valid range
        if (newIndex < 0)
        {
            newIndex = 0;
        }

        if (newIndex >= count)
        {
            newIndex = count - 1;
        }

        SelectedIndex = newIndex;
    }

    /// <summary>
    /// Selects the first item.
    /// </summary>
    private void SelectFirst()
    {
        if (ItemsPanel.Children.Count > 0)
        {
            SelectedIndex = 0;
        }
    }

    /// <summary>
    /// Selects the last item.
    /// </summary>
    private void SelectLast()
    {
        var count = ItemsPanel.Children.Count;
        if (count > 0)
        {
            SelectedIndex = count - 1;
        }
    }

    // ─── Rendering ───────────────────────────────────────────────────

    /// <inheritdoc />
    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);

        // Fill background
        var clipped = bounds.Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0)
        {
            return;
        }

        var bgCell = new Cell(' ', Foreground, Background);
        buffer.FillRect(clipped, bgCell);

        // Render items panel
        ItemsPanel.Render(buffer, bounds);
    }
}
