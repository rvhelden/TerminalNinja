namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Tests for the ScrollViewer control covering:
/// - Default property values
/// - Offset clamping
/// - Viewport and extent calculation
/// - Content clipping via intermediate buffer
/// - Keyboard navigation
/// - Mouse wheel scrolling
/// - Scroll indicators
/// - ScrollIntoView helper
/// - XAML loading
/// </summary>
public class ScrollViewerTests
{
    #region Default Values

    [Test]
    public async Task VerticalScrollBarVisibility_Default_IsVisible()
    {
        var sv = new ScrollViewer();
        await Assert.That(sv.VerticalScrollBarVisibility).IsEqualTo(ScrollBarVisibility.Visible);
    }

    [Test]
    public async Task HorizontalScrollBarVisibility_Default_IsDisabled()
    {
        var sv = new ScrollViewer();
        await Assert.That(sv.HorizontalScrollBarVisibility).IsEqualTo(ScrollBarVisibility.Disabled);
    }

    [Test]
    public async Task VerticalOffset_Default_IsZero()
    {
        var sv = new ScrollViewer();
        await Assert.That(sv.VerticalOffset).IsEqualTo(0);
    }

    [Test]
    public async Task HorizontalOffset_Default_IsZero()
    {
        var sv = new ScrollViewer();
        await Assert.That(sv.HorizontalOffset).IsEqualTo(0);
    }

    [Test]
    public async Task Focusable_Default_IsFalse()
    {
        // ScrollViewer is a container — should not steal focus from children
        var sv = new ScrollViewer();
        await Assert.That(sv.Focusable).IsFalse();
    }

    #endregion

    #region Content Rendering

    [Test]
    public async Task Render_SmallContent_RendersDirectly()
    {
        var sv = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            Content = new TextBlock { Text = "Hello" }
        };

        using var buffer = new CellBuffer(20, 5);
        sv.Render(buffer, new Rect(0, 0, 20, 5));

