using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Controls.Primitives;
using TerminalNinja.Input;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// A control that displays a strip of selectable tab headers and shows the
/// content of the selected tab. Extends <see cref="Selector"/> for tab selection.
/// Corresponds to WPF's System.Windows.Controls.TabControl.
/// </summary>
[ContentProperty("Items")]
[RuntimeNameProperty("Name")]
public sealed class TabControl : Selector
{
    public TabControl()
    {
        DefaultStyleKey = typeof(TabControl);

        // Sort last in tab order rather than first. The strip sits at the top of its own subtree
        // and FocusManager orders by TabIndex then Y, so with the inherited 0 the control always
        // won the focus search — focus landed on the strip and every key, including up and down,
        // went to OnKeyEvent here, which handles only left and right. The list the user was
        // looking at never saw a key. The strip is still reachable, just after its content.
        TabIndex = int.MaxValue;
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty SelectedBackgroundProperty =
        DependencyProperty.Register(nameof(SelectedBackground), typeof(Color), typeof(TabControl),
            new FrameworkPropertyMetadata(Color.Blue, affectsRender: true));

    public static readonly DependencyProperty FocusColorProperty =
        DependencyProperty.Register(nameof(FocusColor), typeof(Color), typeof(TabControl),
            new FrameworkPropertyMetadata(Color.Cyan, affectsRender: true));

    public Color FocusColor { get => (Color)GetValue(FocusColorProperty)!; set => SetValue(FocusColorProperty, value); }

    public static readonly DependencyProperty SelectedForegroundProperty =
        DependencyProperty.Register(nameof(SelectedForeground), typeof(Color), typeof(TabControl),
            new FrameworkPropertyMetadata(Color.White, affectsRender: true));

    /// <summary>Gets or sets the background color for the selected tab header.</summary>
    public Color SelectedBackground
    {
        get => (Color)GetValue(SelectedBackgroundProperty)!;
        set => SetValue(SelectedBackgroundProperty, value);
    }

    /// <summary>Gets or sets the foreground color for the selected tab header.</summary>
    public Color SelectedForeground
    {
        get => (Color)GetValue(SelectedForegroundProperty)!;
        set => SetValue(SelectedForegroundProperty, value);
    }

    // ─── Container Generation ────────────────────────────────────────

    /// <summary>
    /// Moves focus out of the tab that just left, so keys follow the content on screen.
    /// </summary>
    /// <remarks>
    /// <see cref="GetChildrenWithBounds"/> reports only the selected tab's content, so after a tab
    /// change a focused element in the old tab is no longer reachable from anywhere — yet
    /// <see cref="Input.FocusManager"/> still holds it and still delivers keys to it. The symptom is
    /// nasty: the list on screen sits inert while the arrow keys drive an invisible one behind it.
    ///
    /// Only focus that was inside this control is moved. A tab changed from elsewhere — a shortcut
    /// handled at application level while focus sits in a sidebar — leaves that focus alone.
    /// </remarks>
    protected override void OnSelectionChanged(IList<object> removed, IList<object> added)
    {
        base.OnSelectionChanged(removed, added);

        var focusManager = App.Application.Current?.FocusManager;
        if (focusManager?.FocusedElement is not { } focused || ReferenceEquals(focused, this))
        {
            return;
        }

        // Was the focus inside the tab that just left? Walk up rather than down: the old subtree
        // is exactly what is no longer enumerable from here.
        var insideThisControl = false;
        for (Visual? ancestor = focused; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ReferenceEquals(ancestor, this))
            {
                insideThisControl = true;
                break;
            }
        }

        if (!insideThisControl)
        {
            return;
        }

        var viewport = App.Application.Current?.Renderer?.Viewport ?? default;
        focusManager.ClearFocus();
        focusManager.FocusNext(this, viewport);
    }

    /// <inheritdoc />
    protected override bool IsItemItsOwnContainer(object item) => item is TabItem;

    /// <inheritdoc />
    protected override UIElement CreateContainerForItem(object item)
    {
        var ti = new TabItem
        {
            Background = Background,
            Foreground = Foreground,
            SelectedBackground = SelectedBackground,
            SelectedForeground = SelectedForeground
        };

        if (item is string s)
        {
            ti.Header = s;
            ti.Content = new TextBlock { Text = s };
        }
        else
        {
            ti.Header = item?.ToString() ?? "";
            if (ItemTemplate != null)
            {
                var content = ItemTemplate.CreateContent();
                if (content is FrameworkElement fe)
                    fe.DataContext = item;
                ti.Content = content;
            }
            else
            {
                ti.Content = new TextBlock { Text = item?.ToString() ?? "" };
            }
        }

        return ti;
    }

    /// <inheritdoc />
    protected override void PrepareContainerForItem(UIElement container, object item)
    {
        base.PrepareContainerForItem(container, item);
        if (container is TabItem ti)
        {
            ti.SelectedBackground = SelectedBackground;
            ti.SelectedForeground = SelectedForeground;
            ti.IsSelected = SelectedItem == item;
        }
    }

    // ─── Layout ──────────────────────────────────────────────────────

    private const int HeaderHeight = 3; // Tab text row + underline row + separator

