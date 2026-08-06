using System.Collections.ObjectModel;
using System.Windows.Markup;
using TerminalNinja.Xaml.Binding;
using TerminalNinja.Xaml.Mvvm;

namespace TerminalNinja.Tests.Xaml;

/// <summary>
/// Tests that <see cref="Selector.SelectedItem"/> and <see cref="Selector.SelectedIndex"/> bind
/// two-way when the binding's <see cref="BindingMode"/> is left at
/// <see cref="BindingMode.Default"/>, because they are registered with
/// <see cref="FrameworkPropertyMetadata.BindsTwoWayByDefault"/>. This mirrors WPF, where selection
/// is user-editable state, and prevents the "selection snaps back to the top on refresh" bug: if
/// the binding were one-way, a keyboard/mouse selection would never reach the view model and the
/// next source push would overwrite it.
/// </summary>
public class SelectorBindingTests
{
    internal class SelectionViewModel : ViewModelBase
    {
        private string? _selected;
        private int _selectedIndex = -1;

        public ObservableCollection<string> Items { get; } = ["Alpha", "Beta", "Gamma"];

        public string? Selected
        {
            get => _selected;
            set => SetProperty(ref _selected, value);
        }

        public int SelectedIndex
        {
            get => _selectedIndex;
            set => SetProperty(ref _selectedIndex, value);
        }
    }

    private static ListBox BoundListBox(SelectionViewModel vm)
    {
        var listBox = new ListBox { DataContext = vm, ItemsSource = vm.Items };

        // Mode left at Default on purpose — the point of the test is that SelectedItem resolves
        // to TwoWay from its metadata without an explicit Mode=TwoWay.
        BindingOperations.SetBinding(listBox, Selector.SelectedItemProperty, new Binding(nameof(SelectionViewModel.Selected)));
        BindingOperations.SetBinding(listBox, Selector.SelectedIndexProperty, new Binding(nameof(SelectionViewModel.SelectedIndex)));

        return listBox;
    }

    [Test]
    public async Task SelectedItem_DefaultMode_WritesBackToSource()
    {
        var vm = new SelectionViewModel();
        var listBox = BoundListBox(vm);

        // Simulate the user picking a row (the keyboard/mouse path, which syncs via SetValueInternal).
        listBox.NotifyItemClicked("Gamma");

        await Assert.That(vm.Selected).IsEqualTo("Gamma");
        await Assert.That(vm.SelectedIndex).IsEqualTo(2);
    }

    [Test]
    public async Task SelectedItem_DefaultMode_SourceChangeStillFlowsToTarget()
    {
        var vm = new SelectionViewModel();
        var listBox = BoundListBox(vm);

        // Two-way must not cost the one-way direction: a view-model change still updates the control.
        vm.Selected = "Beta";

        await Assert.That(listBox.SelectedItem).IsEqualTo("Beta");
        await Assert.That(listBox.SelectedIndex).IsEqualTo(1);
    }

    [Test]
    public async Task SelectedItem_TwoWay_KeyboardStyleIndexChange_WritesBack()
    {
        // The home screen binds only SelectedItem. Arrow-key navigation runs through
        // MoveSelection, which sets SelectedIndex via the public setter. Verify that path still
        // flows the resulting item back to the source, since that is the actual "selection snaps
        // back on refresh" scenario.
        var vm = new SelectionViewModel();
        var listBox = new ListBox { DataContext = vm, ItemsSource = vm.Items };
        BindingOperations.SetBinding(
            listBox,
            Selector.SelectedItemProperty,
            new Binding(nameof(SelectionViewModel.Selected)) { Mode = BindingMode.TwoWay });

        listBox.SelectedIndex = 2; // what MoveSelection does

        await Assert.That(listBox.SelectedItem).IsEqualTo("Gamma");
        await Assert.That(vm.Selected).IsEqualTo("Gamma");
    }

    internal class Row : ViewModelBase
    {
        private string _summary = "";
        public string Summary { get => _summary; set => SetProperty(ref _summary, value); }
    }

    [Test]
    public async Task Selection_Survives_InPlaceItemMutation()
    {
        // obs's refresh mutates each row's properties in place (raising PropertyChanged) without
        // replacing the collection. Reproduce that to prove selection is not lost on refresh.
        var rows = new ObservableCollection<Row>
        {
            new() { Summary = "dev" },
            new() { Summary = "test" },
            new() { Summary = "prod" },
        };

        var listBox = new ListBox { ItemsSource = rows };
        listBox.SelectedIndex = 2; // user selects "prod"

        // Simulate a refresh: update every row's content in place.
        foreach (var row in rows)
        {
            row.Summary += " (updated)";
        }

        await Assert.That(listBox.SelectedIndex).IsEqualTo(2);
        await Assert.That(listBox.SelectedItem).IsEqualTo(rows[2]);
    }

    [Test]
    public async Task SelectedItem_XamlTwoWay_WritesBack()
    {
        // Exactly the obs home-screen shape: SelectedItem bound TwoWay from XAML.
        const string xaml = """
            <ListBox xmlns='http://schemas.terminalninja.dev/xaml'
                     ItemsSource='{Binding Items}'
                     SelectedItem='{Binding Selected, Mode=TwoWay}' />
            """;
        var vm = new SelectionViewModel();
        var listBox = TerminalXaml.Load<ListBox>(xaml, vm);

        listBox.SelectedIndex = 2; // arrow-key selection

        await Assert.That(listBox.SelectedItem).IsEqualTo("Gamma");
        await Assert.That(vm.Selected).IsEqualTo("Gamma");
    }

    [Test]
    public async Task SelectedItem_XamlDefaultMode_WritesBack()
    {
        // Same, but with no Mode specified — relies on BindsTwoWayByDefault.
        const string xaml = """
            <ListBox xmlns='http://schemas.terminalninja.dev/xaml'
                     ItemsSource='{Binding Items}'
                     SelectedItem='{Binding Selected}' />
            """;
        var vm = new SelectionViewModel();
        var listBox = TerminalXaml.Load<ListBox>(xaml, vm);

        listBox.SelectedIndex = 1;

        await Assert.That(vm.Selected).IsEqualTo("Beta");
    }

    [Test]
    public async Task SelectedItem_ExplicitOneWay_DoesNotWriteBack()
    {
        var vm = new SelectionViewModel { Selected = "Alpha" };
        var listBox = new ListBox { DataContext = vm, ItemsSource = vm.Items };

        // An explicit Mode still wins over the metadata default.
        BindingOperations.SetBinding(
            listBox,
            Selector.SelectedItemProperty,
            new Binding(nameof(SelectionViewModel.Selected)) { Mode = BindingMode.OneWay });

        listBox.NotifyItemClicked("Gamma");

        await Assert.That(vm.Selected).IsEqualTo("Alpha");
    }
}
