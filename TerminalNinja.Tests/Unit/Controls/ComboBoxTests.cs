using System.Collections.ObjectModel;

namespace TerminalNinja.Tests.Unit.Controls;

// Opening the drop-down pushes a Popup onto Application.Current's overlay stack.
// Application.Current is a static singleton, so running these tests alongside the
// other Application-touching classes lets a ComboBox popup land on another test's
// app and skew its Overlays count. Serialize on the same constraint key.
[NotInParallel("ApplicationSingleton")]
public class ComboBoxTests
{
    #region Default Values

    [Test]
    public async Task IsDropDownOpen_Default_IsFalse()
    {
        var cb = new ComboBox();
        await Assert.That(cb.IsDropDownOpen).IsFalse();
    }

    [Test]
    public async Task MaxDropDownHeight_Default_Is8()
    {
        var cb = new ComboBox();
        await Assert.That(cb.MaxDropDownHeight).IsEqualTo(8);
    }

    [Test]
    public async Task Focusable_Default_IsTrue()
    {
        var cb = new ComboBox();
        await Assert.That(cb.Focusable).IsTrue();
    }

    [Test]
    public async Task SelectedIndex_Default_IsMinusOne()
    {
        var cb = new ComboBox();
        await Assert.That(cb.SelectedIndex).IsEqualTo(-1);
    }

    #endregion

    #region Container Generation

    [Test]
    public async Task ItemsSource_GeneratesComboBoxItemContainers()
    {
        var cb = new ComboBox();
        cb.ItemsSource = new ObservableCollection<string> { "A", "B", "C" };

        await Assert.That(cb.ItemsPanel.Children.Count).IsEqualTo(3);
        for (var i = 0; i < 3; i++)
        {
            await Assert.That(cb.ItemsPanel.Children[i]).IsTypeOf<ComboBoxItem>();
        }
    }

    [Test]
    public async Task IsItemItsOwnContainer_ComboBoxItem_ReturnsTrue()
    {
        var cb = new ComboBox();
        var cbi = new ComboBoxItem { Content = "Direct" };
        cb.Items.Add(cbi);

        await Assert.That(cb.ItemsPanel.Children.Count).IsEqualTo(1);
        await Assert.That(cb.ItemsPanel.Children[0]).IsEqualTo(cbi);
    }

    [Test]
    public async Task ContainerFromItem_ReturnsComboBoxItem()
    {
        var cb = new ComboBox();
        cb.ItemsSource = new ObservableCollection<string> { "X", "Y" };

        var container = cb.ContainerFromItem("Y");
        await Assert.That(container).IsNotNull();
        await Assert.That(container).IsTypeOf<ComboBoxItem>();
    }

    #endregion

    #region Selection

    [Test]
    public async Task SelectedIndex_SyncsSelectedItem()
    {
        var cb = new ComboBox();
        cb.ItemsSource = new ObservableCollection<string> { "A", "B", "C" };

        cb.SelectedIndex = 1;

        await Assert.That(cb.SelectedItem).IsEqualTo("B");
    }

    [Test]
    public async Task SelectionChanged_Fires()
    {
        var cb = new ComboBox();
        cb.ItemsSource = new ObservableCollection<string> { "A", "B" };
        var fired = false;
        cb.SelectionChanged += (_, _) => fired = true;

        cb.SelectedIndex = 0;

        await Assert.That(fired).IsTrue();
    }

    #endregion

    #region Keyboard Navigation (Closed)

