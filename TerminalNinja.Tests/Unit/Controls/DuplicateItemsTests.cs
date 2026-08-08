using System.Collections.ObjectModel;
using TerminalNinja.Rendering;

namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// A collection is allowed to contain equal items — two identical strings, two equal records,
/// a repeated spacer row — and each of them is still its own row.
/// <para>
/// The containers used to be held in a <c>Dictionary&lt;object, UIElement&gt;</c> keyed by the
/// item, which cannot represent that. Virtualized, four equal items realised <i>one</i> container
/// that was then added to the panel four times — the row count looked right, but all four rows
/// were the same control, sharing one <c>IsSelected</c> and one parent. Unvirtualized each row did
/// get its own container, but the map kept only the last of them, so selection and
/// <c>ContainerFromItem</c> answered for the wrong row. Containers are keyed by index now, and
/// these tests pin the difference.
/// </para>
/// </summary>
public class DuplicateItemsTests
{
    private record Person(string Name, int Age);

    private static string[] Lines(UIElement root, int width, int height) =>
        FrameCapture.ToText(root, width, height).Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

    // ─── Unvirtualized: the rows exist, in order ─────────────────────

    [Test]
    public async Task Unvirtualized_EqualStrings_RenderOneRowEachInOrder()
    {
        var items = new ObservableCollection<string> { "alpha", "dup", "dup", "beta", "dup" };
        var control = new ItemsControl { ItemsSource = items };

        var lines = Lines(control, 20, 5);

        await Assert.That(control.ItemsPanel.Children.Count).IsEqualTo(5);
        for (var i = 0; i < items.Count; i++)
        {
            await Assert.That(lines[i]).IsEqualTo(items[i]);
        }
    }

    [Test]
    public async Task Unvirtualized_RepeatedSpacerRows_KeepTheirPositions()
    {
        // The shape that made this bite in practice: {Binding} with no path renders a bare string
        // collection directly, and real lists are full of repeated " " spacers.
        var items = new ObservableCollection<string> { "one", " ", "two", " ", "three" };
        var control = new ItemsControl { ItemsSource = items };

        var lines = Lines(control, 20, 5);

        await Assert.That(control.ItemsPanel.Children.Count).IsEqualTo(5);
        await Assert.That(lines[0]).IsEqualTo("one");
        await Assert.That(lines[1]).IsEqualTo("");   // the spacer, trailing blanks trimmed
        await Assert.That(lines[2]).IsEqualTo("two");
        await Assert.That(lines[3]).IsEqualTo("");
        await Assert.That(lines[4]).IsEqualTo("three");
    }

    [Test]
    public async Task Unvirtualized_EqualRecords_EachGetTheirOwnContainer()
    {
        // Value equality, reference inequality: two distinct instances that compare equal.
        var items = new ObservableCollection<Person>
        {
            new("Ada", 36),
            new("Ada", 36),
            new("Grace", 45),
        };
        var control = new ItemsControl { ItemsSource = items };

        await Assert.That(control.ItemsPanel.Children.Count).IsEqualTo(3);
        await Assert.That(control.ContainerFromIndex(0)).IsNotSameReferenceAs(control.ContainerFromIndex(1));
    }

    [Test]
    public async Task Unvirtualized_EqualBoxedValueTypes_EachGetTheirOwnRow()
    {
        // Boxed ints: equal by value, and the same boxes are not even the same reference.
        var items = new ObservableCollection<object> { 7, 7, 7, 42 };
        var control = new ItemsControl { ItemsSource = items };

        var lines = Lines(control, 20, 4);

        await Assert.That(control.ItemsPanel.Children.Count).IsEqualTo(4);
        await Assert.That(control.ItemsPanel.Children.Distinct().Count()).IsEqualTo(4);
        await Assert.That(lines[0]).IsEqualTo("7");
        await Assert.That(lines[1]).IsEqualTo("7");
        await Assert.That(lines[2]).IsEqualTo("7");
        await Assert.That(lines[3]).IsEqualTo("42");
    }

    // ─── Virtualized: the rows must not collapse ─────────────────────

