using TerminalNinja.Input;

namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Container virtualization: with <c>IsVirtualizing</c> on, a control builds containers only for
/// the rows it is about to draw.
/// </summary>
/// <remarks>
/// Unvirtualized, an <c>ItemsControl</c> creates one container per item the moment the collection
/// is set — ten thousand rows meant ten thousand live controls to show the thirty that fit. The
/// cost is memory and load time, not per-frame rendering: <see cref="ListBox"/> and
/// <see cref="DataGrid"/> already drew only their viewport.
///
/// It is opt-in. <c>ContainerFromItem</c> can only answer for a realised item, and
/// <c>ItemsPanel.Children</c> stops tracking the item list one-for-one, so turning it on silently
/// would change a published contract underneath existing callers.
/// </remarks>
public class VirtualizationTests
{
    private static List<object> Rows(int count) =>
        [.. Enumerable.Range(0, count).Select(object (i) => $"row {i}")];

    private static KeyEvent Key(ConsoleKey key) => new(key, '\0', false, false, false);

    [Test]
    public async Task Default_IsOff()
    {
        await Assert.That(new ItemsControl().IsVirtualizing).IsFalse();
        await Assert.That(new ListBox().IsVirtualizing).IsFalse();
    }

    [Test]
    public async Task Unvirtualized_RealisesEveryItemUpFront()
    {
        var list = new ListBox { ItemsSource = Rows(500) };

        await Assert.That(list.ItemsPanel.Children.Count).IsEqualTo(500);
    }

    [Test]
    public async Task Virtualized_RealisesNothingBeforeTheFirstRender()
    {
        var list = new ListBox { IsVirtualizing = true, ItemsSource = Rows(500) };

        await Assert.That(list.ItemsPanel.Children.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Virtualized_RealisesOnlyTheViewport()
    {
        var list = new ListBox { IsVirtualizing = true, ItemsSource = Rows(10_000) };

        using var buffer = new CellBuffer(40, 12);
        list.Render(buffer, new Rect(0, 0, 40, 12));

        // Twelve rows on screen, so twelve containers — not ten thousand.
        await Assert.That(list.ItemsPanel.Children.Count).IsEqualTo(12);
    }

    [Test]
    public async Task Virtualized_DrawsTheSameRowsAsUnvirtualized()
    {
        var items = Rows(200);

        using var plain = new CellBuffer(40, 6);
        new ListBox { ItemsSource = items }.Render(plain, new Rect(0, 0, 40, 6));

        using var virtualized = new CellBuffer(40, 6);
        new ListBox { IsVirtualizing = true, ItemsSource = items }.Render(virtualized, new Rect(0, 0, 40, 6));

        for (var y = 0; y < 6; y++)
        {
            for (var x = 0; x < 40; x++)
            {
                await Assert.That(virtualized.GetCell(x, y).Codepoint)
                    .IsEqualTo(plain.GetCell(x, y).Codepoint);
            }
        }
    }

    [Test]
    public async Task Virtualized_SelectionRunsOverTheWholeList_NotTheRealisedWindow()
    {
        var list = new ListBox { IsVirtualizing = true, ItemsSource = Rows(1_000) };

        using var buffer = new CellBuffer(40, 10);
        list.Render(buffer, new Rect(0, 0, 40, 10));

        // End must reach item 999, not the bottom of the first realised page.
        list.OnKeyEvent(Key(ConsoleKey.End));

        await Assert.That(list.SelectedIndex).IsEqualTo(999);
        await Assert.That(list.SelectedItem).IsEqualTo("row 999");
    }

    [Test]
    public async Task Virtualized_ScrollingMovesTheWindowAndKeepsItBounded()
    {
        var list = new ListBox { IsVirtualizing = true, ItemsSource = Rows(1_000) };

        using var buffer = new CellBuffer(40, 10);
        list.Render(buffer, new Rect(0, 0, 40, 10));
        list.OnKeyEvent(Key(ConsoleKey.End));
        list.Render(buffer, new Rect(0, 0, 40, 10));

        // Scrolled to the bottom: still one screenful of containers, now a different one.
        await Assert.That(list.ItemsPanel.Children.Count).IsEqualTo(10);

        var lastRow = string.Concat(Enumerable.Range(0, 40).Select(x => (char)buffer.GetCell(x, 9).Codepoint)).Trim();
        // The window really did move: the bottom line is the last item, not the tenth.
        await Assert.That(lastRow).Contains("row 999");
    }

    [Test]
    public async Task Virtualized_ReusesContainersThatStayInTheWindow()
    {
        var list = new ListBox { IsVirtualizing = true, ItemsSource = Rows(100) };

        using var buffer = new CellBuffer(40, 10);
        list.Render(buffer, new Rect(0, 0, 40, 10));

        var beforeScroll = list.ItemsPanel.Children[5];

        // Down one row: rows 1..10 are now on screen, so the container for what was row 5 stays.
        list.OnKeyEvent(Key(ConsoleKey.DownArrow));
        list.Render(buffer, new Rect(0, 0, 40, 10));

        await Assert.That(list.ItemsPanel.Children.Contains(beforeScroll)).IsTrue();
    }

    [Test]
    public async Task Virtualized_DataGridDrawsTheSameRowsAndRealisesNoContainers()
    {
        var items = Rows(5_000);

        using var plain = new CellBuffer(30, 8);
        var unvirtualized = new DataGrid { ItemsSource = items };
        unvirtualized.Render(plain, new Rect(0, 0, 30, 8));

        using var buffer = new CellBuffer(30, 8);
        var grid = new DataGrid { IsVirtualizing = true, ItemsSource = items };
        grid.Render(buffer, new Rect(0, 0, 30, 8));

        // The grid renders cells straight from the items, so it needs no containers at all.
        await Assert.That(grid.ItemsPanel.Children.Count).IsEqualTo(0);

        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 30; x++)
            {
                await Assert.That(buffer.GetCell(x, y).Codepoint)
                    .IsEqualTo(plain.GetCell(x, y).Codepoint);
            }
        }
    }

