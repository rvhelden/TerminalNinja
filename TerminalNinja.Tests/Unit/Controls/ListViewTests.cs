using System.Collections.ObjectModel;

namespace TerminalNinja.Tests.Unit.Controls;

public class ListViewTests
{
    #region Default Values

    [Test]
    public async Task Focusable_Default_IsTrue()
    {
        var lv = new ListView();
        await Assert.That(lv.Focusable).IsTrue();
    }

    [Test]
    public async Task ShowGridLines_Default_IsTrue()
    {
        var lv = new ListView();
        await Assert.That(lv.ShowGridLines).IsTrue();
    }

    [Test]
    public async Task Columns_Default_IsEmpty()
    {
        var lv = new ListView();
        await Assert.That(lv.Columns.Count).IsEqualTo(0);
    }

    #endregion

    #region Column Width Resolution

    [Test]
    public async Task ResolveColumnWidths_AllFixed_ReturnsFixedWidths()
    {
        var lv = new ListView();
        lv.Columns.Add(new ListViewColumn { Header = "A", Width = 10 });
        lv.Columns.Add(new ListViewColumn { Header = "B", Width = 20 });

        var widths = lv.ResolveColumnWidths(31); // 31 = 10 + 20 + 1 separator

        await Assert.That(widths[0]).IsEqualTo(10);
        await Assert.That(widths[1]).IsEqualTo(20);
    }

    [Test]
    public async Task ResolveColumnWidths_StarSized_DistributesEvenly()
    {
        var lv = new ListView { ShowGridLines = false };
        lv.Columns.Add(new ListViewColumn { Header = "A", Width = 0 });
        lv.Columns.Add(new ListViewColumn { Header = "B", Width = 0 });

        var widths = lv.ResolveColumnWidths(20);

        await Assert.That(widths[0]).IsEqualTo(10);
        await Assert.That(widths[1]).IsEqualTo(10);
    }

    [Test]
    public async Task ResolveColumnWidths_MixedFixedAndStar()
    {
        var lv = new ListView();
        lv.Columns.Add(new ListViewColumn { Header = "A", Width = 10 });
        lv.Columns.Add(new ListViewColumn { Header = "B", Width = 0 }); // star

        var widths = lv.ResolveColumnWidths(21); // 21 = total, 1 separator

        await Assert.That(widths[0]).IsEqualTo(10);
        await Assert.That(widths[1]).IsEqualTo(10); // 21 - 1 sep - 10 fixed = 10
    }

    #endregion

    #region Container Generation

    [Test]
    public async Task ItemsSource_GeneratesListViewItemContainers()
    {
        var lv = new ListView();
        lv.Columns.Add(new ListViewColumn { Header = "Name" });
        lv.ItemsSource = new ObservableCollection<string> { "A", "B", "C" };

        await Assert.That(lv.ItemsPanel.Children.Count).IsEqualTo(3);
        for (var i = 0; i < 3; i++)
        {
            await Assert.That(lv.ItemsPanel.Children[i]).IsTypeOf<ListViewItem>();
        }
    }

    [Test]
    public async Task IsItemItsOwnContainer_ListViewItem_ReturnsTrue()
    {
        var lv = new ListView();
        var lvi = new ListViewItem { Content = "Direct" };
        lv.Items.Add(lvi);

        await Assert.That(lv.ItemsPanel.Children.Count).IsEqualTo(1);
        await Assert.That(lv.ItemsPanel.Children[0]).IsEqualTo(lvi);
    }

    #endregion

    #region Keyboard Navigation

