using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Controls.Primitives;
using TerminalNinja.Input;
using TerminalNinja.Primitives;
using TerminalNinja.Styling;

namespace TerminalNinja.Controls;

/// <summary>
/// A drop-down selection control that displays the selected item and opens a
/// popup list for selection. Extends <see cref="Selector"/> for item management
/// and selection semantics.
/// Corresponds to WPF's System.Windows.Controls.ComboBox.
/// </summary>
[ContentProperty("Items")]
[RuntimeNameProperty("Name")]
public sealed class ComboBox : Selector
{
    private readonly Popup _popup;
    private readonly Border _dropdownBorder;

    public ComboBox()
    {
        DefaultStyleKey = typeof(ComboBox);

        _dropdownBorder = new Border
        {
            BorderStyle = Styling.BorderStyle.Single(Foreground),
            Background = Background
        };

        _popup = new Popup
        {
            Placement = PlacementMode.Bottom,
            StaysOpen = false
        };
        _popup.Closed += (_, _) =>
        {
            if (IsDropDownOpen)
                IsDropDownOpen = false;
        };
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty IsDropDownOpenProperty =
        DependencyProperty.Register(nameof(IsDropDownOpen), typeof(bool), typeof(ComboBox),
            new FrameworkPropertyMetadata(false, affectsRender: true,
                propertyChangedCallback: OnIsDropDownOpenChanged));

    public static readonly DependencyProperty MaxDropDownHeightProperty =
        DependencyProperty.Register(nameof(MaxDropDownHeight), typeof(int), typeof(ComboBox),
            new PropertyMetadata(8));

    public static readonly DependencyProperty FocusColorProperty =
        DependencyProperty.Register(nameof(FocusColor), typeof(Color), typeof(ComboBox),
            new FrameworkPropertyMetadata(Color.Cyan, affectsRender: true));

    public static readonly DependencyProperty HoverColorProperty =
        DependencyProperty.Register(nameof(HoverColor), typeof(Color), typeof(ComboBox),
            new FrameworkPropertyMetadata(Color.Yellow, affectsRender: true));

    public static readonly DependencyProperty SelectedBackgroundProperty =
        DependencyProperty.Register(nameof(SelectedBackground), typeof(Color), typeof(ComboBox),
            new FrameworkPropertyMetadata(Color.Blue, affectsRender: true));

    public static readonly DependencyProperty SelectedForegroundProperty =
        DependencyProperty.Register(nameof(SelectedForeground), typeof(Color), typeof(ComboBox),
            new FrameworkPropertyMetadata(Color.White, affectsRender: true));

    private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var cb = (ComboBox)d;
        if ((bool)e.NewValue!)
            cb.OpenDropDown();
        else
            cb.CloseDropDown();
    }

    // ─── CLR Wrappers ────────────────────────────────────────────────

    /// <summary>Gets or sets whether the dropdown is open.</summary>
    public bool IsDropDownOpen
    {
        get => (bool)GetValue(IsDropDownOpenProperty)!;
        set => SetValue(IsDropDownOpenProperty, value);
    }

    /// <summary>Gets or sets the maximum number of visible rows in the dropdown.</summary>
    public int MaxDropDownHeight
    {
        get => (int)GetValue(MaxDropDownHeightProperty)!;
        set => SetValue(MaxDropDownHeightProperty, value);
    }

    /// <summary>Gets or sets the border color when focused.</summary>
    public Color FocusColor
    {
        get => (Color)GetValue(FocusColorProperty)!;
        set => SetValue(FocusColorProperty, value);
    }

    /// <summary>Gets or sets the border color when hovered.</summary>
    public Color HoverColor
    {
        get => (Color)GetValue(HoverColorProperty)!;
        set => SetValue(HoverColorProperty, value);
    }

    /// <summary>Gets or sets the background color for selected items in the dropdown.</summary>
    public Color SelectedBackground
    {
        get => (Color)GetValue(SelectedBackgroundProperty)!;
        set => SetValue(SelectedBackgroundProperty, value);
    }

    /// <summary>Gets or sets the foreground color for selected items in the dropdown.</summary>
    public Color SelectedForeground
    {
        get => (Color)GetValue(SelectedForegroundProperty)!;
        set => SetValue(SelectedForegroundProperty, value);
    }

    // ─── Container Generation ────────────────────────────────────────

    /// <inheritdoc />
    protected override bool IsItemItsOwnContainer(object item) => item is ComboBoxItem;

