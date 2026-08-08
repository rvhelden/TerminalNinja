namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Tests for the WrapPanel layout container covering:
/// - Horizontal and vertical flow, and where the line breaks
/// - Line extent (every child in a line gets the line's cross-axis size)
/// - Collapsed (takes no room in the flow) versus Hidden (flows normally, paints nothing)
/// - Edge cases (no children, a child larger than the panel, lines past the far edge)
/// </summary>
public class WrapPanelTests
{
    private CellBuffer _buffer = null!;
    private const int BufferWidth = 100;
    private const int BufferHeight = 50;

    [Before(Test)]
    public Task Setup()
    {
        _buffer = new CellBuffer(BufferWidth, BufferHeight);
        return Task.CompletedTask;
    }

    [After(Test)]
    public Task Cleanup()
    {
        _buffer.Dispose();
        return Task.CompletedTask;
    }

    private static global::TerminalNinja.Controls.Border Box(Color background, int width, int height)
    {
        return new global::TerminalNinja.Controls.Border
        {
            Background = background,
            Width = Size.Absolute(width),
            Height = Size.Absolute(height)
        };
    }

    private static WrapPanel PanelOf(Orientation orientation, params global::TerminalNinja.Controls.Border[] children)
    {
        var panel = new WrapPanel { Orientation = orientation };
        foreach (var child in children)
        {
            panel.Children.Add(child);
        }

        return panel;
    }

    #region Properties

    [Test]
    public async Task Orientation_DefaultValue_IsHorizontal()
    {
        await Assert.That(new WrapPanel().Orientation).IsEqualTo(Orientation.Horizontal);
    }

    [Test]
    public async Task Orientation_SetVertical_UpdatesProperty()
    {
        await Assert.That(new WrapPanel { Orientation = Orientation.Vertical }.Orientation)
            .IsEqualTo(Orientation.Vertical);
    }

    #endregion

    #region Horizontal flow

    [Test]
    public async Task CalculateChildBounds_Horizontal_WrapsAtTheRightEdge()
    {
        var panel = PanelOf(Orientation.Horizontal,
            Box(Color.Red, 5, 2),
            Box(Color.Green, 5, 2),
            Box(Color.Blue, 5, 2));

        var rects = panel.CalculateChildBounds(new Rect(0, 0, 12, 10));

        await Assert.That(rects[0]).IsEqualTo(new Rect(0, 0, 5, 2));
        await Assert.That(rects[1]).IsEqualTo(new Rect(5, 0, 5, 2));
        // 15 > 12, so the third child starts a new line at the line's cross extent.
        await Assert.That(rects[2]).IsEqualTo(new Rect(0, 2, 5, 2));
    }

    [Test]
    public async Task CalculateChildBounds_Horizontal_LineTakesTheTallestChildsHeight()
    {
        var panel = PanelOf(Orientation.Horizontal,
            Box(Color.Red, 4, 2),
            Box(Color.Green, 4, 5),
            Box(Color.Blue, 4, 1));

        var rects = panel.CalculateChildBounds(new Rect(0, 0, 8, 20));

        // First line holds the 2-tall and the 5-tall child: the line is 5 tall and both get 5.
        await Assert.That(rects[0]).IsEqualTo(new Rect(0, 0, 4, 5));
        await Assert.That(rects[1]).IsEqualTo(new Rect(4, 0, 4, 5));
        // Second line starts below the whole first line.
        await Assert.That(rects[2]).IsEqualTo(new Rect(0, 5, 4, 1));
    }

    [Test]
    public async Task CalculateChildBounds_Horizontal_HonoursPanelOffset()
    {
        var panel = PanelOf(Orientation.Horizontal,
            Box(Color.Red, 3, 2),
            Box(Color.Green, 3, 2));

        var rects = panel.CalculateChildBounds(new Rect(7, 4, 5, 10));

        await Assert.That(rects[0]).IsEqualTo(new Rect(7, 4, 3, 2));
        await Assert.That(rects[1]).IsEqualTo(new Rect(7, 6, 3, 2));
    }

    #endregion

    #region Vertical flow

    [Test]
    public async Task CalculateChildBounds_Vertical_WrapsAtTheBottomEdge()
    {
        var panel = PanelOf(Orientation.Vertical,
            Box(Color.Red, 3, 2),
            Box(Color.Green, 3, 2),
            Box(Color.Blue, 3, 2));

        var rects = panel.CalculateChildBounds(new Rect(0, 0, 10, 5));

        await Assert.That(rects[0]).IsEqualTo(new Rect(0, 0, 3, 2));
        await Assert.That(rects[1]).IsEqualTo(new Rect(0, 2, 3, 2));
        // 6 > 5, so the third child starts a new column.
        await Assert.That(rects[2]).IsEqualTo(new Rect(3, 0, 3, 2));
    }

