using System.Collections.ObjectModel;

namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Tests for the ListBox control covering:
/// - Container generation (ListBoxItem wrapping)
/// - Keyboard navigation (Up/Down/Home/End)
/// - Mouse click selection
/// - Rendering with selection highlight
/// - SelectedBackground / SelectedForeground propagation
/// - ItemTemplate support
/// - IsItemItsOwnContainer for ListBoxItem instances
/// </summary>
public class ListBoxTests
{
    #region Container Generation

    [Test]
    public async Task ItemsSource_GeneratesListBoxItemContainers()
    {
        var listBox = new ListBox();
        var items = new ObservableCollection<string> { "A", "B", "C" };
        listBox.ItemsSource = items;

        await Assert.That(listBox.ItemsPanel.Children.Count).IsEqualTo(3);

        for (var i = 0; i < 3; i++)
        {
            var child = listBox.ItemsPanel.Children[i];
            await Assert.That(child).IsTypeOf<ListBoxItem>();
        }
    }

    [Test]
    public async Task GeneratedContainer_HasTextBlockContent()
    {
        var listBox = new ListBox();
        listBox.ItemsSource = new ObservableCollection<string> { "Hello" };

        var lbi = listBox.ItemsPanel.Children[0] as ListBoxItem;
        await Assert.That(lbi).IsNotNull();
        await Assert.That(lbi!.Content).IsTypeOf<TextBlock>();

        var tb = (TextBlock)lbi.Content!;
        await Assert.That(tb.Text).IsEqualTo("Hello");
    }

    [Test]
    public async Task IsItemItsOwnContainer_ListBoxItem_ReturnsTrue()
    {
        var listBox = new ListBox();
        var lbi = new ListBoxItem { Content = new TextBlock { Text = "Direct" } };
        listBox.Items.Add(lbi);

        // The ListBoxItem itself should be used directly (not wrapped)
        await Assert.That(listBox.ItemsPanel.Children.Count).IsEqualTo(1);
        await Assert.That(listBox.ItemsPanel.Children[0]).IsEqualTo(lbi);
    }

    [Test]
    public async Task ContainerFromItem_ReturnsGeneratedListBoxItem()
    {
        var listBox = new ListBox();
        listBox.ItemsSource = new ObservableCollection<string> { "X", "Y" };

        var container = listBox.ContainerFromItem("Y");
        await Assert.That(container).IsNotNull();
        await Assert.That(container).IsTypeOf<ListBoxItem>();
    }

    [Test]
    public async Task ItemFromContainer_ReturnsDataItem()
    {
        var listBox = new ListBox();
        listBox.ItemsSource = new ObservableCollection<string> { "X", "Y" };

        var container = listBox.ContainerFromItem("Y")!;
        var item = listBox.ItemFromContainer(container);
        await Assert.That(item).IsEqualTo("Y");
    }

    #endregion

    #region Selection Colors

    [Test]
    public async Task SelectedBackground_Default_IsBlue()
    {
        var listBox = new ListBox();
        await Assert.That(listBox.SelectedBackground).IsEqualTo(Color.Blue);
    }

    [Test]
    public async Task SelectedForeground_Default_IsWhite()
    {
        var listBox = new ListBox();
        await Assert.That(listBox.SelectedForeground).IsEqualTo(Color.White);
    }

    [Test]
    public async Task PrepareContainer_PropagatesSelectionColors()
    {
        var listBox = new ListBox
        {
            SelectedBackground = Color.Green,
            SelectedForeground = Color.Yellow
        };
        listBox.ItemsSource = new ObservableCollection<string> { "Item1" };

        var lbi = listBox.ItemsPanel.Children[0] as ListBoxItem;
        await Assert.That(lbi).IsNotNull();
        await Assert.That(lbi!.SelectedBackground).IsEqualTo(Color.Green);
        await Assert.That(lbi.SelectedForeground).IsEqualTo(Color.Yellow);
    }

    #endregion

    #region Keyboard Navigation

