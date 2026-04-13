using System.Collections.ObjectModel;

namespace TerminalNinja.Tests.Unit.Controls;

public class TabControlTests
{
    #region Default Values

    [Test]
    public async Task Focusable_Default_IsTrue()
    {
        var tc = new TabControl();
        await Assert.That(tc.Focusable).IsTrue();
    }

    [Test]
    public async Task SelectedIndex_Default_IsMinusOne()
    {
        var tc = new TabControl();
        await Assert.That(tc.SelectedIndex).IsEqualTo(-1);
    }

    #endregion

    #region Container Generation

    [Test]
    public async Task Items_Add_TabItem_UsedDirectly()
    {
        var tc = new TabControl();
        var ti = new TabItem { Header = "Tab1", Content = new TextBlock { Text = "Content1" } };
        tc.Items.Add(ti);

        await Assert.That(tc.ItemsPanel.Children.Count).IsEqualTo(1);
        await Assert.That(tc.ItemsPanel.Children[0]).IsEqualTo(ti);
    }

    [Test]
    public async Task ItemsSource_StringItems_GeneratesTabItems()
    {
        var tc = new TabControl();
        tc.ItemsSource = new ObservableCollection<string> { "A", "B", "C" };

        await Assert.That(tc.ItemsPanel.Children.Count).IsEqualTo(3);
        for (var i = 0; i < 3; i++)
        {
            await Assert.That(tc.ItemsPanel.Children[i]).IsTypeOf<TabItem>();
        }
    }

    [Test]
    public async Task GeneratedTabItem_HasHeaderFromString()
    {
        var tc = new TabControl();
        tc.ItemsSource = new ObservableCollection<string> { "Hello" };

        var ti = tc.ItemsPanel.Children[0] as TabItem;
        await Assert.That(ti).IsNotNull();
        await Assert.That(ti!.HeaderText).IsEqualTo("Hello");
    }

    #endregion

    #region Selection

    [Test]
    public async Task SelectedIndex_SyncsSelectedItem()
    {
        var tc = new TabControl();
        tc.ItemsSource = new ObservableCollection<string> { "A", "B", "C" };

        tc.SelectedIndex = 1;

        await Assert.That(tc.SelectedItem).IsEqualTo("B");
    }

    [Test]
    public async Task SelectionChanged_Fires()
    {
        var tc = new TabControl();
        tc.ItemsSource = new ObservableCollection<string> { "A", "B" };
        var fired = false;
        tc.SelectionChanged += (_, _) => fired = true;

        tc.SelectedIndex = 0;

        await Assert.That(fired).IsTrue();
    }

    #endregion

    #region Keyboard Navigation