    [Test]
    public async Task CalculateChildBounds_Vertical_ColumnTakesTheWidestChildsWidth()
    {
        var panel = PanelOf(Orientation.Vertical,
            Box(Color.Red, 3, 2),
            Box(Color.Green, 6, 2),
            Box(Color.Blue, 2, 2));

        var rects = panel.CalculateChildBounds(new Rect(0, 0, 20, 4));

        await Assert.That(rects[0]).IsEqualTo(new Rect(0, 0, 6, 2));
        await Assert.That(rects[1]).IsEqualTo(new Rect(0, 2, 6, 2));
        await Assert.That(rects[2]).IsEqualTo(new Rect(6, 0, 2, 2));
    }

    #endregion

    #region Children that do not fit

    [Test]
    public async Task CalculateChildBounds_ChildWiderThanThePanel_IsClampedNotDropped()
    {
        var panel = PanelOf(Orientation.Horizontal,
            Box(Color.Red, 30, 2),
            Box(Color.Green, 4, 2));

        var rects = panel.CalculateChildBounds(new Rect(0, 0, 10, 10));

        // The over-long child is clamped to the panel rather than wrapping forever hunting for
        // room that does not exist; the next child starts a fresh line.
        await Assert.That(rects[0]).IsEqualTo(new Rect(0, 0, 10, 2));
        await Assert.That(rects[1]).IsEqualTo(new Rect(0, 2, 4, 2));
    }

    [Test]
    public async Task CalculateChildBounds_LinesPastTheBottom_AreTruncatedThenSkipped()
    {
        var panel = PanelOf(Orientation.Horizontal,
            Box(Color.Red, 5, 2),
            Box(Color.Green, 5, 2),
            Box(Color.Blue, 5, 2));

        var rects = panel.CalculateChildBounds(new Rect(0, 0, 5, 3));

        // One child per line; only three rows are available for three 2-tall lines.
        await Assert.That(rects[0]).IsEqualTo(new Rect(0, 0, 5, 2));
        await Assert.That(rects[1]).IsEqualTo(new Rect(0, 2, 5, 1)); // truncated to what is left
        await Assert.That(rects[2].Height).IsEqualTo(0);             // no room at all
    }

    [Test]
    public async Task CalculateChildBounds_ZeroSizedPanel_ProducesZeroSizeRects()
    {
        var panel = PanelOf(Orientation.Horizontal,
            Box(Color.Red, 5, 2),
            Box(Color.Green, 5, 2));

        var rects = panel.CalculateChildBounds(new Rect(0, 0, 0, 0));

        foreach (var rect in rects)
        {
            await Assert.That(rect.Width).IsEqualTo(0);
            await Assert.That(rect.Height).IsEqualTo(0);
        }
    }

    [Test]
    public async Task CalculateChildBounds_NoChildren_ReturnsEmpty()
    {
        await Assert.That(new WrapPanel().CalculateChildBounds(new Rect(0, 0, 10, 10)).Length)
            .IsEqualTo(0);
    }

    #endregion

    #region Rendering

    [Test]
    public void Render_NoChildren_DoesNotCrash()
    {
        new WrapPanel().Render(_buffer, new Rect(0, 0, 20, 10));
    }