    [Test]
    public async Task Virtualized_EqualStrings_RealiseOneContainerEach()
    {
        var items = new ObservableCollection<string> { "dup", "dup", "dup", "dup" };
        var list = new ListBox { IsVirtualizing = true, ItemsSource = items };

        var lines = Lines(list, 20, 4);

        // The distinctness is the point: item-keyed, the panel held the same container four
        // times, which draws the same text and so looks right until anything is per-row.
        await Assert.That(list.ItemsPanel.Children.Count).IsEqualTo(4);
        await Assert.That(list.ItemsPanel.Children.Distinct().Count()).IsEqualTo(4);
        for (var i = 0; i < 4; i++)
        {
            await Assert.That(lines[i]).IsEqualTo("dup");
        }
    }

    [Test]
    public async Task Virtualized_EqualStringsMixedWithOthers_RenderInOrder()
    {
        var items = new ObservableCollection<string> { "a", "dup", "b", "dup", "c" };
        var list = new ListBox { IsVirtualizing = true, ItemsSource = items };

        var lines = Lines(list, 20, 5);

        await Assert.That(list.ItemsPanel.Children.Count).IsEqualTo(5);
        await Assert.That(list.ItemsPanel.Children.Distinct().Count()).IsEqualTo(5);
        for (var i = 0; i < items.Count; i++)
        {
            await Assert.That(lines[i]).IsEqualTo(items[i]);
        }
    }

    [Test]
    public async Task Virtualized_EqualBoxedValueTypes_RealiseOneContainerEach()
    {
        var items = new ObservableCollection<object> { 7, 7, 7, 42 };
        var list = new ListBox { IsVirtualizing = true, ItemsSource = items };

        using var buffer = new CellBuffer(20, 4);
        list.Render(buffer, new Rect(0, 0, 20, 4));

        await Assert.That(list.ItemsPanel.Children.Count).IsEqualTo(4);
        await Assert.That(list.ItemsPanel.Children.Distinct().Count()).IsEqualTo(4);
    }