    [Test]
    public async Task DownArrow_Closed_SelectsNextItem()
    {
        var cb = new ComboBox();
        cb.ItemsSource = new ObservableCollection<string> { "A", "B", "C" };
        cb.SelectedIndex = 0;

        cb.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false));

        await Assert.That(cb.SelectedIndex).IsEqualTo(1);
    }

    [Test]
    public async Task UpArrow_Closed_SelectsPreviousItem()
    {
        var cb = new ComboBox();
        cb.ItemsSource = new ObservableCollection<string> { "A", "B", "C" };
        cb.SelectedIndex = 2;

        cb.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(cb.SelectedIndex).IsEqualTo(1);
    }

    [Test]
    public async Task Enter_Closed_OpensDropDown()
    {
        var cb = new ComboBox();
        cb.ItemsSource = new ObservableCollection<string> { "A", "B" };

        cb.OnKeyEvent(new KeyEvent(ConsoleKey.Enter, '\r', false, false, false));

        await Assert.That(cb.IsDropDownOpen).IsTrue();
    }

    [Test]
    public async Task Space_Closed_OpensDropDown()
    {
        var cb = new ComboBox();
        cb.ItemsSource = new ObservableCollection<string> { "A" };

        cb.OnKeyEvent(new KeyEvent(ConsoleKey.Spacebar, ' ', false, false, false));

        await Assert.That(cb.IsDropDownOpen).IsTrue();
    }

    #endregion

    #region Keyboard Navigation (Open)

    [Test]
    public async Task Escape_Open_ClosesDropDown()
    {
        var cb = new ComboBox();
        cb.ItemsSource = new ObservableCollection<string> { "A", "B" };
        cb.IsDropDownOpen = true;

        cb.OnKeyEvent(new KeyEvent(ConsoleKey.Escape, '\0', false, false, false));

        await Assert.That(cb.IsDropDownOpen).IsFalse();
    }

    [Test]
    public async Task Enter_Open_ClosesDropDown()
    {
        var cb = new ComboBox();
        cb.ItemsSource = new ObservableCollection<string> { "A", "B" };
        cb.SelectedIndex = 0;
        cb.IsDropDownOpen = true;

        cb.OnKeyEvent(new KeyEvent(ConsoleKey.Enter, '\r', false, false, false));

        await Assert.That(cb.IsDropDownOpen).IsFalse();
    }

    [Test]
    public async Task DownArrow_Open_NavigatesItems()
    {
        var cb = new ComboBox();
        cb.ItemsSource = new ObservableCollection<string> { "A", "B", "C" };
        cb.SelectedIndex = 0;
        cb.IsDropDownOpen = true;

        cb.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false));

        await Assert.That(cb.SelectedIndex).IsEqualTo(1);
    }

    [Test]
    public async Task Home_Open_SelectsFirst()
    {
        var cb = new ComboBox();
        cb.ItemsSource = new ObservableCollection<string> { "A", "B", "C" };
        cb.SelectedIndex = 2;
        cb.IsDropDownOpen = true;

        cb.OnKeyEvent(new KeyEvent(ConsoleKey.Home, '\0', false, false, false));

        await Assert.That(cb.SelectedIndex).IsEqualTo(0);
    }

    [Test]
    public async Task End_Open_SelectsLast()
    {
        var cb = new ComboBox();
        cb.ItemsSource = new ObservableCollection<string> { "A", "B", "C" };
        cb.SelectedIndex = 0;
        cb.IsDropDownOpen = true;

        cb.OnKeyEvent(new KeyEvent(ConsoleKey.End, '\0', false, false, false));

        await Assert.That(cb.SelectedIndex).IsEqualTo(2);
    }

    #endregion

    #region Mouse

    [Test]
    public async Task LeftClick_TogglesDropDown()
    {
        var cb = new ComboBox();
        cb.ItemsSource = new ObservableCollection<string> { "A" };

        cb.OnMouseEvent(new MouseEvent(5, 1, MouseButton.Left, MouseAction.Press));

        await Assert.That(cb.IsDropDownOpen).IsTrue();
    }

    #endregion

    #region Rendering

    [Test]
    public async Task Render_ShowsSelectedItemText()
    {
        var cb = new ComboBox();
        cb.ItemsSource = new ObservableCollection<string> { "Hello", "World" };
        cb.SelectedIndex = 0;

        using var buffer = new CellBuffer(20, 3);
        cb.Render(buffer, new Rect(0, 0, 20, 3));

        // Selected text "Hello" starts at (1, 1) — inside border
        await Assert.That(buffer.GetCell(1, 1).Codepoint).IsEqualTo('H');
        await Assert.That(buffer.GetCell(5, 1).Codepoint).IsEqualTo('o');
    }

    [Test]
    public async Task Render_ShowsDropdownIndicator()
    {
        var cb = new ComboBox();
        cb.ItemsSource = new ObservableCollection<string> { "Test" };

        using var buffer = new CellBuffer(20, 3);
        cb.Render(buffer, new Rect(0, 0, 20, 3));

        // Dropdown arrow ▼ at right edge minus 2 (border)
        var arrowCell = buffer.GetCell(18, 1);
        await Assert.That(arrowCell.Codepoint).IsEqualTo('\u25BC');
    }

    [Test]
    public async Task Render_DrawsBorder()
    {
        var cb = new ComboBox();

        using var buffer = new CellBuffer(20, 3);
        cb.Render(buffer, new Rect(0, 0, 20, 3));

        var corner = buffer.GetCell(0, 0);
        await Assert.That(corner.Codepoint).IsNotEqualTo(' ');
        await Assert.That(corner.Codepoint).IsNotEqualTo('\0');
    }

    [Test]
    public async Task Render_NoSelection_ShowsEmpty()
    {
        var cb = new ComboBox();

        using var buffer = new CellBuffer(20, 3);
        cb.Render(buffer, new Rect(0, 0, 20, 3));

        // Middle row should have the dropdown arrow but no text
        await Assert.That(buffer.GetCell(18, 1).Codepoint).IsEqualTo('\u25BC');
    }

    #endregion

    #region XAML

    [Test]
    public async Task Xaml_ParsesComboBoxItems()
    {
        var xaml = """
            <ComboBox xmlns="http://schemas.terminalninja.dev/xaml">
                <ComboBoxItem Content="Option 1" />
                <ComboBoxItem Content="Option 2" />
            </ComboBox>
            """;

        var cb = TerminalXaml.Load<ComboBox>(xaml);

        await Assert.That(cb.ItemsPanel.Children.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Xaml_ParsesMaxDropDownHeight()
    {
        var xaml = """
            <ComboBox xmlns="http://schemas.terminalninja.dev/xaml"
                      MaxDropDownHeight="12" />
            """;

        var cb = TerminalXaml.Load<ComboBox>(xaml);

        await Assert.That(cb.MaxDropDownHeight).IsEqualTo(12);
    }

    #endregion

    #region Boundary Clamping

    [Test]
    public async Task DownArrow_AtLastItem_StaysAtLast()
    {
        var cb = new ComboBox();
        cb.ItemsSource = new ObservableCollection<string> { "A", "B" };
        cb.SelectedIndex = 1;

        cb.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false));

        await Assert.That(cb.SelectedIndex).IsEqualTo(1);
    }

    [Test]
    public async Task UpArrow_AtFirstItem_StaysAtFirst()
    {
        var cb = new ComboBox();
        cb.ItemsSource = new ObservableCollection<string> { "A", "B" };
        cb.SelectedIndex = 0;

        cb.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(cb.SelectedIndex).IsEqualTo(0);
    }

    [Test]
    public async Task KeyboardNav_EmptyList_DoesNotThrow()
    {
        var cb = new ComboBox();
        cb.ItemsSource = new ObservableCollection<string>();

        cb.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false));
        cb.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(cb.SelectedIndex).IsEqualTo(-1);
    }

    #endregion
}