    [Test]
    public async Task Render_Horizontal_PaintsWrappedCells()
    {
        var panel = PanelOf(Orientation.Horizontal,
            Box(Color.Red, 5, 2),
            Box(Color.Green, 5, 2),
            Box(Color.Blue, 5, 2));

        panel.Render(_buffer, new Rect(0, 0, 12, 10));

        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(4, 1).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(5, 0).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(9, 1).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(0, 2).Background).IsEqualTo(Color.Blue);
        await Assert.That(_buffer.GetCell(4, 3).Background).IsEqualTo(Color.Blue);
        // The 2 columns left over on the first line stay unpainted.
        await Assert.That(_buffer.GetCell(10, 0).Background).IsNotEqualTo(Color.Green);
    }

    [Test]
    public async Task Render_Vertical_PaintsWrappedCells()
    {
        var panel = PanelOf(Orientation.Vertical,
            Box(Color.Red, 3, 2),
            Box(Color.Green, 3, 2),
            Box(Color.Blue, 3, 2));

        panel.Render(_buffer, new Rect(0, 0, 10, 5));

        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(2, 1).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(0, 2).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(3, 0).Background).IsEqualTo(Color.Blue);
        await Assert.That(_buffer.GetCell(5, 1).Background).IsEqualTo(Color.Blue);
    }

    #endregion

    #region Visibility

    [Test]
    public async Task CalculateChildBounds_CollapsedChild_TakesNoRoomInTheFlow()
    {
        var first = Box(Color.Red, 4, 2);
        var collapsed = Box(Color.Green, 4, 2);
        var third = Box(Color.Blue, 4, 2);
        collapsed.Visibility = Visibility.Collapsed;

        var panel = PanelOf(Orientation.Horizontal, first, collapsed, third);

        var rects = panel.CalculateChildBounds(new Rect(0, 0, 8, 10));

        await Assert.That(rects[0]).IsEqualTo(new Rect(0, 0, 4, 2));
        await Assert.That(rects[1].Width).IsEqualTo(0);
        await Assert.That(rects[1].Height).IsEqualTo(0);
        // With the collapsed child gone the third fits on the first line.
        await Assert.That(rects[2]).IsEqualTo(new Rect(4, 0, 4, 2));
    }

    [Test]
    public async Task CalculateChildBounds_HiddenChild_FlowsNormally()
    {
        var first = Box(Color.Red, 4, 2);
        var hidden = Box(Color.Green, 4, 2);
        var third = Box(Color.Blue, 4, 2);
        hidden.Visibility = Visibility.Hidden;

        var panel = PanelOf(Orientation.Horizontal, first, hidden, third);

        var rects = panel.CalculateChildBounds(new Rect(0, 0, 8, 10));

        await Assert.That(rects[0]).IsEqualTo(new Rect(0, 0, 4, 2));
        await Assert.That(rects[1]).IsEqualTo(new Rect(4, 0, 4, 2));
        // The hidden child still pushed the third onto the next line.
        await Assert.That(rects[2]).IsEqualTo(new Rect(0, 2, 4, 2));
    }

    [Test]
    public async Task Render_HiddenChild_LeavesItsCellsAsBackground()
    {
        var first = Box(Color.Red, 4, 2);
        var hidden = Box(Color.Green, 4, 2);
        var third = Box(Color.Blue, 4, 2);
        hidden.Visibility = Visibility.Hidden;

        var panel = PanelOf(Orientation.Horizontal, first, hidden, third);

        panel.Render(_buffer, new Rect(0, 0, 8, 10));

        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(5, 0).Background).IsNotEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(0, 2).Background).IsEqualTo(Color.Blue);
    }

    #endregion

    #region GetPreferredSize

    [Test]
    public async Task GetPreferredSize_NoChildren_ReturnsZero()
    {
        await Assert.That(new WrapPanel().GetPreferredSize(new Rect(0, 0, 40, 20)))
            .IsEqualTo(new Size2D(0, 0));
    }

    [Test]
    public async Task GetPreferredSize_Horizontal_LongestLineAndTotalLineHeights()
    {
        var panel = PanelOf(Orientation.Horizontal,
            Box(Color.Red, 5, 2),
            Box(Color.Green, 5, 3),
            Box(Color.Blue, 4, 1));

        // Against a 12-wide parent the first two share a line (10 wide, 3 tall) and the third
        // starts a second line (4 wide, 1 tall).
        await Assert.That(panel.GetPreferredSize(new Rect(0, 0, 12, 20)))
            .IsEqualTo(new Size2D(10, 4));
    }

    [Test]
    public async Task GetPreferredSize_Vertical_LongestColumnAndTotalColumnWidths()
    {
        var panel = PanelOf(Orientation.Vertical,
            Box(Color.Red, 3, 2),
            Box(Color.Green, 6, 2),
            Box(Color.Blue, 2, 3));

        // Against a 5-tall parent the first two share a column (6 wide, 4 tall); the third
        // starts a second column (2 wide, 3 tall).
        await Assert.That(panel.GetPreferredSize(new Rect(0, 0, 20, 5)))
            .IsEqualTo(new Size2D(8, 4));
    }

    [Test]
    public async Task GetPreferredSize_AllChildrenCollapsed_ReturnsZero()
    {
        var child = Box(Color.Red, 5, 2);
        child.Visibility = Visibility.Collapsed;
        var panel = PanelOf(Orientation.Horizontal, child);

        await Assert.That(panel.GetPreferredSize(new Rect(0, 0, 40, 20)))
            .IsEqualTo(new Size2D(0, 0));
    }

    #endregion

    #region GetChildrenWithBounds

    [Test]
    public async Task GetChildrenWithBounds_MatchesArrangement_AndSkipsCollapsed()
    {
        var first = Box(Color.Red, 4, 2);
        var collapsed = Box(Color.Green, 4, 2);
        var third = Box(Color.Blue, 4, 2);
        collapsed.Visibility = Visibility.Collapsed;

        var panel = PanelOf(Orientation.Horizontal, first, collapsed, third);

        var pairs = panel.GetChildrenWithBounds(new Rect(0, 0, 8, 10)).ToList();

        await Assert.That(pairs.Count).IsEqualTo(2);
        await Assert.That(pairs[0].Child).IsEqualTo(first);
        await Assert.That(pairs[0].ChildParentBounds).IsEqualTo(new Rect(0, 0, 4, 2));
        await Assert.That(pairs[1].Child).IsEqualTo(third);
        await Assert.That(pairs[1].ChildParentBounds).IsEqualTo(new Rect(4, 0, 4, 2));
    }

    [Test]
    public async Task GetChildrenWithBounds_NoChildren_ReturnsEmpty()
    {
        await Assert.That(new WrapPanel().GetChildrenWithBounds(new Rect(0, 0, 20, 10)).Any()).IsFalse();
    }

    #endregion
}
