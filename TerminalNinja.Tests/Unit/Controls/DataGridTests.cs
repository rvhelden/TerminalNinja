using System.Collections.ObjectModel;

namespace TerminalNinja.Tests.Unit.Controls;

public class DataGridTests
{
    #region Default Values

    [Test]
    public async Task Focusable_Default_IsTrue()
    {
        var dg = new DataGrid();
        await Assert.That(dg.Focusable).IsTrue();
    }

    [Test]
    public async Task CanSort_Default_IsTrue()
    {
        var dg = new DataGrid();
        await Assert.That(dg.CanSort).IsTrue();
    }

    [Test]
    public async Task ShowGridLines_Default_IsTrue()
    {
        var dg = new DataGrid();
        await Assert.That(dg.ShowGridLines).IsTrue();
    }

    #endregion

    #region Sorting

    [Test]
    public async Task SortByColumn_CyclesDirection()
    {
        var dg = new DataGrid();
        dg.Columns.Add(new DataGridTextColumn { Header = "Name" });

        dg.SortByColumn(0);
        await Assert.That(dg.Columns[0].SortDirection).IsEqualTo(SortDirection.Ascending);

        dg.SortByColumn(0);
        await Assert.That(dg.Columns[0].SortDirection).IsEqualTo(SortDirection.Descending);

        dg.SortByColumn(0);
        await Assert.That(dg.Columns[0].SortDirection).IsEqualTo(SortDirection.None);
    }

    [Test]
    public async Task SortByColumn_ClearsOtherColumns()
    {
        var dg = new DataGrid();
        dg.Columns.Add(new DataGridTextColumn { Header = "A" });
        dg.Columns.Add(new DataGridTextColumn { Header = "B" });

        dg.SortByColumn(0);
        await Assert.That(dg.Columns[0].SortDirection).IsEqualTo(SortDirection.Ascending);

        dg.SortByColumn(1);
        await Assert.That(dg.Columns[0].SortDirection).IsEqualTo(SortDirection.None);
        await Assert.That(dg.Columns[1].SortDirection).IsEqualTo(SortDirection.Ascending);
    }

    [Test]
    public async Task SortByColumn_NotSortable_DoesNothing()
    {
        var dg = new DataGrid();
        dg.Columns.Add(new DataGridTextColumn { Header = "A", CanUserSort = false });

        dg.SortByColumn(0);
        await Assert.That(dg.Columns[0].SortDirection).IsEqualTo(SortDirection.None);
    }

    [Test]
    public async Task SortingChanged_Fires()
    {
        var dg = new DataGrid();
        dg.Columns.Add(new DataGridTextColumn { Header = "A" });
        var fired = false;
        dg.SortingChanged += (_, _) => fired = true;

        dg.SortByColumn(0);
        await Assert.That(fired).IsTrue();
    }

    #endregion

    #region Rendering

    [Test]
    public async Task Render_ShowsSortIndicator()
    {
        var dg = new DataGrid();
        dg.Columns.Add(new DataGridTextColumn { Header = "Name", Width = 15 });
        dg.Columns[0].SortDirection = SortDirection.Ascending;
        dg.ItemsSource = new ObservableCollection<string> { "A" };

        using var buffer = new CellBuffer(30, 5);
        dg.Render(buffer, new Rect(0, 0, 30, 5));

        // Header should contain ▲ indicator
        var hasIndicator = false;
        for (var x = 0; x < 30; x++)
            if (buffer.GetCell(x, 0).Codepoint == '\u25B2') hasIndicator = true;
        await Assert.That(hasIndicator).IsTrue();
    }

    [Test]
    public async Task Render_ShowsHeaders()
    {
        var dg = new DataGrid();
        dg.Columns.Add(new DataGridTextColumn { Header = "Name", Width = 10 });
        dg.Columns.Add(new DataGridTextColumn { Header = "Value", Width = 10 });

        using var buffer = new CellBuffer(25, 5);
        dg.Render(buffer, new Rect(0, 0, 25, 5));

        await Assert.That(buffer.GetCell(0, 0).Codepoint).IsEqualTo('N');
    }

    [Test]
    public async Task Render_SelectedRow_Highlighted()
    {
        var dg = new DataGrid { SelectedBackground = Color.Blue };
        dg.Columns.Add(new DataGridTextColumn { Header = "X", Width = 10 });
        dg.ItemsSource = new ObservableCollection<string> { "A", "B" };
        dg.SelectedIndex = 0;

        using var buffer = new CellBuffer(15, 5);
        dg.Render(buffer, new Rect(0, 0, 15, 5));

        await Assert.That(buffer.GetCell(0, 2).Background).IsEqualTo(Color.Blue);
    }