    [Test]
    public async Task DownArrow_SelectsNextRow()
    {
        var lv = CreateListView();
        lv.SelectedIndex = 0;

        lv.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false));

        await Assert.That(lv.SelectedIndex).IsEqualTo(1);
    }

    [Test]
    public async Task UpArrow_SelectsPreviousRow()
    {
        var lv = CreateListView();
        lv.SelectedIndex = 2;

        lv.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(lv.SelectedIndex).IsEqualTo(1);
    }

    [Test]
    public async Task HomeKey_SelectsFirstRow()
    {
        var lv = CreateListView();
        lv.SelectedIndex = 2;

        lv.OnKeyEvent(new KeyEvent(ConsoleKey.Home, '\0', false, false, false));

        await Assert.That(lv.SelectedIndex).IsEqualTo(0);
    }

    [Test]
    public async Task EndKey_SelectsLastRow()
    {
        var lv = CreateListView();
        lv.SelectedIndex = 0;

        lv.OnKeyEvent(new KeyEvent(ConsoleKey.End, '\0', false, false, false));

        await Assert.That(lv.SelectedIndex).IsEqualTo(2);
    }

    [Test]
    public async Task KeyboardNav_EmptyListView_DoesNotThrow()
    {
        var lv = new ListView();
        lv.Columns.Add(new ListViewColumn { Header = "A" });

        lv.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false));

        await Assert.That(lv.SelectedIndex).IsEqualTo(-1);
    }

    #endregion

    #region Rendering

    [Test]
    public async Task Render_HeaderRow_ShowsColumnHeaders()
    {
        var lv = CreateListView();

        using var buffer = new CellBuffer(40, 10);
        lv.Render(buffer, new Rect(0, 0, 40, 10));

        // First column header "Name" starts at x=0
        await Assert.That(buffer.GetCell(0, 0).Character).IsEqualTo('N');
        await Assert.That(buffer.GetCell(3, 0).Character).IsEqualTo('e');
    }

    [Test]
    public async Task Render_SeparatorRow_UsesCorrectCharacters()
    {
        var lv = CreateListView();

        using var buffer = new CellBuffer(40, 10);
        lv.Render(buffer, new Rect(0, 0, 40, 10));

        // Row 1 is separator
        await Assert.That(buffer.GetCell(0, 1).Character).IsEqualTo('─');
    }

    [Test]
    public async Task Render_ColumnSeparators_AreVisible()
    {
        var lv = new ListView { ShowGridLines = true };
        lv.Columns.Add(new ListViewColumn { Header = "A", Width = 5 });
        lv.Columns.Add(new ListViewColumn { Header = "B", Width = 5 });
        lv.ItemsSource = new ObservableCollection<string> { "X" };

        using var buffer = new CellBuffer(20, 5);
        lv.Render(buffer, new Rect(0, 0, 20, 5));

        // Column separator at x=5
        await Assert.That(buffer.GetCell(5, 0).Character).IsEqualTo('│');
    }

    [Test]
    public async Task Render_EmptyListView_ShowsHeadersOnly()
    {
        var lv = new ListView();
        lv.Columns.Add(new ListViewColumn { Header = "Col", Width = 10 });

        using var buffer = new CellBuffer(20, 5);
        lv.Render(buffer, new Rect(0, 0, 20, 5));

        await Assert.That(buffer.GetCell(0, 0).Character).IsEqualTo('C');
    }

    [Test]
    public async Task Render_SelectedRow_UsesSelectedBackground()
    {
        var lv = CreateListView();
        lv.SelectedIndex = 0;
        lv.SelectedBackground = Color.Blue;

        using var buffer = new CellBuffer(40, 10);
        lv.Render(buffer, new Rect(0, 0, 40, 10));

        // Data row 0 is at y=2 (header=0, sep=1)
        await Assert.That(buffer.GetCell(0, 2).Background).IsEqualTo(Color.Blue);
    }

    #endregion

    #region Selection

    [Test]
    public async Task SelectionChanged_Fires()
    {
        var lv = CreateListView();
        var fired = false;
        lv.SelectionChanged += (_, _) => fired = true;

        lv.SelectedIndex = 1;

        await Assert.That(fired).IsTrue();
    }

    #endregion

    #region XAML

    [Test]
    public async Task Xaml_ParsesListViewItems()
    {
        var xaml = """
            <ListView xmlns="http://schemas.terminalninja.dev/xaml">
                <ListViewItem Content="Row 1" />
                <ListViewItem Content="Row 2" />
            </ListView>
            """;

        var lv = TerminalXaml.Load<ListView>(xaml);

        await Assert.That(lv.ItemsPanel.Children.Count).IsEqualTo(2);
    }

    #endregion

    #region Helpers

    private static ListView CreateListView()
    {
        var lv = new ListView();
        lv.Columns.Add(new ListViewColumn { Header = "Name", Width = 15 });
        lv.Columns.Add(new ListViewColumn { Header = "Size", Width = 10 });
        lv.ItemsSource = new ObservableCollection<string> { "File1", "File2", "File3" };
        return lv;
    }

    #endregion
}