    /// <inheritdoc />
    protected override UIElement CreateContainerForItem(object item)
    {
        var cbi = new ComboBoxItem
        {
            Background = Background,
            Foreground = Foreground,
            SelectedBackground = SelectedBackground,
            SelectedForeground = SelectedForeground
        };

        if (ItemTemplate != null)
        {
            var content = ItemTemplate.CreateContent();
            if (content is FrameworkElement fe)
                fe.DataContext = item;
            cbi.Content = content;
        }
        else
        {
            cbi.Content = new TextBlock { Text = item?.ToString() ?? "" };
        }

        return cbi;
    }

    /// <inheritdoc />
    protected override void PrepareContainerForItem(UIElement container, object item)
    {
        base.PrepareContainerForItem(container, item);
        if (container is ComboBoxItem cbi)
        {
            cbi.SelectedBackground = SelectedBackground;
            cbi.SelectedForeground = SelectedForeground;
            cbi.IsSelected = SelectedItem == item;
        }
    }

    // ─── Selection Override ──────────────────────────────────────────

    /// <summary>
    /// Notifies the ComboBox that a container was clicked (from ComboBoxItem mouse handler).
    /// Selects the item and closes the dropdown.
    /// </summary>
    internal void NotifyItemClickedAndClose(UIElement container)
    {
        NotifyContainerClicked(container);
        IsDropDownOpen = false;
    }

    // ─── Dropdown ────────────────────────────────────────────────────

    private void OpenDropDown()
    {
        // Set ItemsPanel as popup content
        _dropdownBorder.Child = ItemsPanel;
        _dropdownBorder.BorderStyle = Styling.BorderStyle.Single(Foreground);
        _dropdownBorder.Background = Background;
        _popup.Child = _dropdownBorder;
        _popup.PlacementTarget = this;
        _popup.IsOpen = true;
    }

    private void CloseDropDown()
    {
        _popup.IsOpen = false;
        // Re-parent ItemsPanel back (detach from popup border)
        _dropdownBorder.Child = null;
    }

    // ─── Layout ──────────────────────────────────────────────────────

    /// <inheritdoc />
    public override Size2D GetPreferredSize(Rect parent)
    {
        return new Size2D(Math.Max(parent.Width, 10), 3);
    }

    /// <inheritdoc />
    public override Rect CalculateBounds(Rect parent) => parent;

    // ─── Rendering (Closed State) ────────────────────────────────────

    /// <inheritdoc />
    protected override void OnRender(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        var clipped = bounds.Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0) return;

        var borderColor = IsFocused ? FocusColor : IsMouseOver ? HoverColor : Foreground;
        if (!IsEnabled)
            borderColor = DimColor(borderColor);

        // Fill background
        buffer.FillRect(clipped, new Cell(' ', Foreground, Background));

        // Draw border (3 rows tall)
        if (bounds is { Width: >= 2, Height: >= 2 })
        {
            var border = Styling.BorderStyle.Rounded(borderColor);
            DrawBorder(buffer, bounds, border.Chars, borderColor);
        }

        // Draw selected item text
        var textY = bounds.Y + bounds.Height / 2;
        var textX = bounds.X + 1;
        var maxTextWidth = Math.Max(0, bounds.Width - 4); // -2 border -2 for arrow
        var text = GetDisplayText(SelectedItem);
        var fg = IsEnabled ? Foreground : DimColor(Foreground);

        for (var i = 0; i < Math.Min(text.Length, maxTextWidth); i++)
        {
            var cx = textX + i;
            if (cx >= 0 && cx < buffer.Width && textY >= 0 && textY < buffer.Height)
                buffer.SetChar(cx, textY, text[i], fg, Background);
        }

        // Draw dropdown indicator ▼
        var arrowX = bounds.X + bounds.Width - 2;
        if (arrowX >= 0 && arrowX < buffer.Width && textY >= 0 && textY < buffer.Height)
            buffer.SetChar(arrowX, textY, IsDropDownOpen ? '\u25B2' : '\u25BC', borderColor, Background);

