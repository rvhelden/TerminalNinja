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
    private DateTime _lastClickTime;
    private int _lastClickY = -1;
    private int _scrollOffset;

    public ListBox()
    {
        DefaultStyleKey = typeof(ListBox);
    }

    /// <summary>
    /// Raised when an item is activated via Enter, Space, or double-click
    /// (clicking an already-selected item).
    /// </summary>
    public event EventHandler? ItemActivated;

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

        // Use the explicit ItemTemplate, or an implicit one matched on the item's type
        if (SelectItemTemplate(item) is { } template)
        {
            var content = template.CreateContent();
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
    public override bool OnKeyEvent(KeyEvent e)
    {
        switch (e.Key)
        {
            case ConsoleKey.DownArrow:
                MoveSelection(1);
                return true;
            case ConsoleKey.UpArrow:
                MoveSelection(-1);
                return true;
            case ConsoleKey.Home:
                SelectFirst();
                return true;
            case ConsoleKey.End:
                SelectLast();
                return true;
            case ConsoleKey.Enter or ConsoleKey.Spacebar:
                ItemActivated?.Invoke(this, EventArgs.Empty);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Moves the selection by the specified delta (+1 = next, -1 = previous).
    /// </summary>
    private void MoveSelection(int delta)
    {
        // EffectiveItems, not ItemsPanel.Children: virtualized, the children are the screenful
        // currently realised, so selection would stop at the bottom of the first page.
        var count = EffectiveItems.Count;
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

        SetCurrentSelectedIndex(newIndex);
        ScrollSelectedIntoView();
    }

    /// <summary>
    /// Selects the first item.
    /// </summary>
    private void SelectFirst()
    {
        if (EffectiveItems.Count > 0)
        {
            SetCurrentSelectedIndex(0);
            ScrollSelectedIntoView();
        }
    }

    /// <summary>
    /// Selects the last item.
    /// </summary>
    private void SelectLast()
    {
        var count = EffectiveItems.Count;
        if (count > 0)
        {
            SetCurrentSelectedIndex(count - 1);
            ScrollSelectedIntoView();
        }
    }

    /// <summary>
    /// If this ListBox is inside a ScrollViewer, scrolls to keep the selected item visible.
    /// </summary>
    private void ScrollSelectedIntoView()
    {
        if (SelectedIndex < 0) return;

        var current = Parent;
        while (current != null)
        {
            if (current is ScrollViewer sv)
            {
                sv.ScrollIntoView(SelectedIndex);
                return;
            }
            current = current.Parent;
        }
    }

    /// <inheritdoc />
    public override void OnMouseEvent(MouseEvent e)
    {
        switch (e.Action)
        {
            case MouseAction.Press when e.Button == MouseButton.Left:
            {
                var now = DateTime.UtcNow;

                // Double-click: two presses within 500ms at same Y coordinate
                if ((now - _lastClickTime).TotalMilliseconds < 500 && e.Y == _lastClickY)
                {
                    ItemActivated?.Invoke(this, EventArgs.Empty);
                    _lastClickTime = DateTime.MinValue;
                }
                else
                {
                    _lastClickTime = now;
                    _lastClickY = e.Y;
                }
                break;
            }
            case MouseAction.ScrollUp:
                _scrollOffset = Math.Max(0, _scrollOffset - 3);
                InvalidateVisual();
                break;
            case MouseAction.ScrollDown:
                _scrollOffset = Math.Min(Math.Max(0, EffectiveItems.Count - 1), _scrollOffset + 3);
                InvalidateVisual();
                break;
        }
    }

    // ─── Internal Scrolling ──────────────────────────────────────────

    private void EnsureSelectedVisible(int viewportHeight)
    {
        if (SelectedIndex < 0 || viewportHeight <= 0) return;

        if (SelectedIndex < _scrollOffset)
            _scrollOffset = SelectedIndex;
        else if (SelectedIndex >= _scrollOffset + viewportHeight)
            _scrollOffset = SelectedIndex - viewportHeight + 1;
    }

    // ─── Rendering ───────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnRender(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);

        var clipped = bounds.Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0)
        {
            return;
        }

        var bgCell = new Cell(' ', Foreground, Background);
        buffer.FillRect(clipped, bgCell);

        var itemCount = EffectiveItems.Count;
        var viewportHeight = bounds.Height;

        // Ensure selected item is visible
        EnsureSelectedVisible(viewportHeight);

        // Clamp scroll offset
        _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, itemCount - viewportHeight));

        // Build containers for the window about to be drawn. Unvirtualized this is a no-op and
        // the children are already the whole list, which is why the loop below indexes them
        // relative to the realised start rather than absolutely.
        RealizeRange(_scrollOffset, viewportHeight);

        var children = ItemsPanel.Children;
        var firstChild = IsVirtualizing ? 0 : _scrollOffset;

        for (var i = 0; i < viewportHeight && firstChild + i < children.Count; i++)
        {
            var child = children[firstChild + i];
            var itemBounds = new Rect(bounds.X, bounds.Y + i, bounds.Width, 1);
            child.Render(buffer, itemBounds);
        }

        // No focus visual here on purpose: the list owns every cell in its bounds for content,
        // so anything it drew to show focus would overpaint the first and last row and the first
        // and last character of every row between. Focus is shown by the Border around it — see
        // Border.ContainsFocus.
    }
}
