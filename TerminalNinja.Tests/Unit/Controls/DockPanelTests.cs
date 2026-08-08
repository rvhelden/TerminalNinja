namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Tests for the DockPanel layout container covering:
/// - The Dock attached property and LastChildFill
/// - Docking against each of the four edges, in declaration order
/// - Exact cell accounting (docked rectangles tile the panel with no gap or overlap)
/// - Collapsed (zero allocation) versus Hidden (normal allocation, painted as background)
/// - Edge cases (no children, no space left, a child larger than what remains)
/// </summary>
public class DockPanelTests
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

    private static global::TerminalNinja.Controls.Border Box(Color background, int? width = null, int? height = null)
    {
        var border = new global::TerminalNinja.Controls.Border { Background = background };
        if (width.HasValue)
        {
            border.Width = Size.Absolute(width.Value);
        }

        if (height.HasValue)
        {
            border.Height = Size.Absolute(height.Value);
        }

        return border;
    }

    #region Attached Property - Dock

    [Test]
    public async Task GetDock_DefaultValue_ReturnsLeft()
    {
        var control = new global::TerminalNinja.Controls.Border();

        await Assert.That(DockPanel.GetDock(control)).IsEqualTo(Dock.Left);
    }

    [Test]
    public async Task SetDock_EachEdge_RoundTrips()
    {
        var control = new global::TerminalNinja.Controls.Border();

        DockPanel.SetDock(control, Dock.Top);
        await Assert.That(DockPanel.GetDock(control)).IsEqualTo(Dock.Top);

        DockPanel.SetDock(control, Dock.Right);
        await Assert.That(DockPanel.GetDock(control)).IsEqualTo(Dock.Right);

        DockPanel.SetDock(control, Dock.Bottom);
        await Assert.That(DockPanel.GetDock(control)).IsEqualTo(Dock.Bottom);

        DockPanel.SetDock(control, Dock.Left);
        await Assert.That(DockPanel.GetDock(control)).IsEqualTo(Dock.Left);
    }

    [Test]
    public async Task GetDock_NullControl_ThrowsArgumentNullException()
    {
        await Assert.That(() => DockPanel.GetDock(null!)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task SetDock_NullControl_ThrowsArgumentNullException()
    {
        await Assert.That(() => DockPanel.SetDock(null!, Dock.Top)).ThrowsExactly<ArgumentNullException>();
    }

    #endregion

    #region LastChildFill

    [Test]
    public async Task LastChildFill_DefaultValue_IsTrue()
    {
        await Assert.That(new DockPanel().LastChildFill).IsTrue();
    }

    [Test]
    public async Task LastChildFill_SetFalse_UpdatesProperty()
    {
        var panel = new DockPanel { LastChildFill = false };

        await Assert.That(panel.LastChildFill).IsFalse();
    }

    #endregion

    #region Arrangement

    [Test]
    public async Task CalculateChildBounds_AllFourEdgesThenFill_ShrinksRemainingRect()
    {
        var top = Box(Color.Red, height: 2);
        var bottom = Box(Color.Green, height: 3);
        var left = Box(Color.Blue, width: 4);
        var right = Box(Color.Yellow, width: 5);
        var fill = Box(Color.Cyan);

        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(bottom, Dock.Bottom);
        DockPanel.SetDock(left, Dock.Left);
        DockPanel.SetDock(right, Dock.Right);

        var panel = new DockPanel();
        panel.Children.Add(top);
        panel.Children.Add(bottom);
        panel.Children.Add(left);
        panel.Children.Add(right);
        panel.Children.Add(fill);

        var bounds = new Rect(0, 0, 40, 20);
        var rects = panel.CalculateChildBounds(bounds);

        await Assert.That(rects[0]).IsEqualTo(new Rect(0, 0, 40, 2));   // top
        await Assert.That(rects[1]).IsEqualTo(new Rect(0, 17, 40, 3));  // bottom, from the far edge
        await Assert.That(rects[2]).IsEqualTo(new Rect(0, 2, 4, 15));   // left, inside the band
        await Assert.That(rects[3]).IsEqualTo(new Rect(35, 2, 5, 15));  // right, from the far edge
        await Assert.That(rects[4]).IsEqualTo(new Rect(4, 2, 31, 15));  // fill takes the remainder
    }

    [Test]
    public async Task CalculateChildBounds_LastChildFill_SpendsEveryCell()
    {
        var top = Box(Color.Red, height: 2);
        var left = Box(Color.Blue, width: 7);
        var fill = Box(Color.Cyan);

        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(left, Dock.Left);

        var panel = new DockPanel();
        panel.Children.Add(top);
        panel.Children.Add(left);
        panel.Children.Add(fill);

        var bounds = new Rect(3, 5, 37, 19);
        var rects = panel.CalculateChildBounds(bounds);

        // The docked rectangles must tile the panel exactly: total area equals the panel area.
        var totalArea = rects.Sum(r => r.Width * r.Height);
        await Assert.That(totalArea).IsEqualTo(bounds.Width * bounds.Height);

        // ...and nothing may escape the panel.
        foreach (var rect in rects)
        {
            await Assert.That(rect.X).IsGreaterThanOrEqualTo(bounds.X);
            await Assert.That(rect.Y).IsGreaterThanOrEqualTo(bounds.Y);
            await Assert.That(rect.Right).IsLessThanOrEqualTo(bounds.Right);
            await Assert.That(rect.Bottom).IsLessThanOrEqualTo(bounds.Bottom);
        }
    }

    [Test]
    public async Task CalculateChildBounds_LastChildFillFalse_LastChildUsesItsOwnSize()
    {
        var top = Box(Color.Red, height: 2);
        var second = Box(Color.Green, height: 3);

        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(second, Dock.Top);

        var panel = new DockPanel { LastChildFill = false };
        panel.Children.Add(top);
        panel.Children.Add(second);

        var rects = panel.CalculateChildBounds(new Rect(0, 0, 20, 20));

        await Assert.That(rects[0]).IsEqualTo(new Rect(0, 0, 20, 2));
        await Assert.That(rects[1]).IsEqualTo(new Rect(0, 2, 20, 3));
    }

    [Test]
    public async Task CalculateChildBounds_ChildLargerThanRemainingSpace_IsClamped()
    {
        var first = Box(Color.Red, height: 8);
        var second = Box(Color.Green, height: 8);

        DockPanel.SetDock(first, Dock.Top);
        DockPanel.SetDock(second, Dock.Top);

        var panel = new DockPanel { LastChildFill = false };
        panel.Children.Add(first);
        panel.Children.Add(second);

        var rects = panel.CalculateChildBounds(new Rect(0, 0, 10, 10));

        await Assert.That(rects[0]).IsEqualTo(new Rect(0, 0, 10, 8));
        // Only two rows are left, so the second child gets two rather than overflowing.
        await Assert.That(rects[1]).IsEqualTo(new Rect(0, 8, 10, 2));
    }

    [Test]
    public async Task CalculateChildBounds_NoSpaceLeft_ProducesZeroSizeRects()
    {
        var first = Box(Color.Red, height: 10);
        var second = Box(Color.Green, height: 4);
        var third = Box(Color.Blue, height: 4);

        DockPanel.SetDock(first, Dock.Top);
        DockPanel.SetDock(second, Dock.Top);
        DockPanel.SetDock(third, Dock.Top);

        var panel = new DockPanel { LastChildFill = false };
        panel.Children.Add(first);
        panel.Children.Add(second);
        panel.Children.Add(third);

        var rects = panel.CalculateChildBounds(new Rect(0, 0, 10, 10));

        await Assert.That(rects[0]).IsEqualTo(new Rect(0, 0, 10, 10));
        await Assert.That(rects[1].Height).IsEqualTo(0);
        await Assert.That(rects[2].Height).IsEqualTo(0);
    }

    [Test]
    public async Task CalculateChildBounds_ZeroSizedPanel_DoesNotProduceNegativeSizes()
    {
        var child = Box(Color.Red, height: 4);
        DockPanel.SetDock(child, Dock.Top);

        var panel = new DockPanel();
        panel.Children.Add(child);

        var rects = panel.CalculateChildBounds(new Rect(0, 0, 0, 0));

        await Assert.That(rects[0].Width).IsEqualTo(0);
        await Assert.That(rects[0].Height).IsEqualTo(0);
    }

    #endregion

    #region Rendering

    [Test]
    public void Render_NoChildren_DoesNotCrash()
    {
        new DockPanel().Render(_buffer, new Rect(0, 0, 20, 10));
    }

    [Test]
    public async Task Render_TopLeftAndFill_PaintsExpectedCells()
    {
        var header = Box(Color.Red, height: 2);
        var sidebar = Box(Color.Green, width: 6);
        var content = Box(Color.Blue);

        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(sidebar, Dock.Left);

        var panel = new DockPanel();
        panel.Children.Add(header);
        panel.Children.Add(sidebar);
        panel.Children.Add(content);

        panel.Render(_buffer, new Rect(0, 0, 20, 10));

        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(19, 1).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(0, 2).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(5, 9).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(6, 2).Background).IsEqualTo(Color.Blue);
        await Assert.That(_buffer.GetCell(19, 9).Background).IsEqualTo(Color.Blue);
    }

    [Test]
    public async Task Render_BottomAndRight_PaintsFromTheFarEdges()
    {
        var footer = Box(Color.Red, height: 2);
        var aside = Box(Color.Green, width: 4);
        var content = Box(Color.Blue);

        DockPanel.SetDock(footer, Dock.Bottom);
        DockPanel.SetDock(aside, Dock.Right);

        var panel = new DockPanel();
        panel.Children.Add(footer);
        panel.Children.Add(aside);
        panel.Children.Add(content);

        panel.Render(_buffer, new Rect(0, 0, 20, 10));

        await Assert.That(_buffer.GetCell(0, 8).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(19, 9).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(16, 0).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(19, 7).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Blue);
        await Assert.That(_buffer.GetCell(15, 7).Background).IsEqualTo(Color.Blue);
    }

    #endregion

    #region Visibility

    [Test]
    public async Task CalculateChildBounds_CollapsedChild_TakesNoSpaceAndSiblingsCloseTheGap()
    {
        var first = Box(Color.Red, height: 2);
        var collapsed = Box(Color.Green, height: 5);
        var third = Box(Color.Blue, height: 3);

        DockPanel.SetDock(first, Dock.Top);
        DockPanel.SetDock(collapsed, Dock.Top);
        DockPanel.SetDock(third, Dock.Top);
        collapsed.Visibility = Visibility.Collapsed;

        var panel = new DockPanel { LastChildFill = false };
        panel.Children.Add(first);
        panel.Children.Add(collapsed);
        panel.Children.Add(third);

        var rects = panel.CalculateChildBounds(new Rect(0, 0, 20, 20));

        await Assert.That(rects[0]).IsEqualTo(new Rect(0, 0, 20, 2));
        await Assert.That(rects[1].Width).IsEqualTo(0);
        await Assert.That(rects[1].Height).IsEqualTo(0);
        await Assert.That(rects[2]).IsEqualTo(new Rect(0, 2, 20, 3)); // starts where the collapsed child would have
    }

    [Test]
    public async Task CalculateChildBounds_CollapsedLastChild_FillGoesToTheLastVisibleChild()
    {
        var first = Box(Color.Red, height: 2);
        var second = Box(Color.Green, height: 2);
        var collapsed = Box(Color.Blue, height: 2);

        DockPanel.SetDock(first, Dock.Top);
        DockPanel.SetDock(second, Dock.Top);
        DockPanel.SetDock(collapsed, Dock.Top);
        collapsed.Visibility = Visibility.Collapsed;

        var panel = new DockPanel();
        panel.Children.Add(first);
        panel.Children.Add(second);
        panel.Children.Add(collapsed);

        var rects = panel.CalculateChildBounds(new Rect(0, 0, 20, 20));

        await Assert.That(rects[0]).IsEqualTo(new Rect(0, 0, 20, 2));
        await Assert.That(rects[1]).IsEqualTo(new Rect(0, 2, 20, 18)); // fills the remainder
        await Assert.That(rects[2].Height).IsEqualTo(0);
    }

    [Test]
    public async Task CalculateChildBounds_HiddenChild_KeepsItsNormalAllocation()
    {
        var first = Box(Color.Red, height: 2);
        var hidden = Box(Color.Green, height: 5);
        var third = Box(Color.Blue, height: 3);

        DockPanel.SetDock(first, Dock.Top);
        DockPanel.SetDock(hidden, Dock.Top);
        DockPanel.SetDock(third, Dock.Top);
        hidden.Visibility = Visibility.Hidden;

        var panel = new DockPanel { LastChildFill = false };
        panel.Children.Add(first);
        panel.Children.Add(hidden);
        panel.Children.Add(third);

        var rects = panel.CalculateChildBounds(new Rect(0, 0, 20, 20));

        await Assert.That(rects[1]).IsEqualTo(new Rect(0, 2, 20, 5));
        await Assert.That(rects[2]).IsEqualTo(new Rect(0, 7, 20, 3));
    }

    [Test]
    public async Task Render_HiddenChild_LeavesItsCellsAsBackground()
    {
        var first = Box(Color.Red, height: 2);
        var hidden = Box(Color.Green, height: 3);
        var third = Box(Color.Blue, height: 2);

        DockPanel.SetDock(first, Dock.Top);
        DockPanel.SetDock(hidden, Dock.Top);
        DockPanel.SetDock(third, Dock.Top);
        hidden.Visibility = Visibility.Hidden;

        var panel = new DockPanel { LastChildFill = false };
        panel.Children.Add(first);
        panel.Children.Add(hidden);
        panel.Children.Add(third);

        panel.Render(_buffer, new Rect(0, 0, 20, 20));

        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        // The hidden child's rows keep their allocation but paint nothing.
        await Assert.That(_buffer.GetCell(0, 2).Background).IsNotEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(0, 4).Background).IsNotEqualTo(Color.Green);
        // The child after it still starts below the hidden allocation.
        await Assert.That(_buffer.GetCell(0, 5).Background).IsEqualTo(Color.Blue);
    }

    #endregion

    #region GetPreferredSize

    [Test]
    public async Task GetPreferredSize_NoChildren_ReturnsZero()
    {
        await Assert.That(new DockPanel().GetPreferredSize(new Rect(0, 0, 40, 20)))
            .IsEqualTo(new Size2D(0, 0));
    }

    [Test]
    public async Task GetPreferredSize_StackedEdges_AccumulatesAlongEachAxis()
    {
        var top = Box(Color.Red, width: 10, height: 2);
        var bottom = Box(Color.Green, width: 8, height: 3);
        var left = Box(Color.Blue, width: 4, height: 6);

        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(bottom, Dock.Bottom);
        DockPanel.SetDock(left, Dock.Left);

        var panel = new DockPanel();
        panel.Children.Add(top);
        panel.Children.Add(bottom);
        panel.Children.Add(left);

        var preferred = panel.GetPreferredSize(new Rect(0, 0, 40, 20));

        // Top/Bottom accumulate 2+3 rows; Left contributes 4 columns and needs 6 rows on top of
        // the 5 already consumed.
        await Assert.That(preferred).IsEqualTo(new Size2D(10, 11));
    }

    [Test]
    public async Task GetPreferredSize_CollapsedChild_ContributesNothing()
    {
        var top = Box(Color.Red, width: 10, height: 2);
        var collapsed = Box(Color.Green, width: 30, height: 9);
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(collapsed, Dock.Top);
        collapsed.Visibility = Visibility.Collapsed;

        var panel = new DockPanel();
        panel.Children.Add(top);
        panel.Children.Add(collapsed);

        await Assert.That(panel.GetPreferredSize(new Rect(0, 0, 40, 20)))
            .IsEqualTo(new Size2D(10, 2));
    }

    #endregion

    #region GetChildrenWithBounds

    [Test]
    public async Task GetChildrenWithBounds_MatchesArrangement_AndSkipsCollapsed()
    {
        var top = Box(Color.Red, height: 2);
        var collapsed = Box(Color.Green, height: 4);
        var fill = Box(Color.Blue);

        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(collapsed, Dock.Top);
        collapsed.Visibility = Visibility.Collapsed;

        var panel = new DockPanel();
        panel.Children.Add(top);
        panel.Children.Add(collapsed);
        panel.Children.Add(fill);

        var pairs = panel.GetChildrenWithBounds(new Rect(0, 0, 20, 10)).ToList();

        await Assert.That(pairs.Count).IsEqualTo(2);
        await Assert.That(pairs[0].Child).IsEqualTo(top);
        await Assert.That(pairs[0].ChildParentBounds).IsEqualTo(new Rect(0, 0, 20, 2));
        await Assert.That(pairs[1].Child).IsEqualTo(fill);
        await Assert.That(pairs[1].ChildParentBounds).IsEqualTo(new Rect(0, 2, 20, 8));
    }

    [Test]
    public async Task GetChildrenWithBounds_NoChildren_ReturnsEmpty()
    {
        await Assert.That(new DockPanel().GetChildrenWithBounds(new Rect(0, 0, 20, 10)).Any()).IsFalse();
    }

    #endregion
}