        await Assert.That(buffer.GetCell(0, 0).Codepoint).IsEqualTo('H');
        await Assert.That(buffer.GetCell(4, 0).Codepoint).IsEqualTo('o');
    }

    [Test]
    public async Task Render_TallContent_ClipsToViewport()
    {
        // Create content taller than the viewport
        var panel = new StackPanel { Orientation = Orientation.Vertical };
        for (var i = 0; i < 20; i++)
        {
            var tb = new TextBlock { Text = $"Line {i:D2}" };
            StackPanel.SetSizeMode(tb, ChildSizeMode.Fixed);
            StackPanel.SetFixedSize(tb, 1);
            panel.Children.Add(tb);
        }

        var sv = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            Content = panel
        };

        using var buffer = new CellBuffer(20, 5);
        sv.Render(buffer, new Rect(0, 0, 20, 5));

        // First line should show "Line 00"
        await Assert.That(buffer.GetCell(0, 0).Codepoint).IsEqualTo('L');
        await Assert.That(buffer.GetCell(5, 0).Codepoint).IsEqualTo('0');
    }

    [Test]
    public async Task Render_WithVerticalOffset_ShowsScrolledContent()
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical };
        for (var i = 0; i < 20; i++)
        {
            var tb = new TextBlock { Text = $"Line {i:D2}" };
            StackPanel.SetSizeMode(tb, ChildSizeMode.Fixed);
            StackPanel.SetFixedSize(tb, 1);
            panel.Children.Add(tb);
        }

        var sv = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            Content = panel,
            VerticalOffset = 5
        };

        using var buffer = new CellBuffer(20, 5);
        sv.Render(buffer, new Rect(0, 0, 20, 5));

        // First visible line should be "Line 05"
        await Assert.That(buffer.GetCell(0, 0).Codepoint).IsEqualTo('L');
        await Assert.That(buffer.GetCell(5, 0).Codepoint).IsEqualTo('0');
        await Assert.That(buffer.GetCell(6, 0).Codepoint).IsEqualTo('5');
    }

    [Test]
    public async Task Render_NoContent_DoesNotThrow()
    {
        var sv = new ScrollViewer();

        using var buffer = new CellBuffer(20, 5);
        sv.Render(buffer, new Rect(0, 0, 20, 5));

        // Should not throw — just renders empty background
        await Assert.That(sv.ViewportHeight).IsGreaterThan(0);
    }

    #endregion

    #region Viewport and Extent

    [Test]
    public async Task ViewportAndExtent_TallContent_CorrectValues()
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical };
        for (var i = 0; i < 30; i++)
        {
            var tb = new TextBlock { Text = $"Line {i}" };
            StackPanel.SetSizeMode(tb, ChildSizeMode.Fixed);
            StackPanel.SetFixedSize(tb, 1);
            panel.Children.Add(tb);
        }

        var sv = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            Content = panel
        };

        using var buffer = new CellBuffer(20, 10);
        sv.Render(buffer, new Rect(0, 0, 20, 10));

        await Assert.That(sv.ViewportHeight).IsEqualTo(10);
        await Assert.That(sv.ExtentHeight).IsEqualTo(30);
        await Assert.That(sv.ScrollableHeight).IsEqualTo(20);
    }

    [Test]
    public async Task ViewportWidth_WithVerticalIndicator_ReducedByOne()
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical };
        for (var i = 0; i < 30; i++)
        {
            var tb = new TextBlock { Text = $"Line {i}" };
            StackPanel.SetSizeMode(tb, ChildSizeMode.Fixed);
            StackPanel.SetFixedSize(tb, 1);
            panel.Children.Add(tb);
        }

        var sv = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            Content = panel
        };

        using var buffer = new CellBuffer(20, 10);
        sv.Render(buffer, new Rect(0, 0, 20, 10));

        // Viewport width should be reduced by 1 for the vertical indicator
        await Assert.That(sv.ViewportWidth).IsEqualTo(19);
    }

    #endregion

    #region Keyboard Navigation

    [Test]
    public async Task DownArrow_ScrollsDownOneRow()
    {
        var sv = CreateTallScrollViewer();
        RenderOnce(sv, 20, 5);

        sv.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false));

        await Assert.That(sv.VerticalOffset).IsEqualTo(1);
    }

    [Test]
    public async Task UpArrow_ScrollsUpOneRow()
    {
        var sv = CreateTallScrollViewer();
        sv.VerticalOffset = 5;
        RenderOnce(sv, 20, 5);

        sv.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(sv.VerticalOffset).IsEqualTo(4);
    }

    [Test]
    public async Task UpArrow_AtTop_StaysAtZero()
    {
        var sv = CreateTallScrollViewer();
        RenderOnce(sv, 20, 5);

        sv.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(sv.VerticalOffset).IsEqualTo(0);
    }

    [Test]
    public async Task PageDown_ScrollsByViewportHeight()
    {
        var sv = CreateTallScrollViewer();
        RenderOnce(sv, 20, 5);

        sv.OnKeyEvent(new KeyEvent(ConsoleKey.PageDown, '\0', false, false, false));

        await Assert.That(sv.VerticalOffset).IsEqualTo(sv.ViewportHeight);
    }

    [Test]
    public async Task PageUp_ScrollsByViewportHeight()
    {
        var sv = CreateTallScrollViewer();
        sv.VerticalOffset = 10;
        RenderOnce(sv, 20, 5);

        sv.OnKeyEvent(new KeyEvent(ConsoleKey.PageUp, '\0', false, false, false));

        await Assert.That(sv.VerticalOffset).IsEqualTo(10 - sv.ViewportHeight);
    }

    [Test]
    public async Task Home_ScrollsToTop()
    {
        var sv = CreateTallScrollViewer();
        sv.VerticalOffset = 10;
        RenderOnce(sv, 20, 5);

        sv.OnKeyEvent(new KeyEvent(ConsoleKey.Home, '\0', false, false, false));

        await Assert.That(sv.VerticalOffset).IsEqualTo(0);
    }

    [Test]
    public async Task End_ScrollsToBottom()
    {
        var sv = CreateTallScrollViewer();
        RenderOnce(sv, 20, 5);

        sv.OnKeyEvent(new KeyEvent(ConsoleKey.End, '\0', false, false, false));

        await Assert.That(sv.VerticalOffset).IsEqualTo(sv.ScrollableHeight);
    }

    [Test]
    public async Task CtrlHome_ScrollsToTopLeft()
    {
        var sv = CreateTallScrollViewer();
        sv.VerticalOffset = 5;
        RenderOnce(sv, 20, 5);

        sv.OnKeyEvent(new KeyEvent(ConsoleKey.Home, '\0', false, false, true));

        await Assert.That(sv.VerticalOffset).IsEqualTo(0);
        await Assert.That(sv.HorizontalOffset).IsEqualTo(0);
    }

    [Test]
    public async Task CtrlEnd_ScrollsToBottomRight()
    {
        var sv = CreateTallScrollViewer();
        RenderOnce(sv, 20, 5);

        sv.OnKeyEvent(new KeyEvent(ConsoleKey.End, '\0', false, false, true));

        await Assert.That(sv.VerticalOffset).IsEqualTo(sv.ScrollableHeight);
    }

    [Test]
    public async Task ArrowKeys_WhenDisabled_DoNotScroll()
    {
        var sv = CreateTallScrollViewer();
        sv.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        RenderOnce(sv, 20, 5);

        sv.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false));

        await Assert.That(sv.VerticalOffset).IsEqualTo(0);
    }

    #endregion

    #region Mouse Wheel

    [Test]
    public async Task ScrollDown_IncreasesVerticalOffset()
    {
        var sv = CreateTallScrollViewer();
        RenderOnce(sv, 20, 5);

        sv.OnMouseEvent(new MouseEvent(5, 2, MouseButton.None, MouseAction.ScrollDown));

        await Assert.That(sv.VerticalOffset).IsEqualTo(3);
    }

    [Test]
    public async Task ScrollUp_DecreasesVerticalOffset()
    {
        var sv = CreateTallScrollViewer();
        sv.VerticalOffset = 10;
        RenderOnce(sv, 20, 5);

        sv.OnMouseEvent(new MouseEvent(5, 2, MouseButton.None, MouseAction.ScrollUp));

        await Assert.That(sv.VerticalOffset).IsEqualTo(7);
    }

    [Test]
    public async Task ScrollUp_AtTop_StaysAtZero()
    {
        var sv = CreateTallScrollViewer();
        RenderOnce(sv, 20, 5);

        sv.OnMouseEvent(new MouseEvent(5, 2, MouseButton.None, MouseAction.ScrollUp));

        await Assert.That(sv.VerticalOffset).IsEqualTo(0);
    }

    #endregion

    #region Scroll Indicators

    [Test]
    public async Task Render_VerticalIndicatorVisible_DrawsTrack()
    {
        var sv = CreateTallScrollViewer();
        sv.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;

        using var buffer = new CellBuffer(20, 10);
        sv.Render(buffer, new Rect(0, 0, 20, 10));

        // The rightmost column should contain indicator characters
        var trackCell = buffer.GetCell(19, 5);
        await Assert.That(trackCell.Codepoint == '░' || trackCell.Codepoint == '█').IsTrue();
    }

    [Test]
    public async Task Render_VerticalIndicatorAuto_HiddenWhenContentFits()
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical };
        var tb = new TextBlock { Text = "Short" };
        StackPanel.SetSizeMode(tb, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(tb, 1);
        panel.Children.Add(tb);

        var sv = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = panel
        };

        using var buffer = new CellBuffer(20, 10);
        sv.Render(buffer, new Rect(0, 0, 20, 10));

        // Viewport should use full width (no indicator)
        await Assert.That(sv.ViewportWidth).IsEqualTo(20);
    }

    #endregion

    #region ScrollIntoView

    [Test]
    public async Task ScrollIntoView_RowAboveViewport_ScrollsUp()
    {
        var sv = CreateTallScrollViewer();
        sv.VerticalOffset = 10;
        RenderOnce(sv, 20, 5);

        sv.ScrollIntoView(5);

        await Assert.That(sv.VerticalOffset).IsEqualTo(5);
    }

    [Test]
    public async Task ScrollIntoView_RowBelowViewport_ScrollsDown()
    {
        var sv = CreateTallScrollViewer();
        RenderOnce(sv, 20, 5);

        sv.ScrollIntoView(10);

        await Assert.That(sv.VerticalOffset).IsGreaterThan(0);
    }

    [Test]
    public async Task ScrollIntoView_RowInViewport_NoChange()
    {
        var sv = CreateTallScrollViewer();
        sv.VerticalOffset = 5;
        RenderOnce(sv, 20, 10);

        sv.ScrollIntoView(7);

        await Assert.That(sv.VerticalOffset).IsEqualTo(5);
    }

    #endregion

    #region CellBuffer.CopyRegionTo

    [Test]
    public async Task CopyRegionTo_FullRegion_CopiesCorrectly()
    {
        using var source = new CellBuffer(10, 5);
        using var target = new CellBuffer(10, 5);

        source.SetChar(0, 0, 'A', Color.White, Color.Black);
        source.SetChar(9, 4, 'Z', Color.White, Color.Black);

        source.CopyRegionTo(target, new Rect(0, 0, 10, 5), 0, 0);

        await Assert.That(target.GetCell(0, 0).Codepoint).IsEqualTo('A');
        await Assert.That(target.GetCell(9, 4).Codepoint).IsEqualTo('Z');
    }

    [Test]
    public async Task CopyRegionTo_PartialRegion_CopiesSubset()
    {
        using var source = new CellBuffer(10, 10);
        using var target = new CellBuffer(5, 5);

        source.SetChar(3, 3, 'X', Color.White, Color.Black);

        source.CopyRegionTo(target, new Rect(2, 2, 5, 5), 0, 0);

        // (3,3) in source is at offset (1,1) in the copied region
        await Assert.That(target.GetCell(1, 1).Codepoint).IsEqualTo('X');
    }

    [Test]
    public async Task CopyRegionTo_OutOfBoundsTarget_ClipsCorrectly()
    {
        using var source = new CellBuffer(10, 10);
        using var target = new CellBuffer(5, 5);

        source.SetChar(0, 0, 'A', Color.White, Color.Black);
        source.SetChar(9, 9, 'Z', Color.White, Color.Black);

        // Copy a large region but target is small — should clip
        source.CopyRegionTo(target, new Rect(0, 0, 10, 10), 0, 0);

        await Assert.That(target.GetCell(0, 0).Codepoint).IsEqualTo('A');
        // (9,9) is outside target bounds, should not crash
    }

    #endregion

    #region XAML Loading

    [Test]
    public async Task Xaml_ScrollViewer_ParsesVisibility()
    {
        var xaml = """
            <ScrollViewer xmlns="http://schemas.terminalninja.dev/xaml"
                          VerticalScrollBarVisibility="Auto"
                          HorizontalScrollBarVisibility="Hidden">
                <TextBlock Text="Test" />
            </ScrollViewer>
            """;

        var sv = TerminalXaml.Load<ScrollViewer>(xaml);

        await Assert.That(sv.VerticalScrollBarVisibility).IsEqualTo(ScrollBarVisibility.Auto);
        await Assert.That(sv.HorizontalScrollBarVisibility).IsEqualTo(ScrollBarVisibility.Hidden);
        await Assert.That(sv.Content).IsNotNull();
    }

    [Test]
    public async Task Xaml_ScrollViewer_WithStackPanelContent()
    {
        var xaml = """
            <ScrollViewer xmlns="http://schemas.terminalninja.dev/xaml"
                          VerticalScrollBarVisibility="Visible">
                <StackPanel Orientation="Vertical">
                    <TextBlock Text="Line 1" StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" />
                    <TextBlock Text="Line 2" StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" />
                </StackPanel>
            </ScrollViewer>
            """;

        var sv = TerminalXaml.Load<ScrollViewer>(xaml);

        await Assert.That(sv.Content).IsTypeOf<StackPanel>();
        var panel = (StackPanel)sv.Content!;
        await Assert.That(panel.Children.Count).IsEqualTo(2);
    }

    #endregion

    #region Helpers

    private static ScrollViewer CreateTallScrollViewer()
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical };
        for (var i = 0; i < 30; i++)
        {
            var tb = new TextBlock { Text = $"Line {i:D2}" };
            StackPanel.SetSizeMode(tb, ChildSizeMode.Fixed);
            StackPanel.SetFixedSize(tb, 1);
            panel.Children.Add(tb);
        }

        return new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            Content = panel
        };
    }

    /// <summary>
    /// Renders the ScrollViewer once to initialize viewport/extent measurements.
    /// </summary>
    private static void RenderOnce(ScrollViewer sv, int width, int height)
    {
        using var buffer = new CellBuffer(width, height);
        sv.Render(buffer, new Rect(0, 0, width, height));
    }

    #endregion
}
