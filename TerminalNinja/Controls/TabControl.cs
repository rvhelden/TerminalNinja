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
    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        var clipped = bounds.Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0) return;

        // Fill background
        buffer.FillRect(clipped, new Cell(' ', Foreground, Background));

        var tabs = GetTabItems();
        if (tabs.Count == 0) return;

        // Auto-select first tab if none selected
        if (SelectedIndex < 0) SelectedIndex = 0;

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
    public override void OnKeyEvent(KeyEvent e)
    {
        var count = ItemsPanel.Children.Count;
        if (count == 0) return;

        switch (e.Key)
        {
            case ConsoleKey.RightArrow:
                SelectedIndex = Math.Min(SelectedIndex + 1, count - 1);
                break;
            case ConsoleKey.LeftArrow:
                SelectedIndex = Math.Max(SelectedIndex - 1, 0);
                break;
            case ConsoleKey.Home:
                SelectedIndex = 0;
                break;
            case ConsoleKey.End:
                SelectedIndex = count - 1;
                break;
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