    #endregion

    #region Scrolling

    [Test]
    public async Task Render_SelectionBelowViewport_ScrollsIntoView()
    {
        var dg = new DataGrid();
        dg.Columns.Add(new DataGridTextColumn { Header = "X", Width = 10 });
        dg.ItemsSource = new ObservableCollection<string> { "A", "B", "C", "D", "E" };
        dg.SelectedIndex = 4;

        // Height 4 = 2 header rows + 2 visible data rows.
        using var buffer = new CellBuffer(15, 4);
        dg.Render(buffer, new Rect(0, 0, 15, 4));

        // The viewport shows the last two items, with the selection on the bottom row.
        await Assert.That(buffer.GetCell(0, 2).Codepoint).IsEqualTo('D');
        await Assert.That(buffer.GetCell(0, 3).Codepoint).IsEqualTo('E');
    }

    [Test]
    public async Task Render_SelectionBackAboveViewport_ScrollsBackUp()
    {
        var dg = new DataGrid();
        dg.Columns.Add(new DataGridTextColumn { Header = "X", Width = 10 });
        dg.ItemsSource = new ObservableCollection<string> { "A", "B", "C", "D", "E" };
        dg.SelectedIndex = 4;

        using var buffer = new CellBuffer(15, 4);
        dg.Render(buffer, new Rect(0, 0, 15, 4));

        dg.SelectedIndex = 0;
        dg.Render(buffer, new Rect(0, 0, 15, 4));

        await Assert.That(buffer.GetCell(0, 2).Codepoint).IsEqualTo('A');
        await Assert.That(buffer.GetCell(0, 3).Codepoint).IsEqualTo('B');
    }

    [Test]
    public async Task Render_Scrolled_HeaderStaysFixed()
    {
        var dg = new DataGrid();
        dg.Columns.Add(new DataGridTextColumn { Header = "Name", Width = 10 });
        dg.ItemsSource = new ObservableCollection<string> { "A", "B", "C", "D", "E" };
        dg.SelectedIndex = 4;

        using var buffer = new CellBuffer(15, 4);
        dg.Render(buffer, new Rect(0, 0, 15, 4));

        await Assert.That(buffer.GetCell(0, 0).Codepoint).IsEqualTo('N');
    }

    #endregion

    #region Keyboard