    [Test]
    public async Task RightArrow_SelectsNextTab()
    {
        var tc = new TabControl();
        tc.ItemsSource = new ObservableCollection<string> { "A", "B", "C" };
        tc.SelectedIndex = 0;

        tc.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));

        await Assert.That(tc.SelectedIndex).IsEqualTo(1);
    }

    [Test]
    public async Task LeftArrow_SelectsPreviousTab()
    {
        var tc = new TabControl();
        tc.ItemsSource = new ObservableCollection<string> { "A", "B", "C" };
        tc.SelectedIndex = 2;

        tc.OnKeyEvent(new KeyEvent(ConsoleKey.LeftArrow, '\0', false, false, false));

        await Assert.That(tc.SelectedIndex).IsEqualTo(1);
    }

    [Test]
    public async Task RightArrow_AtLastTab_StaysAtLast()
    {
        var tc = new TabControl();
        tc.ItemsSource = new ObservableCollection<string> { "A", "B" };
        tc.SelectedIndex = 1;

        tc.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));

        await Assert.That(tc.SelectedIndex).IsEqualTo(1);
    }

    [Test]
    public async Task LeftArrow_AtFirstTab_StaysAtFirst()
    {
        var tc = new TabControl();
        tc.ItemsSource = new ObservableCollection<string> { "A", "B" };
        tc.SelectedIndex = 0;

        tc.OnKeyEvent(new KeyEvent(ConsoleKey.LeftArrow, '\0', false, false, false));

        await Assert.That(tc.SelectedIndex).IsEqualTo(0);
    }

    [Test]
    public async Task HomeKey_SelectsFirstTab()
    {
        var tc = new TabControl();
        tc.ItemsSource = new ObservableCollection<string> { "A", "B", "C" };
        tc.SelectedIndex = 2;

        tc.OnKeyEvent(new KeyEvent(ConsoleKey.Home, '\0', false, false, false));

        await Assert.That(tc.SelectedIndex).IsEqualTo(0);
    }

    [Test]
    public async Task EndKey_SelectsLastTab()
    {
        var tc = new TabControl();
        tc.ItemsSource = new ObservableCollection<string> { "A", "B", "C" };
        tc.SelectedIndex = 0;

        tc.OnKeyEvent(new KeyEvent(ConsoleKey.End, '\0', false, false, false));

        await Assert.That(tc.SelectedIndex).IsEqualTo(2);
    }

    [Test]
    public async Task KeyboardNav_EmptyTabControl_DoesNotThrow()
    {
        var tc = new TabControl();
        tc.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        tc.OnKeyEvent(new KeyEvent(ConsoleKey.LeftArrow, '\0', false, false, false));

        await Assert.That(tc.SelectedIndex).IsEqualTo(-1);
    }

    #endregion

    #region Rendering

    [Test]
    public async Task Render_TabHeaders_AreVisible()
    {
        var tc = CreateTabControl();
        tc.SelectedIndex = 0;

        using var buffer = new CellBuffer(40, 10);
        tc.Render(buffer, new Rect(0, 0, 40, 10));

        // First tab header text starts at (1, 0)
        await Assert.That(buffer.GetCell(1, 0).Character).IsEqualTo('T');
    }

    [Test]
    public async Task Render_SelectedTabContent_IsRendered()
    {
        var tc = new TabControl();
        var ti1 = new TabItem { Header = "T1", Content = new TextBlock { Text = "Hello" } };
        var ti2 = new TabItem { Header = "T2", Content = new TextBlock { Text = "World" } };
        tc.Items.Add(ti1);
        tc.Items.Add(ti2);
        tc.SelectedIndex = 0;

        using var buffer = new CellBuffer(30, 8);
        tc.Render(buffer, new Rect(0, 0, 30, 8));

        // Content starts at row 3 (header + underline + separator)
        await Assert.That(buffer.GetCell(0, 3).Character).IsEqualTo('H');
    }

    [Test]
    public async Task Render_EmptyTabControl_DoesNotThrow()
    {
        var tc = new TabControl();

        using var buffer = new CellBuffer(30, 8);
        tc.Render(buffer, new Rect(0, 0, 30, 8));

        // Should not throw
        await Assert.That(tc.SelectedIndex).IsEqualTo(-1);
    }

    [Test]
    public async Task Render_SwitchTab_ShowsDifferentContent()
    {
        var tc = new TabControl();
        var ti1 = new TabItem { Header = "T1", Content = new TextBlock { Text = "AAA" } };
        var ti2 = new TabItem { Header = "T2", Content = new TextBlock { Text = "BBB" } };
        tc.Items.Add(ti1);
        tc.Items.Add(ti2);

        using var buffer = new CellBuffer(30, 8);

        tc.SelectedIndex = 0;
        tc.Render(buffer, new Rect(0, 0, 30, 8));
        await Assert.That(buffer.GetCell(0, 3).Character).IsEqualTo('A');

        tc.SelectedIndex = 1;
        tc.Render(buffer, new Rect(0, 0, 30, 8));
        await Assert.That(buffer.GetCell(0, 3).Character).IsEqualTo('B');
    }

    #endregion

    #region XAML

    [Test]
    public async Task Xaml_ParsesTabItems()
    {
        var xaml = """
            <TabControl xmlns="http://schemas.terminalninja.dev/xaml">
                <TabItem Header="Tab1">
                    <TextBlock Text="Content1" />
                </TabItem>
                <TabItem Header="Tab2">
                    <TextBlock Text="Content2" />
                </TabItem>
            </TabControl>
            """;

        var tc = TerminalXaml.Load<TabControl>(xaml);

        await Assert.That(tc.ItemsPanel.Children.Count).IsEqualTo(2);
        var ti1 = tc.ItemsPanel.Children[0] as TabItem;
        await Assert.That(ti1).IsNotNull();
        await Assert.That(ti1!.HeaderText).IsEqualTo("Tab1");
    }

    #endregion

    #region Helpers

    private static TabControl CreateTabControl()
    {
        var tc = new TabControl();
        tc.Items.Add(new TabItem { Header = "Tab1", Content = new TextBlock { Text = "Content 1" } });
        tc.Items.Add(new TabItem { Header = "Tab2", Content = new TextBlock { Text = "Content 2" } });
        return tc;
    }

    #endregion
}