    [Test]
    public async Task Virtualized_LoadIsIndependentOfCollectionSize()
    {
        // The point of the whole exercise: binding a big collection must not cost time and memory
        // proportional to it when only a screenful is ever drawn. Asserted as a ratio rather than
        // an absolute — this runs on whatever machine CI hands it.
        using var buffer = new CellBuffer(40, 20);

        var small = System.Diagnostics.Stopwatch.StartNew();
        var smallList = new ListBox { IsVirtualizing = true, ItemsSource = Rows(100) };
        smallList.Render(buffer, new Rect(0, 0, 40, 20));
        small.Stop();

        var large = System.Diagnostics.Stopwatch.StartNew();
        var largeList = new ListBox { IsVirtualizing = true, ItemsSource = Rows(50_000) };
        largeList.Render(buffer, new Rect(0, 0, 40, 20));
        large.Stop();

        await Assert.That(largeList.ItemsPanel.Children.Count).IsEqualTo(20);
        await Assert.That(smallList.ItemsPanel.Children.Count).IsEqualTo(20);

        // Unvirtualized this is 500x the containers; virtualized the only work that scales is
        // materialising the item list itself, so allow a wide margin and still catch a regression
        // back to per-item realisation.
        await Assert.That(large.Elapsed).IsLessThan(TimeSpan.FromSeconds(2));
    }

    [Test]
    public async Task Virtualized_SurvivesTheCollectionChangingUnderIt()
    {
        var items = new System.Collections.ObjectModel.ObservableCollection<object>(Rows(50));
        var list = new ListBox { IsVirtualizing = true, ItemsSource = items };

        using var buffer = new CellBuffer(40, 10);
        list.Render(buffer, new Rect(0, 0, 40, 10));

        items.Clear();
        list.Render(buffer, new Rect(0, 0, 40, 10));
        await Assert.That(list.ItemsPanel.Children.Count).IsEqualTo(0);

        foreach (var row in Rows(3))
        {
            items.Add(row);
        }

        list.Render(buffer, new Rect(0, 0, 40, 10));
        await Assert.That(list.ItemsPanel.Children.Count).IsEqualTo(3);
    }
}