    [Test]
    public async Task DownArrow_SelectsNext()
    {
        var dg = new DataGrid();
        dg.Columns.Add(new DataGridTextColumn { Header = "X" });
        dg.ItemsSource = new ObservableCollection<string> { "A", "B" };
        dg.SelectedIndex = 0;

        dg.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false));
        await Assert.That(dg.SelectedIndex).IsEqualTo(1);
    }

    /// <summary>
    /// A grid of <paramref name="itemCount"/> single-column rows, rendered once at
    /// <paramref name="height"/> so that it knows its viewport. Height includes the two header
    /// rows, so the page size is <c>height - 2</c>.
    /// </summary>
    private static DataGrid RenderedGrid(int itemCount, int height)
    {
        var dg = new DataGrid();
        dg.Columns.Add(new DataGridTextColumn { Header = "X", Width = 10 });
        dg.ItemsSource = new ObservableCollection<string>(
            Enumerable.Range(0, itemCount).Select(i => i.ToString()));
        dg.SelectedIndex = 0;

        using var buffer = new CellBuffer(15, height);
        dg.Render(buffer, new Rect(0, 0, 15, height));

        return dg;
    }

    private static void Press(DataGrid dg, ConsoleKey key) =>
        dg.OnKeyEvent(new KeyEvent(key, (char)0, false, false, false));

    [Test]
    public async Task PageDown_MovesByOneViewportOfRows()
    {
        // Height 12 = 2 header rows + 10 visible data rows, so a page is 10.
        var dg = RenderedGrid(itemCount: 100, height: 12);

        Press(dg, ConsoleKey.PageDown);
        await Assert.That(dg.SelectedIndex).IsEqualTo(10);

        Press(dg, ConsoleKey.PageDown);
        await Assert.That(dg.SelectedIndex).IsEqualTo(20);
    }

    [Test]
    public async Task PageUp_MovesBackByOneViewportOfRows()
    {
        var dg = RenderedGrid(itemCount: 100, height: 12);
        dg.SelectedIndex = 45;

        Press(dg, ConsoleKey.PageUp);
        await Assert.That(dg.SelectedIndex).IsEqualTo(35);
    }

    [Test]
    public async Task PageSize_FollowsTheRenderedHeight()
    {
        // The point of the feature: 30 visible rows page by 30, not by a fixed constant.
        var dg = RenderedGrid(itemCount: 100, height: 32);

        Press(dg, ConsoleKey.PageDown);
        await Assert.That(dg.SelectedIndex).IsEqualTo(30);
    }

    [Test]
    public async Task PageDown_StopsAtTheLastRow()
    {
        var dg = RenderedGrid(itemCount: 15, height: 12);

        Press(dg, ConsoleKey.PageDown);
        Press(dg, ConsoleKey.PageDown);
        Press(dg, ConsoleKey.PageDown);

        await Assert.That(dg.SelectedIndex).IsEqualTo(14);
    }

    [Test]
    public async Task PageUp_StopsAtTheFirstRow()
    {
        var dg = RenderedGrid(itemCount: 100, height: 12);
        dg.SelectedIndex = 5;

        Press(dg, ConsoleKey.PageUp);

        await Assert.That(dg.SelectedIndex).IsEqualTo(0);
    }

    [Test]
    public async Task PageDown_BeforeFirstRender_MovesOneRow()
    {
        // No render means no known viewport. Moving an arbitrary distance would be worse than
        // moving the smallest honest one.
        var dg = new DataGrid();
        dg.Columns.Add(new DataGridTextColumn { Header = "X" });
        dg.ItemsSource = new ObservableCollection<string> { "A", "B", "C" };
        dg.SelectedIndex = 0;

        Press(dg, ConsoleKey.PageDown);

        await Assert.That(dg.SelectedIndex).IsEqualTo(1);
    }

    [Test]
    public async Task PageDown_CarriesTheViewportWithTheSelection()
    {
        // Height 5 = 2 header rows + 3 visible data rows.
        var dg = RenderedGrid(itemCount: 20, height: 5);

        Press(dg, ConsoleKey.PageDown);

        using var buffer = new CellBuffer(15, 5);
        dg.Render(buffer, new Rect(0, 0, 15, 5));

        // Selection moved 0 -> 3, and the page scrolled so rows 3..5 are the ones on screen.
        await Assert.That(dg.SelectedIndex).IsEqualTo(3);
        await Assert.That(buffer.GetCell(0, 2).Codepoint).IsEqualTo('3');
        await Assert.That(buffer.GetCell(0, 3).Codepoint).IsEqualTo('4');
        await Assert.That(buffer.GetCell(0, 4).Codepoint).IsEqualTo('5');
    }

    [Test]
    public async Task Home_SelectsTheFirstRowAndScrollsToTheTop()
    {
        var dg = RenderedGrid(itemCount: 20, height: 5);
        dg.SelectedIndex = 19;

        using var scrolled = new CellBuffer(15, 5);
        dg.Render(scrolled, new Rect(0, 0, 15, 5));

        Press(dg, ConsoleKey.Home);

        using var buffer = new CellBuffer(15, 5);
        dg.Render(buffer, new Rect(0, 0, 15, 5));

        await Assert.That(dg.SelectedIndex).IsEqualTo(0);
        await Assert.That(buffer.GetCell(0, 2).Codepoint).IsEqualTo('0');
    }

    [Test]
    public async Task End_SelectsTheLastRowAndScrollsToTheBottom()
    {
        var dg = RenderedGrid(itemCount: 20, height: 5);

        Press(dg, ConsoleKey.End);

        using var buffer = new CellBuffer(15, 5);
        dg.Render(buffer, new Rect(0, 0, 15, 5));

        await Assert.That(dg.SelectedIndex).IsEqualTo(19);

        // The last page fills: rows 17, 18, 19, with the selection on the bottom row.
        await Assert.That(buffer.GetCell(0, 2).Codepoint).IsEqualTo('1');
        await Assert.That(buffer.GetCell(1, 2).Codepoint).IsEqualTo('7');
        await Assert.That(buffer.GetCell(1, 4).Codepoint).IsEqualTo('9');
    }

    #endregion

    #region XAML

    [Test]
    public async Task Xaml_ParsesDataGrid()
    {
        var xaml = """
            <DataGrid xmlns="http://schemas.terminalninja.dev/xaml"
                      ShowGridLines="False" CanSort="False" />
            """;
        var dg = TerminalXaml.Load<DataGrid>(xaml);

        await Assert.That(dg.ShowGridLines).IsFalse();
        await Assert.That(dg.CanSort).IsFalse();
    }

    #endregion
}
