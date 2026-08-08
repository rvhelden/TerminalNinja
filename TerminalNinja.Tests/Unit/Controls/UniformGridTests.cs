namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Tests for the UniformGrid layout container covering:
/// - Rows/Columns, and deriving either (or both) from the child count
/// - Exact integer distribution: cell sizes must sum back to the panel size
/// - Collapsed (occupies no cell) versus Hidden (occupies its cell, paints nothing)
/// - Edge cases (no children, more children than cells, fewer cells than columns)
/// </summary>
public class UniformGridTests
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

    private static UniformGrid GridWith(int count, int rows = 0, int columns = 0)
    {
        var grid = new UniformGrid { Rows = rows, Columns = columns };
        for (var i = 0; i < count; i++)
        {
            grid.Children.Add(new global::TerminalNinja.Controls.Border());
        }

        return grid;
    }

    #region Properties

    [Test]
    public async Task Rows_DefaultValue_IsZero()
    {
        await Assert.That(new UniformGrid().Rows).IsEqualTo(0);
    }

    [Test]
    public async Task Columns_DefaultValue_IsZero()
    {
        await Assert.That(new UniformGrid().Columns).IsEqualTo(0);
    }

    [Test]
    public async Task Rows_NegativeValue_ClampsToZero()
    {
        var grid = new UniformGrid { Rows = -3 };

        await Assert.That(grid.Rows).IsEqualTo(0);
    }

    [Test]
    public async Task Columns_NegativeValue_ClampsToZero()
    {
        var grid = new UniformGrid { Columns = -3 };

        await Assert.That(grid.Columns).IsEqualTo(0);
    }

    #endregion

    #region Shape derivation

    [Test]
    public async Task ResolveShape_BothZero_DerivesNearSquare()
    {
        await Assert.That(GridWith(0).ResolveShape(1)).IsEqualTo((1, 1));
        await Assert.That(GridWith(0).ResolveShape(4)).IsEqualTo((2, 2));
        await Assert.That(GridWith(0).ResolveShape(5)).IsEqualTo((2, 3));
        await Assert.That(GridWith(0).ResolveShape(9)).IsEqualTo((3, 3));
        await Assert.That(GridWith(0).ResolveShape(10)).IsEqualTo((3, 4));
    }

    [Test]
    public async Task ResolveShape_ColumnsOnly_DerivesRows()
    {
        await Assert.That(GridWith(0, columns: 3).ResolveShape(7)).IsEqualTo((3, 3));
        await Assert.That(GridWith(0, columns: 4).ResolveShape(4)).IsEqualTo((1, 4));
    }

    [Test]
    public async Task ResolveShape_RowsOnly_DerivesColumns()
    {
        await Assert.That(GridWith(0, rows: 2).ResolveShape(7)).IsEqualTo((2, 4));
        await Assert.That(GridWith(0, rows: 3).ResolveShape(3)).IsEqualTo((3, 1));
    }

    [Test]
    public async Task ResolveShape_BothSet_UsesThemVerbatim()
    {
        await Assert.That(GridWith(0, rows: 2, columns: 5).ResolveShape(3)).IsEqualTo((2, 5));
    }

    #endregion

    #region Distribution exactness

    [Test]
    public async Task CalculateChildBounds_WidthNotDivisibleByColumns_SpendsEveryCell()
    {
        var grid = GridWith(3, rows: 1, columns: 3);

        var bounds = new Rect(0, 0, 10, 4);
        var rects = grid.CalculateChildBounds(bounds);

        // 10 across 3 columns is 4/3/3 — the remainder goes to the leading columns, never lost.
        await Assert.That(rects[0]).IsEqualTo(new Rect(0, 0, 4, 4));
        await Assert.That(rects[1]).IsEqualTo(new Rect(4, 0, 3, 4));
        await Assert.That(rects[2]).IsEqualTo(new Rect(7, 0, 3, 4));
        await Assert.That(rects.Sum(r => r.Width)).IsEqualTo(bounds.Width);
    }

    [Test]
    public async Task CalculateChildBounds_HeightNotDivisibleByRows_SpendsEveryCell()
    {
        var grid = GridWith(3, rows: 3, columns: 1);

        var bounds = new Rect(0, 0, 6, 11);
        var rects = grid.CalculateChildBounds(bounds);

        await Assert.That(rects[0]).IsEqualTo(new Rect(0, 0, 6, 4));
        await Assert.That(rects[1]).IsEqualTo(new Rect(0, 4, 6, 4));
        await Assert.That(rects[2]).IsEqualTo(new Rect(0, 8, 6, 3));
        await Assert.That(rects.Sum(r => r.Height)).IsEqualTo(bounds.Height);
    }

    [Test]
    public async Task CalculateChildBounds_TwoByTwo_TilesTheWholePanelExactly()
    {
        var grid = GridWith(4, rows: 2, columns: 2);

        var bounds = new Rect(2, 3, 15, 9);
        var rects = grid.CalculateChildBounds(bounds);

        await Assert.That(rects.Sum(r => r.Width * r.Height)).IsEqualTo(bounds.Width * bounds.Height);

        // Adjacent cells must abut with no gap and no overlap.
        await Assert.That(rects[0].Right).IsEqualTo(rects[1].X);
        await Assert.That(rects[2].Right).IsEqualTo(rects[3].X);
        await Assert.That(rects[0].Bottom).IsEqualTo(rects[2].Y);
        await Assert.That(rects[1].Bottom).IsEqualTo(rects[3].Y);
        await Assert.That(rects[1].Right).IsEqualTo(bounds.Right);
        await Assert.That(rects[3].Bottom).IsEqualTo(bounds.Bottom);
    }

    [Test]
    public async Task CalculateChildBounds_MoreColumnsThanCells_StillRendersWhatFits()
    {
        var grid = GridWith(5, rows: 1, columns: 5);

        var rects = grid.CalculateChildBounds(new Rect(0, 0, 3, 2));

        // Three cells across five columns: the first three get one each, the rest get zero.
        // The panel must not round everything to zero and render nothing.
        await Assert.That(rects[0].Width).IsEqualTo(1);
        await Assert.That(rects[1].Width).IsEqualTo(1);
        await Assert.That(rects[2].Width).IsEqualTo(1);
        await Assert.That(rects[3].Width).IsEqualTo(0);
        await Assert.That(rects[4].Width).IsEqualTo(0);
        await Assert.That(rects.Sum(r => r.Width)).IsEqualTo(3);
    }

    [Test]
    public async Task CalculateChildBounds_MoreChildrenThanCells_DropsTheOverflow()
    {
        var grid = GridWith(5, rows: 1, columns: 2);

        var rects = grid.CalculateChildBounds(new Rect(0, 0, 10, 4));

        await Assert.That(rects[0]).IsEqualTo(new Rect(0, 0, 5, 4));
        await Assert.That(rects[1]).IsEqualTo(new Rect(5, 0, 5, 4));
        await Assert.That(rects[2].Width).IsEqualTo(0);
        await Assert.That(rects[3].Width).IsEqualTo(0);
        await Assert.That(rects[4].Width).IsEqualTo(0);
    }

    [Test]
    public async Task CalculateChildBounds_NoChildren_ReturnsEmpty()
    {
        var rects = new UniformGrid().CalculateChildBounds(new Rect(0, 0, 10, 10));

        await Assert.That(rects.Length).IsEqualTo(0);
    }

    [Test]
    public async Task CalculateChildBounds_ZeroSizedPanel_ProducesZeroSizeCells()
    {
        var grid = GridWith(4, rows: 2, columns: 2);

        var rects = grid.CalculateChildBounds(new Rect(0, 0, 0, 0));

        foreach (var rect in rects)
        {
            await Assert.That(rect.Width).IsEqualTo(0);
            await Assert.That(rect.Height).IsEqualTo(0);
        }
    }

    #endregion

    #region Rendering

    [Test]
    public void Render_NoChildren_DoesNotCrash()
    {
        new UniformGrid().Render(_buffer, new Rect(0, 0, 20, 10));
    }

    [Test]
    public async Task Render_TwoByTwo_PaintsEachQuadrant()
    {
        var grid = new UniformGrid { Rows = 2, Columns = 2 };
        grid.Children.Add(Box(Color.Red));
        grid.Children.Add(Box(Color.Green));
        grid.Children.Add(Box(Color.Blue));
        grid.Children.Add(Box(Color.Yellow));

        grid.Render(_buffer, new Rect(0, 0, 20, 10));

        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(9, 4).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(10, 0).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(19, 4).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(0, 5).Background).IsEqualTo(Color.Blue);
        await Assert.That(_buffer.GetCell(9, 9).Background).IsEqualTo(Color.Blue);
        await Assert.That(_buffer.GetCell(10, 5).Background).IsEqualTo(Color.Yellow);
        await Assert.That(_buffer.GetCell(19, 9).Background).IsEqualTo(Color.Yellow);
    }

    [Test]
    public async Task Render_AutoShape_ThreeChildren_FillsRowByRow()
    {
        var grid = new UniformGrid();
        grid.Children.Add(Box(Color.Red));
        grid.Children.Add(Box(Color.Green));
        grid.Children.Add(Box(Color.Blue));

        // Three children auto-derive to 2 columns x 2 rows; the third starts the second row.
        grid.Render(_buffer, new Rect(0, 0, 20, 10));

        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(10, 0).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(0, 5).Background).IsEqualTo(Color.Blue);
        await Assert.That(_buffer.GetCell(10, 5).Background).IsNotEqualTo(Color.Blue);
    }

    #endregion

    #region Visibility

    [Test]
    public async Task CalculateChildBounds_CollapsedChild_OccupiesNoCell()
    {
        var first = Box(Color.Red);
        var collapsed = Box(Color.Green);
        var third = Box(Color.Blue);
        collapsed.Visibility = Visibility.Collapsed;

        var grid = new UniformGrid { Rows = 1, Columns = 2 };
        grid.Children.Add(first);
        grid.Children.Add(collapsed);
        grid.Children.Add(third);

        var rects = grid.CalculateChildBounds(new Rect(0, 0, 10, 4));

        await Assert.That(rects[0]).IsEqualTo(new Rect(0, 0, 5, 4));
        await Assert.That(rects[1].Width).IsEqualTo(0);
        // The third child takes the cell the collapsed one would have used.
        await Assert.That(rects[2]).IsEqualTo(new Rect(5, 0, 5, 4));
    }

    [Test]
    public async Task CalculateChildBounds_CollapsedChild_ExcludedFromDerivedShape()
    {
        var grid = new UniformGrid();
        grid.Children.Add(Box(Color.Red));
        grid.Children.Add(Box(Color.Green));
        var collapsed = Box(Color.Blue);
        collapsed.Visibility = Visibility.Collapsed;
        grid.Children.Add(collapsed);
        var fourth = Box(Color.Yellow);
        grid.Children.Add(fourth);

        // Three visible children derive 2 columns x 2 rows, not the 2x2 that four would.
        var rects = grid.CalculateChildBounds(new Rect(0, 0, 10, 10));

        await Assert.That(rects[0]).IsEqualTo(new Rect(0, 0, 5, 5));
        await Assert.That(rects[1]).IsEqualTo(new Rect(5, 0, 5, 5));
        await Assert.That(rects[2].Width).IsEqualTo(0);
        await Assert.That(rects[3]).IsEqualTo(new Rect(0, 5, 5, 5));
    }

    [Test]
    public async Task CalculateChildBounds_HiddenChild_KeepsItsCell()
    {
        var first = Box(Color.Red);
        var hidden = Box(Color.Green);
        var third = Box(Color.Blue);
        hidden.Visibility = Visibility.Hidden;

        var grid = new UniformGrid { Rows = 1, Columns = 3 };
        grid.Children.Add(first);
        grid.Children.Add(hidden);
        grid.Children.Add(third);

        var rects = grid.CalculateChildBounds(new Rect(0, 0, 9, 4));

        await Assert.That(rects[0]).IsEqualTo(new Rect(0, 0, 3, 4));
        await Assert.That(rects[1]).IsEqualTo(new Rect(3, 0, 3, 4));
        await Assert.That(rects[2]).IsEqualTo(new Rect(6, 0, 3, 4));
    }

    [Test]
    public async Task Render_HiddenChild_LeavesItsCellAsBackground()
    {
        var first = Box(Color.Red);
        var hidden = Box(Color.Green);
        var third = Box(Color.Blue);
        hidden.Visibility = Visibility.Hidden;

        var grid = new UniformGrid { Rows = 1, Columns = 3 };
        grid.Children.Add(first);
        grid.Children.Add(hidden);
        grid.Children.Add(third);

        grid.Render(_buffer, new Rect(0, 0, 9, 4));

        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(4, 0).Background).IsNotEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(7, 0).Background).IsEqualTo(Color.Blue);
    }

    #endregion

    #region GetPreferredSize

    [Test]
    public async Task GetPreferredSize_NoChildren_ReturnsZero()
    {
        await Assert.That(new UniformGrid().GetPreferredSize(new Rect(0, 0, 40, 20)))
            .IsEqualTo(new Size2D(0, 0));
    }

    [Test]
    public async Task GetPreferredSize_UsesLargestChildTimesShape()
    {
        var grid = new UniformGrid { Rows = 2, Columns = 3 };
        grid.Children.Add(Box(Color.Red, 4, 2));
        grid.Children.Add(Box(Color.Green, 7, 1));
        grid.Children.Add(Box(Color.Blue, 3, 5));

        await Assert.That(grid.GetPreferredSize(new Rect(0, 0, 40, 20)))
            .IsEqualTo(new Size2D(21, 10));
    }

    [Test]
    public async Task GetPreferredSize_AllChildrenCollapsed_ReturnsZero()
    {
        var grid = new UniformGrid();
        var child = Box(Color.Red, 10, 4);
        child.Visibility = Visibility.Collapsed;
        grid.Children.Add(child);

        await Assert.That(grid.GetPreferredSize(new Rect(0, 0, 40, 20)))
            .IsEqualTo(new Size2D(0, 0));
    }

    #endregion

    #region GetChildrenWithBounds

    [Test]
    public async Task GetChildrenWithBounds_MatchesArrangement_AndSkipsCollapsed()
    {
        var first = Box(Color.Red);
        var collapsed = Box(Color.Green);
        var third = Box(Color.Blue);
        collapsed.Visibility = Visibility.Collapsed;

        var grid = new UniformGrid { Rows = 1, Columns = 2 };
        grid.Children.Add(first);
        grid.Children.Add(collapsed);
        grid.Children.Add(third);

        var pairs = grid.GetChildrenWithBounds(new Rect(0, 0, 10, 4)).ToList();

        await Assert.That(pairs.Count).IsEqualTo(2);
        await Assert.That(pairs[0].Child).IsEqualTo(first);
        await Assert.That(pairs[0].ChildParentBounds).IsEqualTo(new Rect(0, 0, 5, 4));
        await Assert.That(pairs[1].Child).IsEqualTo(third);
        await Assert.That(pairs[1].ChildParentBounds).IsEqualTo(new Rect(5, 0, 5, 4));
    }

    [Test]
    public async Task GetChildrenWithBounds_NoChildren_ReturnsEmpty()
    {
        await Assert.That(new UniformGrid().GetChildrenWithBounds(new Rect(0, 0, 20, 10)).Any()).IsFalse();
    }

    #endregion
}