    /// <inheritdoc />
    public override Size2D GetPreferredSize(Rect parent) => new(parent.Width, parent.Height);

    /// <inheritdoc />
    public override Rect CalculateBounds(Rect parent) => parent;

    // ─── Rendering ───────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnRender(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        var clipped = bounds.Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0) return;

        // Fill background
        buffer.FillRect(clipped, new Cell(' ', Foreground, Background));

        var tabs = GetTabItems();
        if (tabs.Count == 0) return;

        // Auto-select first tab if none selected. SetCurrentSelectedIndex, not the setter: this
        // runs during render, and detaching a two-way SelectedIndex binding on the first paint
        // would break it before the user had touched anything.
        if (SelectedIndex < 0) SetCurrentSelectedIndex(0);

        var accentColor = IsFocused ? FocusColor : Foreground;
        var mutedFg = DimColor(Foreground);

        // ── Row 0: Tab header text ──
        var headerY = bounds.Y;
        var x = bounds.X + 1; // 1-char left margin

        for (var i = 0; i < tabs.Count; i++)
        {
            var isSelected = i == SelectedIndex;
            var text = tabs[i].HeaderText;
            var fg = isSelected ? SelectedForeground : mutedFg;

            // Space before tab (gap between tabs)
            if (i > 0)
            {
                SetCharSafe(buffer, x, headerY, ' ', Foreground, Background);
                x++;
            }

            // Tab text
            for (var c = 0; c < text.Length && x < bounds.Right; c++)
            {
                SetCharSafe(buffer, x, headerY, text[c], fg, Background);
                x++;
            }
        }

        // ── Row 1: Underline bar (▄ under selected tab) ──
        var underlineY = bounds.Y + 1;
        if (underlineY < bounds.Bottom)
        {
            x = bounds.X + 1;
            for (var i = 0; i < tabs.Count; i++)
            {
                var isSelected = i == SelectedIndex;
                var textLen = tabs[i].HeaderText.Length;

                if (i > 0) { x++; } // gap

                for (var c = 0; c < textLen && x < bounds.Right; c++)
                {
                    if (isSelected)
                    {
                        // Half-block underline: ▄ with accent color as foreground
                        SetCharSafe(buffer, x, underlineY, '\u2584', accentColor, Background);
                    }
                    x++;
                }
            }
        }

        // ── Row 2: Thin separator line ──
        var sepY = bounds.Y + 2;
        if (sepY < bounds.Bottom)
        {
            var sepColor = DimColor(Foreground);
            for (var sx = bounds.X; sx < bounds.Right; sx++)
            {
                SetCharSafe(buffer, sx, sepY, '─', sepColor, Background);
            }
        }

        // ── Content area: row 3+ ──
        var contentTop = bounds.Y + HeaderHeight;
        if (SelectedIndex >= 0 && SelectedIndex < tabs.Count)
        {
            var selectedTab = tabs[SelectedIndex];
            var contentBounds = new Rect(
                bounds.X, contentTop,
                bounds.Width,
                Math.Max(0, bounds.Bottom - contentTop));

            if (contentBounds.Width > 0 && contentBounds.Height > 0)
            {
                selectedTab.Render(buffer, contentBounds);
            }
        }
    }

    private List<TabItem> GetTabItems()
    {
        var result = new List<TabItem>();
        foreach (var child in ItemsPanel.Children)
        {
            if (child is TabItem ti)
                result.Add(ti);
        }
        return result;
    }

    /// <inheritdoc />
    public override IEnumerable<(Visual Child, Rect ChildParentBounds)> GetChildrenWithBounds(Rect myBounds)
    {
        // Report the selected tab's content for hit-testing
        var tabs = GetTabItems();
        if (SelectedIndex >= 0 && SelectedIndex < tabs.Count)
        {
            var contentBounds = new Rect(
                myBounds.X, myBounds.Y + HeaderHeight,
                myBounds.Width,
                Math.Max(0, myBounds.Height - HeaderHeight));
            yield return (tabs[SelectedIndex], contentBounds);
        }
    }

    /// <inheritdoc />
    protected internal override IEnumerable<FrameworkElement> GetLogicalChildren()
    {
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
        var count = ItemsPanel.Children.Count;
        if (count == 0) return false;

        switch (e.Key)
        {
            case ConsoleKey.RightArrow:
                SetCurrentSelectedIndex(Math.Min(SelectedIndex + 1, count - 1));
                return true;
            case ConsoleKey.LeftArrow:
                SetCurrentSelectedIndex(Math.Max(SelectedIndex - 1, 0));
                return true;
            case ConsoleKey.Home:
                SetCurrentSelectedIndex(0);
                return true;
            case ConsoleKey.End:
                SetCurrentSelectedIndex(count - 1);
                return true;
            default:
                return false;
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private static void SetCharSafe(CellBuffer buffer, int x, int y, char c, Color fg, Color bg)
    {
        if (x >= 0 && x < buffer.Width && y >= 0 && y < buffer.Height)
            buffer.SetChar(x, y, c, fg, bg);
    }

    private static Color DimColor(Color c) =>
        new((byte)(c.R / 2), (byte)(c.G / 2), (byte)(c.B / 2));
}
