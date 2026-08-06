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

    public static readonly DependencyProperty FocusColorProperty =
        DependencyProperty.Register(nameof(FocusColor), typeof(Color), typeof(ListBox),
            new FrameworkPropertyMetadata(Color.Cyan, affectsRender: true));

    /// <summary>Gets or sets the border color when the ListBox has focus.</summary>
    public Color FocusColor
    {
        get => (Color)GetValue(FocusColorProperty)!;
        set => SetValue(FocusColorProperty, value);
    }

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

    public static readonly DependencyProperty ShowFocusBorderProperty =
        DependencyProperty.Register(nameof(ShowFocusBorder), typeof(bool), typeof(ListBox),
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

    /// <summary>
    /// Gets or sets whether a focus border is drawn around the list when it is focused. Default is
    /// true. The border recolours the cells on the list's edges, which overpaints content sitting
    /// in the first/last row or column; set false when the list is already framed by an outer
    /// Border so the focus rule does not tint edge content.
    /// </summary>
    public bool ShowFocusBorder
    {
        get => (bool)GetValue(ShowFocusBorderProperty)!;
        set => SetValue(ShowFocusBorderProperty, value);
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
                ItemActivated?.Invoke(this, EventArgs.Empty);
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
        ScrollSelectedIntoView();
    }

    /// <summary>
    /// Selects the first item.
    /// </summary>
    private void SelectFirst()
    {
        if (ItemsPanel.Children.Count > 0)
        {
            SelectedIndex = 0;
            ScrollSelectedIntoView();
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
                _scrollOffset = Math.Min(Math.Max(0, ItemsPanel.Children.Count - 1), _scrollOffset + 3);
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

        var children = ItemsPanel.Children;
        var viewportHeight = bounds.Height;

        // Ensure selected item is visible
        EnsureSelectedVisible(viewportHeight);

        // Clamp scroll offset
        _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, children.Count - viewportHeight));

        // Render only the visible items
        for (var i = 0; i < viewportHeight && _scrollOffset + i < children.Count; i++)
        {
            var child = children[_scrollOffset + i];
            var itemBounds = new Rect(bounds.X, bounds.Y + i, bounds.Width, 1);
            child.Render(buffer, itemBounds);
        }

        // Draw focus border when focused
        if (IsFocused && ShowFocusBorder && bounds is { Width: >= 2, Height: >= 2 })
        {
            DrawFocusBorder(buffer, bounds, FocusColor);
        }
    }

    private static void DrawFocusBorder(CellBuffer buffer, Rect bounds, Color color)
    {
        // Top and bottom edges
        for (var x = bounds.X; x < bounds.Right; x++)
        {
            if (x >= 0 && x < buffer.Width)
            {
                if (bounds.Y >= 0 && bounds.Y < buffer.Height)
                {
                    var cell = buffer.GetCell(x, bounds.Y);
                    buffer.SetCell(x, bounds.Y, new Cell(cell.Codepoint, color, cell.Background));
                }
                if (bounds.Bottom - 1 >= 0 && bounds.Bottom - 1 < buffer.Height)
                {
                    var cell = buffer.GetCell(x, bounds.Bottom - 1);
                    buffer.SetCell(x, bounds.Bottom - 1, new Cell(cell.Codepoint, color, cell.Background));
                }
            }
        }

        // Left and right edges
        for (var y = bounds.Y; y < bounds.Bottom; y++)
        {
            if (y >= 0 && y < buffer.Height)
            {
                if (bounds.X >= 0 && bounds.X < buffer.Width)
                {
                    var cell = buffer.GetCell(bounds.X, y);
                    buffer.SetCell(bounds.X, y, new Cell(cell.Codepoint, color, cell.Background));
                }
                if (bounds.Right - 1 >= 0 && bounds.Right - 1 < buffer.Width)
                {
                    var cell = buffer.GetCell(bounds.Right - 1, y);
                    buffer.SetCell(bounds.Right - 1, y, new Cell(cell.Codepoint, color, cell.Background));
                }
            }
        }
    }
}