    [Test]
    public async Task Virtualized_ScrollingPastEqualItems_KeepsTheWindowFull()
    {
        // Every item equal, so under item keying the eviction pass and the realisation pass
        // fought over a single dictionary entry.
        var items = new ObservableCollection<string>(Enumerable.Repeat("row", 100));
        var list = new ListBox { IsVirtualizing = true, ItemsSource = items };

        using var buffer = new CellBuffer(20, 6);
        list.Render(buffer, new Rect(0, 0, 20, 6));
        await Assert.That(list.ItemsPanel.Children.Count).IsEqualTo(6);

        // Nothing is selected to begin with, so the first Down lands on row 0.
        for (var i = 0; i < 21; i++)
        {
            list.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false));
        }

        list.Render(buffer, new Rect(0, 0, 20, 6));
        await Assert.That(list.ItemsPanel.Children.Count).IsEqualTo(6);
        await Assert.That(list.SelectedIndex).IsEqualTo(20);
    }

    // ─── Selection lands on one row, and the right one ───────────────

    [Test]
    public async Task Selection_WithEqualItems_MarksOnlyTheSelectedRow()
    {
        var items = new ObservableCollection<string> { "dup", "dup", "dup" };
        var list = new ListBox { ItemsSource = items };

        list.SelectedIndex = 1;

        await Assert.That(((ListBoxItem)list.ContainerFromIndex(0)!).IsSelected).IsFalse();
        await Assert.That(((ListBoxItem)list.ContainerFromIndex(1)!).IsSelected).IsTrue();
        await Assert.That(((ListBoxItem)list.ContainerFromIndex(2)!).IsSelected).IsFalse();
    }

    [Test]
    public async Task Selection_ClickingTheSecondOfTwoEqualItems_SelectsTheSecond()
    {
        var items = new ObservableCollection<string> { "dup", "dup" };
        var list = new ListBox { ItemsSource = items };

        list.ContainerFromIndex(1)!.OnMouseEvent(new MouseEvent(0, 0, MouseButton.Left, MouseAction.Press));

        await Assert.That(list.SelectedIndex).IsEqualTo(1);
    }

    [Test]
    public async Task Selection_EqualRecords_SelectsTheClickedInstance()
    {
        var items = new ObservableCollection<Person> { new("Ada", 36), new("Ada", 36) };
        var list = new ListBox { ItemsSource = items };

        list.ContainerFromIndex(1)!.OnMouseEvent(new MouseEvent(0, 0, MouseButton.Left, MouseAction.Press));

        await Assert.That(list.SelectedIndex).IsEqualTo(1);
        await Assert.That(list.SelectedItem).IsSameReferenceAs(items[1]);
    }

    // ─── The item→container contract, spelled out ────────────────────

    [Test]
    public async Task ContainerFromItem_WithEqualItems_ReturnsTheFirstMatch()
    {
        // Documented behaviour: an item does not identify a row, so the lookup answers for the
        // first item that compares equal. ContainerFromIndex is the unambiguous one.
        var items = new ObservableCollection<string> { "dup", "dup", "other" };
        var control = new ItemsControl { ItemsSource = items };

        await Assert.That(control.ContainerFromItem("dup"))
            .IsSameReferenceAs(control.ContainerFromIndex(0));
    }

    [Test]
    public async Task ContainerFromItem_MatchesByValueNotReference()
    {
        var items = new ObservableCollection<Person> { new("Grace", 45) };
        var control = new ItemsControl { ItemsSource = items };

        // A different instance that compares equal still resolves.
        await Assert.That(control.ContainerFromItem(new Person("Grace", 45)))
            .IsSameReferenceAs(control.ContainerFromIndex(0));
    }

    [Test]
    public async Task ContainerFromIndex_WithEqualItems_ReturnsDistinctContainers()
    {
        var items = new ObservableCollection<string> { "dup", "dup", "dup" };
        var control = new ItemsControl { ItemsSource = items };

        var containers = new[]
        {
            control.ContainerFromIndex(0)!,
            control.ContainerFromIndex(1)!,
            control.ContainerFromIndex(2)!,
        };

        await Assert.That(containers.Distinct().Count()).IsEqualTo(3);
        await Assert.That(control.ContainerFromIndex(3)).IsNull();
        await Assert.That(control.ContainerFromIndex(-1)).IsNull();
    }

    [Test]
    public async Task IndexFromContainer_WithEqualItems_ReturnsTheContainersOwnRow()
    {
        var items = new ObservableCollection<string> { "dup", "dup", "dup" };
        var control = new ItemsControl { ItemsSource = items };

        for (var i = 0; i < 3; i++)
        {
            await Assert.That(control.IndexFromContainer(control.ContainerFromIndex(i)!)).IsEqualTo(i);
        }

        await Assert.That(control.IndexFromContainer(new TextBlock())).IsEqualTo(-1);
    }

    [Test]
    public async Task ItemFromContainer_WithEqualItems_ResolvesEachContainer()
    {
        var items = new ObservableCollection<Person> { new("Ada", 36), new("Ada", 36) };
        var control = new ItemsControl { ItemsSource = items };

        await Assert.That(control.ItemFromContainer(control.ContainerFromIndex(0)!))
            .IsSameReferenceAs(items[0]);
        await Assert.That(control.ItemFromContainer(control.ContainerFromIndex(1)!))
            .IsSameReferenceAs(items[1]);
    }

    [Test]
    public async Task ContainerFromIndex_Virtualized_AnswersOnlyForRealisedRows()
    {
        var items = new ObservableCollection<string>(Enumerable.Repeat("dup", 100));
        var list = new ListBox { IsVirtualizing = true, ItemsSource = items };

        using var buffer = new CellBuffer(20, 4);
        list.Render(buffer, new Rect(0, 0, 20, 4));

        await Assert.That(list.ContainerFromIndex(0)).IsNotNull();
        await Assert.That(list.ContainerFromIndex(3)).IsNotNull();
        await Assert.That(list.ContainerFromIndex(0)).IsNotSameReferenceAs(list.ContainerFromIndex(3));
        await Assert.That(list.ContainerFromIndex(50)).IsNull();
    }

    // ─── Incremental collection changes stay aligned ─────────────────

    [Test]
    public async Task Removing_OneOfSeveralEqualItems_DropsExactlyOneRow()
    {
        var items = new ObservableCollection<string> { "dup", "dup", "keep" };
        var control = new ItemsControl { ItemsSource = items };

        items.RemoveAt(0);

        var lines = Lines(control, 20, 2);
        await Assert.That(control.ItemsPanel.Children.Count).IsEqualTo(2);
        await Assert.That(lines[0]).IsEqualTo("dup");
        await Assert.That(lines[1]).IsEqualTo("keep");
    }

    [Test]
    public async Task Inserting_AnEqualItem_LandsAtTheGivenPosition()
    {
        var items = new ObservableCollection<string> { "dup", "tail" };
        var control = new ItemsControl { ItemsSource = items };

        items.Insert(1, "dup");

        var lines = Lines(control, 20, 3);
        await Assert.That(control.ItemsPanel.Children.Count).IsEqualTo(3);
        await Assert.That(lines[0]).IsEqualTo("dup");
        await Assert.That(lines[1]).IsEqualTo("dup");
        await Assert.That(lines[2]).IsEqualTo("tail");
    }

    [Test]
    public async Task Inserting_AboveTheSelection_LeavesTheHighlightOnTheSelectedRow()
    {
        // The containers stay where they are and the items move past them, so the highlight has
        // to be re-applied by index — otherwise the row that was selected keeps its flag and the
        // highlight drifts one row down from SelectedIndex, invisibly, because all three rows
        // draw the same text.
        var items = new ObservableCollection<string> { "dup", "dup", "dup" };
        var list = new ListBox { ItemsSource = items };
        list.SelectedIndex = 1;

        items.Insert(0, "dup");

        var selected = new List<int>();
        for (var i = 0; i < list.ItemsPanel.Children.Count; i++)
        {
            if (((ListBoxItem)list.ContainerFromIndex(i)!).IsSelected)
            {
                selected.Add(i);
            }
        }

        await Assert.That(list.ItemsPanel.Children.Count).IsEqualTo(4);
        await Assert.That(selected.Count).IsEqualTo(1);
        await Assert.That(selected[0]).IsEqualTo(list.SelectedIndex);
    }

    [Test]
    public async Task Replacing_AnEqualItem_SwapsOnlyThatRow()
    {
        var items = new ObservableCollection<string> { "dup", "dup", "dup" };
        var control = new ItemsControl { ItemsSource = items };

        items[1] = "changed";

        var lines = Lines(control, 20, 3);
        await Assert.That(lines[0]).IsEqualTo("dup");
        await Assert.That(lines[1]).IsEqualTo("changed");
        await Assert.That(lines[2]).IsEqualTo("dup");
    }

    [Test]
    public async Task Moving_AnEqualItem_KeepsTheRowCount()
    {
        var items = new ObservableCollection<string> { "dup", "dup", "tail" };
        var control = new ItemsControl { ItemsSource = items };

        items.Move(2, 0);

        var lines = Lines(control, 20, 3);
        await Assert.That(control.ItemsPanel.Children.Count).IsEqualTo(3);
        await Assert.That(lines[0]).IsEqualTo("tail");
        await Assert.That(lines[1]).IsEqualTo("dup");
        await Assert.That(lines[2]).IsEqualTo("dup");
    }

    [Test]
    public async Task Virtualized_AddingAnEqualItem_ShowsTheExtraRow()
    {
        var items = new ObservableCollection<string> { "dup", "dup" };
        var list = new ListBox { IsVirtualizing = true, ItemsSource = items };

        using var buffer = new CellBuffer(20, 4);
        list.Render(buffer, new Rect(0, 0, 20, 4));
        await Assert.That(list.ItemsPanel.Children.Count).IsEqualTo(2);

        items.Add("dup");
        list.Render(buffer, new Rect(0, 0, 20, 4));

        await Assert.That(list.ItemsPanel.Children.Count).IsEqualTo(3);
    }
}
