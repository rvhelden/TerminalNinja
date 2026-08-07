using TerminalNinja.Input;

namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// A <see cref="TerminalNinja.Controls.Primitives.Selector"/>'s own input must not destroy the
/// binding it is supposed to drive.
/// </summary>
/// <remarks>
/// The public <c>SelectedIndex</c> setter goes through <c>SetValue</c>, which detaches the
/// expression — a local value overrides a binding, as in WPF. Every keyboard path therefore has to
/// use <c>SetCurrentValue</c>. Without it the first arrow key silently deleted the binding: the
/// control kept moving on screen, the view model stopped hearing about it, and nothing anywhere
/// reported a problem. These tests fail if any of it regresses.
/// </remarks>
public class SelectorBindingTests
{
    internal sealed class IndexViewModel : TerminalNinja.Xaml.Mvvm.ViewModelBase
    {
        public int Selected
        {
            get;
            set => SetProperty(ref field, value);
        }
    }

    private static KeyEvent Key(ConsoleKey key) => new(key, '\0', false, false, false);

    [Test]
    public async Task ListBox_TwoWaySelectedIndex_SurvivesRepeatedArrowKeys()
    {
        var vm = new IndexViewModel();
        const string xaml = """
            <ListBox xmlns="http://schemas.terminalninja.dev/xaml"
                     SelectedIndex="{Binding Selected, Mode=TwoWay}">
                <TextBlock Text="a" />
                <TextBlock Text="b" />
                <TextBlock Text="c" />
            </ListBox>
            """;

        var list = TerminalXaml.Load<ListBox>(xaml, vm);

        list.OnKeyEvent(Key(ConsoleKey.DownArrow));
        list.OnKeyEvent(Key(ConsoleKey.DownArrow));

        // The second press is the one that used to be lost.
        await Assert.That(list.SelectedIndex).IsEqualTo(2);
        await Assert.That(vm.Selected).IsEqualTo(2);
    }

    [Test]
    public async Task ListBox_TwoWaySelectedIndex_StillFlowsFromTheSourceAfterArrowKeys()
    {
        var vm = new IndexViewModel();
        const string xaml = """
            <ListBox xmlns="http://schemas.terminalninja.dev/xaml"
                     SelectedIndex="{Binding Selected, Mode=TwoWay}">
                <TextBlock Text="a" />
                <TextBlock Text="b" />
                <TextBlock Text="c" />
            </ListBox>
            """;

        var list = TerminalXaml.Load<ListBox>(xaml, vm);

        list.OnKeyEvent(Key(ConsoleKey.DownArrow));

        // The reverse direction is the half that mattered to callers driving a list from code:
        // once the expression was gone, assigning the view model moved nothing on screen.
        vm.Selected = 0;

        await Assert.That(list.SelectedIndex).IsEqualTo(0);
    }

    [Test]
    public async Task ListBox_HomeAndEnd_KeepTheBinding()
    {
        var vm = new IndexViewModel();
        const string xaml = """
            <ListBox xmlns="http://schemas.terminalninja.dev/xaml"
                     SelectedIndex="{Binding Selected, Mode=TwoWay}">
                <TextBlock Text="a" />
                <TextBlock Text="b" />
                <TextBlock Text="c" />
            </ListBox>
            """;

        var list = TerminalXaml.Load<ListBox>(xaml, vm);

        list.OnKeyEvent(Key(ConsoleKey.End));
        await Assert.That(vm.Selected).IsEqualTo(2);

        list.OnKeyEvent(Key(ConsoleKey.Home));
        await Assert.That(vm.Selected).IsEqualTo(0);

        vm.Selected = 1;
        await Assert.That(list.SelectedIndex).IsEqualTo(1);
    }

    [Test]
    public async Task TabControl_TwoWaySelectedIndex_SurvivesArrowKeys()
    {
        var vm = new IndexViewModel();
        const string xaml = """
            <TabControl xmlns="http://schemas.terminalninja.dev/xaml"
                        SelectedIndex="{Binding Selected, Mode=TwoWay}">
                <TabItem Header="one"><TextBlock Text="1" /></TabItem>
                <TabItem Header="two"><TextBlock Text="2" /></TabItem>
                <TabItem Header="three"><TextBlock Text="3" /></TabItem>
            </TabControl>
            """;

        var tabs = TerminalXaml.Load<TabControl>(xaml, vm);

        tabs.OnKeyEvent(Key(ConsoleKey.RightArrow));
        tabs.OnKeyEvent(Key(ConsoleKey.RightArrow));

        // This is the concrete failure that started it: the tab moved on screen while the view
        // model stayed on 0, so the screen's on-demand load for the new tab never fired.
        await Assert.That(vm.Selected).IsEqualTo(2);

        vm.Selected = 0;
        await Assert.That(tabs.SelectedIndex).IsEqualTo(0);
    }

    [Test]
    public async Task DataGrid_TwoWaySelectedIndex_SurvivesArrowKeys()
    {
        var vm = new IndexViewModel();
        var grid = new DataGrid
        {
            ItemsSource = new List<string> { "a", "b", "c" },
        };

        TerminalNinja.Xaml.Binding.BindingOperations.SetBinding(
            grid,
            DataGrid.SelectedIndexProperty,
            new System.Windows.Markup.Binding(nameof(IndexViewModel.Selected))
            {
                Mode = TerminalNinja.Xaml.Binding.BindingMode.TwoWay,
            });

        grid.DataContext = vm;

        grid.OnKeyEvent(Key(ConsoleKey.DownArrow));
        grid.OnKeyEvent(Key(ConsoleKey.DownArrow));

        await Assert.That(vm.Selected).IsEqualTo(2);

        vm.Selected = 1;
        await Assert.That(grid.SelectedIndex).IsEqualTo(1);
    }
}