        // Render dropdown via popup overlay (handled by Application overlay stack)
        // ItemsPanel is rendered by the popup, not inline
    }

    /// <inheritdoc />
    public override IEnumerable<(Visual Child, Rect ChildParentBounds)> GetChildrenWithBounds(Rect myBounds)
    {
        // Yield the popup (zero-size in normal tree) for tree walkers
        yield return (_popup, myBounds);
    }

    /// <inheritdoc />
    protected internal override IEnumerable<FrameworkElement> GetLogicalChildren()
    {
        // ItemsPanel children are logical children for DataContext propagation
        foreach (var child in ItemsPanel.Children)
        {
            if (child is FrameworkElement fe)
                yield return fe;
        }
    }

    // ─── Input ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public override bool OnKeyEvent(KeyEvent e)
    {
        if (IsDropDownOpen)
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
                    IsDropDownOpen = false;
                    return true;
                case ConsoleKey.Escape:
                    IsDropDownOpen = false;
                    return true;
                default:
                    return false;
            }
        }
        else
        {
            switch (e.Key)
            {
                case ConsoleKey.DownArrow:
                    MoveSelection(1);
                    return true;
                case ConsoleKey.UpArrow:
                    MoveSelection(-1);
                    return true;
                case ConsoleKey.Enter or ConsoleKey.Spacebar:
                    IsDropDownOpen = true;
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <inheritdoc />
    public override void OnMouseEvent(MouseEvent e)
    {
        if (e is { Action: MouseAction.Press, Button: MouseButton.Left })
        {
            IsDropDownOpen = !IsDropDownOpen;
        }
    }

    /// <inheritdoc />
    public override void OnLostFocus()
    {
        base.OnLostFocus();
        IsDropDownOpen = false;
    }

    // ─── Display Text ─────────────────────────────────────────────────

    private static string GetDisplayText(object? item)
    {
        if (item == null) return "";
        if (item is ComboBoxItem cbi) return cbi.Content?.ToString() ?? "";
        return item.ToString() ?? "";
    }

    // ─── Selection Helpers ───────────────────────────────────────────

    private void MoveSelection(int delta)
    {
        var count = ItemsPanel.Children.Count;
        if (count == 0) return;

        var newIndex = Math.Clamp(SelectedIndex + delta, 0, count - 1);
        SetCurrentSelectedIndex(newIndex);
    }

    private void SelectFirst()
    {
        if (ItemsPanel.Children.Count > 0)
            SetCurrentSelectedIndex(0);
    }

    private void SelectLast()
    {
        var count = ItemsPanel.Children.Count;
        if (count > 0)
            SetCurrentSelectedIndex(count - 1);
    }

    // ─── Drawing Helpers ─────────────────────────────────────────────

    private void DrawBorder(CellBuffer buffer, Rect bounds, BorderChars chars, Color color)
    {
        if (chars.IsEmpty) return;

        for (var x = bounds.X + 1; x < bounds.X + bounds.Width - 1; x++)
        {
            if (x < 0 || x >= buffer.Width) continue;
            if (bounds.Y >= 0 && bounds.Y < buffer.Height)
                buffer.SetChar(x, bounds.Y, chars.Horizontal, color, Background);
            if (bounds.Y + bounds.Height - 1 >= 0 && bounds.Y + bounds.Height - 1 < buffer.Height)
                buffer.SetChar(x, bounds.Y + bounds.Height - 1, chars.Horizontal, color, Background);
        }

        for (var y = bounds.Y + 1; y < bounds.Y + bounds.Height - 1; y++)
        {
            if (y < 0 || y >= buffer.Height) continue;
            if (bounds.X >= 0 && bounds.X < buffer.Width)
                buffer.SetChar(bounds.X, y, chars.Vertical, color, Background);
            if (bounds.X + bounds.Width - 1 >= 0 && bounds.X + bounds.Width - 1 < buffer.Width)
                buffer.SetChar(bounds.X + bounds.Width - 1, y, chars.Vertical, color, Background);
        }

        if (bounds.X >= 0 && bounds.X < buffer.Width && bounds.Y >= 0 && bounds.Y < buffer.Height)
            buffer.SetChar(bounds.X, bounds.Y, chars.TopLeft, color, Background);
        if (bounds.X + bounds.Width - 1 >= 0 && bounds.X + bounds.Width - 1 < buffer.Width && bounds.Y >= 0 && bounds.Y < buffer.Height)
            buffer.SetChar(bounds.X + bounds.Width - 1, bounds.Y, chars.TopRight, color, Background);
        if (bounds.X >= 0 && bounds.X < buffer.Width && bounds.Y + bounds.Height - 1 >= 0 && bounds.Y + bounds.Height - 1 < buffer.Height)
            buffer.SetChar(bounds.X, bounds.Y + bounds.Height - 1, chars.BottomLeft, color, Background);
        if (bounds.X + bounds.Width - 1 >= 0 && bounds.X + bounds.Width - 1 < buffer.Width && bounds.Y + bounds.Height - 1 >= 0 && bounds.Y + bounds.Height - 1 < buffer.Height)
            buffer.SetChar(bounds.X + bounds.Width - 1, bounds.Y + bounds.Height - 1, chars.BottomRight, color, Background);
    }

    private static Color DimColor(Color c) =>
        new((byte)(c.R / 2), (byte)(c.G / 2), (byte)(c.B / 2));
}