    [Test]
    public async Task DownArrow_SelectsNextItem()
    {
        var listBox = new ListBox();
        listBox.ItemsSource = new ObservableCollection<string> { "A", "B", "C" };
        listBox.SelectedIndex = 0;

        listBox.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false));

        await Assert.That(listBox.SelectedIndex).IsEqualTo(1);
        await Assert.That(listBox.SelectedItem).IsEqualTo("B");
    }

    [Test]
    public async Task UpArrow_SelectsPreviousItem()
    {
        var listBox = new ListBox();
        listBox.ItemsSource = new ObservableCollection<string> { "A", "B", "C" };
        listBox.SelectedIndex = 2;

        listBox.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(listBox.SelectedIndex).IsEqualTo(1);
        await Assert.That(listBox.SelectedItem).IsEqualTo("B");
    }

    [Test]
    public async Task DownArrow_AtLastItem_StaysAtLast()
    {
        var listBox = new ListBox();
        listBox.ItemsSource = new ObservableCollection<string> { "A", "B" };
        listBox.SelectedIndex = 1;

        listBox.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false));

        await Assert.That(listBox.SelectedIndex).IsEqualTo(1);
    }

    [Test]
    public async Task UpArrow_AtFirstItem_StaysAtFirst()
    {
        var listBox = new ListBox();
        listBox.ItemsSource = new ObservableCollection<string> { "A", "B" };
        listBox.SelectedIndex = 0;

        listBox.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(listBox.SelectedIndex).IsEqualTo(0);
    }

    [Test]
    public async Task HomeKey_SelectsFirstItem()
    {
        var listBox = new ListBox();
        listBox.ItemsSource = new ObservableCollection<string> { "A", "B", "C" };
        listBox.SelectedIndex = 2;

        listBox.OnKeyEvent(new KeyEvent(ConsoleKey.Home, '\0', false, false, false));

        await Assert.That(listBox.SelectedIndex).IsEqualTo(0);
    }

    [Test]
    public async Task EndKey_SelectsLastItem()
    {
        var listBox = new ListBox();
        listBox.ItemsSource = new ObservableCollection<string> { "A", "B", "C" };
        listBox.SelectedIndex = 0;

        listBox.OnKeyEvent(new KeyEvent(ConsoleKey.End, '\0', false, false, false));

        await Assert.That(listBox.SelectedIndex).IsEqualTo(2);
    }

    [Test]
    public async Task DownArrow_FromNoSelection_SelectsFirst()
    {
        var listBox = new ListBox();
        listBox.ItemsSource = new ObservableCollection<string> { "A", "B", "C" };

        listBox.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false));

        await Assert.That(listBox.SelectedIndex).IsEqualTo(0);
    }

    [Test]
    public async Task KeyboardNav_OnEmptyList_DoesNotThrow()
    {
        var listBox = new ListBox();
        listBox.ItemsSource = new ObservableCollection<string>();

        // Should not throw
        listBox.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false));
        listBox.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));
        listBox.OnKeyEvent(new KeyEvent(ConsoleKey.Home, '\0', false, false, false));
        listBox.OnKeyEvent(new KeyEvent(ConsoleKey.End, '\0', false, false, false));

        await Assert.That(listBox.SelectedIndex).IsEqualTo(-1);
    }

    #endregion

    #region Rendering

    [Test]
    public async Task Render_SelectedItem_UsesSelectedBackground()
    {
        var listBox = new ListBox
        {
            Background = Color.Black,
            Foreground = Color.White,
            SelectedBackground = Color.Blue,
            SelectedForeground = Color.Yellow
        };
        listBox.ItemsSource = new ObservableCollection<string> { "Hello" };
        listBox.SelectedIndex = 0;

        using var buffer = new CellBuffer(20, 5);
        listBox.Render(buffer, new Rect(0, 0, 20, 5));

        // The selected item should render with the SelectedBackground color
        var cell = buffer.GetCell(0, 0);
        await Assert.That(cell.Background).IsEqualTo(Color.Blue);
    }

    [Test]
    public async Task Render_UnselectedItem_UsesNormalBackground()
    {
        var listBox = new ListBox
        {
            Background = Color.Black,
            Foreground = Color.White,
            SelectedBackground = Color.Blue,
            SelectedForeground = Color.Yellow
        };
        listBox.ItemsSource = new ObservableCollection<string> { "A", "B" };
        // Select second item
        listBox.SelectedIndex = 1;

        using var buffer = new CellBuffer(20, 5);
        listBox.Render(buffer, new Rect(0, 0, 20, 5));

        // First item (unselected) should NOT have selectedBackground
        var cell = buffer.GetCell(0, 0);
        await Assert.That(cell.Background).IsNotEqualTo(Color.Blue);
    }

    [Test]
    public async Task Render_EmptyListBox_DoesNotThrow()
    {
        var listBox = new ListBox();
        listBox.ItemsSource = new ObservableCollection<string>();

        using var buffer = new CellBuffer(20, 5);

        // Should not throw
        listBox.Render(buffer, new Rect(0, 0, 20, 5));

        await Assert.That(listBox.ItemsPanel.Children.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Render_SelectedItemText_IsRendered()
    {
        var listBox = new ListBox
        {
            SelectedBackground = Color.Blue,
            SelectedForeground = Color.White
        };
        listBox.ItemsSource = new ObservableCollection<string> { "Test" };
        listBox.SelectedIndex = 0;

        using var buffer = new CellBuffer(20, 5);
        listBox.Render(buffer, new Rect(0, 0, 20, 5));

        // Position 0 has the selection indicator '▌'
        var indicator = buffer.GetCell(0, 0);
        await Assert.That(indicator.Codepoint).IsEqualTo('\u258C');

        // The first char of "Test" should be 'T' at position 1 (offset by indicator)
        var cell = buffer.GetCell(1, 0);
        await Assert.That(cell.Codepoint).IsEqualTo('T');
    }

    #endregion

    #region ObservableCollection Changes

    [Test]
    public async Task AddingItem_ToObservableCollection_AddsContainer()
    {
        var listBox = new ListBox();
        var items = new ObservableCollection<string> { "A" };
        listBox.ItemsSource = items;

        await Assert.That(listBox.ItemsPanel.Children.Count).IsEqualTo(1);

        items.Add("B");

        await Assert.That(listBox.ItemsPanel.Children.Count).IsEqualTo(2);
    }

    [Test]
    public async Task RemovingItem_FromObservableCollection_RemovesContainer()
    {
        var listBox = new ListBox();
        var items = new ObservableCollection<string> { "A", "B", "C" };
        listBox.ItemsSource = items;

        items.Remove("B");

        await Assert.That(listBox.ItemsPanel.Children.Count).IsEqualTo(2);
    }

    #endregion

    #region ListBoxItem

    [Test]
    public async Task ListBoxItem_Focusable_IsFalse()
    {
        var lbi = new ListBoxItem();
        await Assert.That(lbi.Focusable).IsFalse();
    }

    [Test]
    public async Task ListBoxItem_IsSelected_Default_IsFalse()
    {
        var lbi = new ListBoxItem();
        await Assert.That(lbi.IsSelected).IsFalse();
    }

    [Test]
    public async Task ListBoxItem_SelectedBackground_Default_IsBlue()
    {
        var lbi = new ListBoxItem();
        await Assert.That(lbi.SelectedBackground).IsEqualTo(Color.Blue);
    }

    [Test]
    public async Task ListBoxItem_SelectedForeground_Default_IsWhite()
    {
        var lbi = new ListBoxItem();
        await Assert.That(lbi.SelectedForeground).IsEqualTo(Color.White);
    }

    [Test]
    public async Task ListBoxItem_GetPreferredSize_ReturnsAtLeastOneRowHigh()
    {
        var lbi = new ListBoxItem();
        var size = lbi.GetPreferredSize(new Rect(0, 0, 40, 10));
        await Assert.That(size.Height).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task ListBoxItem_Render_Selected_FillsWithSelectedBackground()
    {
        var lbi = new ListBoxItem
        {
            IsSelected = true,
            SelectedBackground = Color.Green,
            SelectedForeground = Color.Red,
            Content = new TextBlock { Text = "X" }
        };

        using var buffer = new CellBuffer(10, 1);
        lbi.Render(buffer, new Rect(0, 0, 10, 1));

        var cell = buffer.GetCell(0, 0);
        await Assert.That(cell.Background).IsEqualTo(Color.Green);
    }

    [Test]
    public async Task ListBoxItem_Render_Unselected_UsesNormalBackground()
    {
        var lbi = new ListBoxItem
        {
            IsSelected = false,
            Background = Color.DarkGray,
            SelectedBackground = Color.Green,
            Content = new TextBlock { Text = "X" }
        };

        using var buffer = new CellBuffer(10, 1);
        lbi.Render(buffer, new Rect(0, 0, 10, 1));

        var cell = buffer.GetCell(0, 0);
        await Assert.That(cell.Background).IsEqualTo(Color.DarkGray);
    }

    [Test]
    public async Task ListBoxItem_MouseClick_NotifiesParentSelector()
    {
        var listBox = new ListBox();
        listBox.ItemsSource = new ObservableCollection<string> { "First", "Second" };

        var secondContainer = listBox.ContainerFromItem("Second")!;
        // Simulate mouse click on the second item
        secondContainer.OnMouseEvent(new MouseEvent(0, 0, MouseButton.Left, MouseAction.Press));

        await Assert.That(listBox.SelectedIndex).IsEqualTo(1);
        await Assert.That(listBox.SelectedItem).IsEqualTo("Second");
    }

    #endregion

    #region Focusable

    [Test]
    public async Task ListBox_IsFocusable()
    {
        var listBox = new ListBox();
        await Assert.That(listBox.Focusable).IsTrue();
    }

    #endregion

    #region FocusManager Mouse Integration

    [Test]
    public async Task FocusManager_MouseClickOnItem_SelectsItem()
    {
        // Arrange — ListBox with 3 items, each 1 row high, laid out vertically
        var listBox = new ListBox();
        listBox.ItemsSource = new ObservableCollection<string> { "Alpha", "Beta", "Gamma" };

        var rootBounds = new Rect(0, 0, 20, 10);
        var focusManager = new FocusManager();

        // Render to establish layout
        using var buffer = new CellBuffer(20, 10);
        listBox.Render(buffer, rootBounds);

        // Act — click on the second item (row index 1, which is y=1)
        var clickEvent = new MouseEvent(5, 1, MouseButton.Left, MouseAction.Press);
        focusManager.HandleMouseEvent(listBox, rootBounds, clickEvent);

        // Assert — second item should be selected
        await Assert.That(listBox.SelectedIndex).IsEqualTo(1);
        await Assert.That(listBox.SelectedItem).IsEqualTo("Beta");
    }

    [Test]
    public async Task FocusManager_MouseClickOnItem_FocusesListBox()
    {
        // The ListBox (focusable) should receive focus, not the ListBoxItem (non-focusable)
        var listBox = new ListBox();
        listBox.ItemsSource = new ObservableCollection<string> { "Alpha", "Beta" };

        var rootBounds = new Rect(0, 0, 20, 10);
        var focusManager = new FocusManager();

        using var buffer = new CellBuffer(20, 10);
        listBox.Render(buffer, rootBounds);

        // Act — click on the first item
        var clickEvent = new MouseEvent(2, 0, MouseButton.Left, MouseAction.Press);
        focusManager.HandleMouseEvent(listBox, rootBounds, clickEvent);

        // Assert — ListBox should have focus
        await Assert.That(focusManager.FocusedElement).IsEqualTo(listBox);
        await Assert.That(listBox.IsFocused).IsTrue();
    }

    [Test]
    public async Task FocusManager_MouseClickOnThirdItem_SelectsThirdItem()
    {
        var listBox = new ListBox();
        listBox.ItemsSource = new ObservableCollection<string> { "One", "Two", "Three" };

        var rootBounds = new Rect(0, 0, 20, 10);
        var focusManager = new FocusManager();

        using var buffer = new CellBuffer(20, 10);
        listBox.Render(buffer, rootBounds);

        // Act — click on the third item (y=2)
        var clickEvent = new MouseEvent(3, 2, MouseButton.Left, MouseAction.Press);
        focusManager.HandleMouseEvent(listBox, rootBounds, clickEvent);

        // Assert
        await Assert.That(listBox.SelectedIndex).IsEqualTo(2);
        await Assert.That(listBox.SelectedItem).IsEqualTo("Three");
    }

    #endregion

    #region Items Collection (Direct Add)

    [Test]
    public async Task Items_Add_SyncsToItemsPanel()
    {
        // Bug fix: Items.Add should immediately sync to ItemsPanel.Children
        var listBox = new ListBox();
        var lbi1 = new ListBoxItem { Content = new TextBlock { Text = "A" } };
        var lbi2 = new ListBoxItem { Content = new TextBlock { Text = "B" } };

        listBox.Items.Add(lbi1);
        listBox.Items.Add(lbi2);

        await Assert.That(listBox.ItemsPanel.Children.Count).IsEqualTo(2);
        await Assert.That(listBox.ItemsPanel.Children[0]).IsEqualTo(lbi1);
        await Assert.That(listBox.ItemsPanel.Children[1]).IsEqualTo(lbi2);
    }

    [Test]
    public async Task Items_AddStringContent_RendersText()
    {
        // Simulates the XAML pattern: <ListBoxItem>Hello</ListBoxItem>
        // where Content is a string (not a TextBlock)
        var listBox = new ListBox();
        var lbi = new ListBoxItem { Content = "Hello" };
        listBox.Items.Add(lbi);

        using var buffer = new CellBuffer(20, 5);
        listBox.Render(buffer, new Rect(0, 0, 20, 5));

        // "Hello" should render as text
        await Assert.That(buffer.GetCell(0, 0).Codepoint).IsEqualTo('H');
        await Assert.That(buffer.GetCell(4, 0).Codepoint).IsEqualTo('o');
    }

    [Test]
    public async Task Items_Remove_SyncsToItemsPanel()
    {
        var listBox = new ListBox();
        var lbi1 = new ListBoxItem { Content = "A" };
        var lbi2 = new ListBoxItem { Content = "B" };
        listBox.Items.Add(lbi1);
        listBox.Items.Add(lbi2);

        await Assert.That(listBox.ItemsPanel.Children.Count).IsEqualTo(2);

        listBox.Items.Remove(lbi1);

        await Assert.That(listBox.ItemsPanel.Children.Count).IsEqualTo(1);
        await Assert.That(listBox.ItemsPanel.Children[0]).IsEqualTo(lbi2);
    }

    #endregion

    #region XAML Loading

    [Test]
    public async Task Xaml_ListBoxItem_BareTextContent_SetsContent()
    {
        // Bug fix: <ListBoxItem>text</ListBoxItem> should set Content = "text"
        var xaml = """
            <ListBox xmlns="http://schemas.terminalninja.dev/xaml">
                <ListBoxItem>Item One</ListBoxItem>
                <ListBoxItem>Item Two</ListBoxItem>
            </ListBox>
            """;

        var listBox = TerminalXaml.Load<ListBox>(xaml);

        await Assert.That(listBox.ItemsPanel.Children.Count).IsEqualTo(2);

        var lbi1 = listBox.ItemsPanel.Children[0] as ListBoxItem;
        var lbi2 = listBox.ItemsPanel.Children[1] as ListBoxItem;
        await Assert.That(lbi1).IsNotNull();
        await Assert.That(lbi2).IsNotNull();
        await Assert.That(lbi1!.Content).IsEqualTo("Item One");
        await Assert.That(lbi2!.Content).IsEqualTo("Item Two");
    }

    [Test]
    public async Task Xaml_ListBoxItem_BareTextContent_RendersText()
    {
        // End-to-end: XAML-loaded ListBoxItems with bare text content render visibly
        var xaml = """
            <ListBox xmlns="http://schemas.terminalninja.dev/xaml"
                     Background="#1A1A2E" Foreground="White"
                     SelectedBackground="Blue" SelectedForeground="White">
                <ListBoxItem>Hello</ListBoxItem>
                <ListBoxItem>World</ListBoxItem>
            </ListBox>
            """;

        var listBox = TerminalXaml.Load<ListBox>(xaml);
        listBox.SelectedIndex = 0;

        using var buffer = new CellBuffer(20, 5);
        listBox.Render(buffer, new Rect(0, 0, 20, 5));

        // First item "Hello" — selected, so indicator at 0, text starts at 1
        await Assert.That(buffer.GetCell(0, 0).Codepoint).IsEqualTo('\u258C'); // ▌ indicator
        await Assert.That(buffer.GetCell(1, 0).Codepoint).IsEqualTo('H');
        await Assert.That(buffer.GetCell(5, 0).Codepoint).IsEqualTo('o');

        // Second item "World" — not selected, no indicator, text at 0
        await Assert.That(buffer.GetCell(0, 1).Codepoint).IsEqualTo('W');
        await Assert.That(buffer.GetCell(4, 1).Codepoint).IsEqualTo('d');
    }

    #endregion
}
