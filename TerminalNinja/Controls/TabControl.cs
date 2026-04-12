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

    private const int HeaderHeight = 2; // Tab strip row + border merge row

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
        if (tabs.Count == 0)
        {
            return;
        }

        var borderColor = Foreground;
        var mutedFg = DimColor(Foreground);

        // Calculate tab header widths
        var tabWidths = new int[tabs.Count];
        for (var i = 0; i < tabs.Count; i++)
        {
            tabWidths[i] = tabs[i].HeaderText.Length + 2; // +2 for padding
        }

        // ── Row 0: Tab headers ──
        var headerY = bounds.Y;
        var x = bounds.X;

        for (var i = 0; i < tabs.Count; i++)
        {
            var isSelected = i == SelectedIndex;
            var w = tabWidths[i];
            var fg = isSelected ? SelectedForeground : mutedFg;
            var bg = isSelected ? Background : Background;

            // Left edge
            if (i == 0)
            {
                SetCharSafe(buffer, x, headerY, '┌', borderColor, Background);
            }
            else
            {
                SetCharSafe(buffer, x, headerY, '┬', borderColor, Background);
            }
            x++;

            // Header text with padding
            SetCharSafe(buffer, x, headerY, '─', borderColor, Background);
            x++;
            var text = tabs[i].HeaderText;
            for (var c = 0; c < text.Length; c++)
            {
                SetCharSafe(buffer, x, headerY, text[c], fg, bg);
                x++;
            }
            SetCharSafe(buffer, x, headerY, '─', borderColor, Background);
            x++;
        }

        // Right cap and fill to end
        SetCharSafe(buffer, x, headerY, '┐', borderColor, Background);
        x++;
        // Fill remaining header row
        for (; x < bounds.Right; x++)
        {
            SetCharSafe(buffer, x, headerY, ' ', Foreground, Background);
        }

        // ── Row 1: Border merge row ──
        var mergeY = bounds.Y + 1;
        if (mergeY < bounds.Bottom)
        {
            x = bounds.X;

            // Left edge
            SetCharSafe(buffer, x, mergeY, '│', borderColor, Background);
            x++;

            // Fill with ─ but leave gap under selected tab
            var tabStart = bounds.X + 1;
            for (var i = 0; i < tabs.Count; i++)
            {
                var w = tabWidths[i];
                var isSelected = i == SelectedIndex;

                for (var c = 0; c < w; c++)
                {
                    if (isSelected)
                    {
                        SetCharSafe(buffer, x, mergeY, ' ', Foreground, Background);
                    }
                    else
                    {
                        SetCharSafe(buffer, x, mergeY, '─', borderColor, Background);
                    }
                    x++;
                }

                // Separator between tabs
                if (i < tabs.Count - 1)
                {
                    var nextSelected = (i + 1) == SelectedIndex;
                    if (isSelected || nextSelected)
                    {
                        SetCharSafe(buffer, x, mergeY, isSelected && nextSelected ? ' ' : isSelected ? '┐' : '┌', borderColor, Background);
                    }
                    else
                    {
                        SetCharSafe(buffer, x, mergeY, '┴', borderColor, Background);
                    }
                    x++;
                }
            }

            // After last tab to right edge
            var lastSelected = SelectedIndex == tabs.Count - 1;
            SetCharSafe(buffer, x, mergeY, lastSelected ? '┘' : '┴', borderColor, Background);
            x++;
            // Right border continues
            var rightEdgeX = bounds.Right - 1;
            for (; x < rightEdgeX; x++)
            {
                SetCharSafe(buffer, x, mergeY, '─', borderColor, Background);
            }
            SetCharSafe(buffer, rightEdgeX, mergeY, '┐', borderColor, Background);
        }

        // ── Content area: rows 2 to bottom-1, with left/right borders ──
        var contentTop = bounds.Y + HeaderHeight;
        var contentBottom = bounds.Bottom - 1;

        for (var y = contentTop; y < contentBottom; y++)
        {
            SetCharSafe(buffer, bounds.X, y, '│', borderColor, Background);
            SetCharSafe(buffer, bounds.Right - 1, y, '│', borderColor, Background);
        }

        // ── Bottom border ──
        if (contentBottom >= bounds.Y && contentBottom < bounds.Bottom)
        {
            SetCharSafe(buffer, bounds.X, contentBottom, '└', borderColor, Background);
            for (var bx = bounds.X + 1; bx < bounds.Right - 1; bx++)
            {
                SetCharSafe(buffer, bx, contentBottom, '─', borderColor, Background);
            }
            SetCharSafe(buffer, bounds.Right - 1, contentBottom, '┘', borderColor, Background);
        }

        // ── Render selected tab content ──
        if (SelectedIndex >= 0 && SelectedIndex < tabs.Count)
        {
            var selectedTab = tabs[SelectedIndex];
            var contentBounds = new Rect(
                bounds.X + 1, contentTop,
                Math.Max(0, bounds.Width - 2),
                Math.Max(0, contentBottom - contentTop));

            if (contentBounds.Width > 0 && contentBounds.Height > 0)
            {
                // Render the TabItem's content via its ContentPresenter
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
                myBounds.X + 1, myBounds.Y + HeaderHeight,
                Math.Max(0, myBounds.Width - 2),
                Math.Max(0, myBounds.Height - HeaderHeight - 1));
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
