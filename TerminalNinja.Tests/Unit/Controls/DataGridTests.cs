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
