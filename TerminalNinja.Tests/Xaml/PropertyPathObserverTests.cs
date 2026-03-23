using System.Windows.Markup;
using TerminalNinja.Xaml.Binding;
using TerminalNinja.Xaml.Mvvm;

namespace TerminalNinja.Tests.Xaml;

/// <summary>
/// Tests for PropertyPathObserver via the public binding API.
/// PropertyPathObserver is internal, so we exercise it through BindingOperations
/// which creates PropertyPathObserver instances internally for each binding.
/// </summary>
public class PropertyPathObserverTests
{
    // --- View models for testing multi-level property paths ---

    internal class InnerViewModel : ViewModelBase
    {
        private string _name = "Initial";
        private int _value;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public int Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
    }

    internal class OuterViewModel : ViewModelBase
    {
        private InnerViewModel _inner = new();
        private string _unrelated = "Unrelated";

        public InnerViewModel Inner
        {
            get => _inner;
            set => SetProperty(ref _inner, value);
        }

        public string Unrelated
        {
            get => _unrelated;
            set => SetProperty(ref _unrelated, value);
        }
    }

    // --- Tests: Leaf property change ---

    [Test]
    public async Task LeafPropertyChange_UpdatesTarget()
    {
        // Arrange — bind TextBlock.Text to "Inner.Name" (two-segment path)
        var vm = new OuterViewModel();
        var textBlock = new TextBlock();
        textBlock.DataContext = vm;

        var binding = new Binding("Inner.Name") { Mode = BindingMode.OneWay };
        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty, binding);

        // Assert — initial value propagated
        await Assert.That(textBlock.Text).IsEqualTo("Initial");

        // Act — change the leaf property
        vm.Inner.Name = "Updated";

        // Assert — target updated
        await Assert.That(textBlock.Text).IsEqualTo("Updated");
    }

    // --- Tests: Intermediate property change (resubscribe) ---

    [Test]
    public async Task IntermediatePropertyChange_Resubscribes_AndUpdatesTarget()
    {
        // Arrange — bind TextBlock.Text to "Inner.Name"
        var vm = new OuterViewModel();
        var textBlock = new TextBlock();
        textBlock.DataContext = vm;

        var binding = new Binding("Inner.Name") { Mode = BindingMode.OneWay };
        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty, binding);

        await Assert.That(textBlock.Text).IsEqualTo("Initial");

        // Act — replace the intermediate object entirely
        var newInner = new InnerViewModel { Name = "NewInner" };
        vm.Inner = newInner;

        // Assert — target updated from the new inner object
        await Assert.That(textBlock.Text).IsEqualTo("NewInner");

        // Act — change a property on the NEW inner object
        // This verifies that resubscription happened correctly
        newInner.Name = "AfterResubscribe";

        // Assert — target updated from the new subscription
        await Assert.That(textBlock.Text).IsEqualTo("AfterResubscribe");
    }

    // --- Tests: Unrelated property change is ignored ---

    [Test]
    public async Task UnrelatedPropertyChange_DoesNotUpdateTarget()
    {
        // Arrange — bind TextBlock.Text to "Inner.Name"
        var vm = new OuterViewModel();
        var textBlock = new TextBlock();
        textBlock.DataContext = vm;

        var binding = new Binding("Inner.Name") { Mode = BindingMode.OneWay };
        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty, binding);

        await Assert.That(textBlock.Text).IsEqualTo("Initial");

        // Act — change an unrelated property on the outer VM
        vm.Unrelated = "Changed";

        // Assert — target unchanged (binding only watches "Inner" and "Name")
        await Assert.That(textBlock.Text).IsEqualTo("Initial");
    }

    // --- Tests: Single-segment path (simple binding) ---

    [Test]
    public async Task SingleSegmentPath_LeafChange_UpdatesTarget()
    {
        // Arrange — bind TextBlock.Text to "Unrelated" (single-segment path)
        var vm = new OuterViewModel();
        var textBlock = new TextBlock();
        textBlock.DataContext = vm;

        var binding = new Binding("Unrelated") { Mode = BindingMode.OneWay };
        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty, binding);

        await Assert.That(textBlock.Text).IsEqualTo("Unrelated");

        // Act — change the leaf (and only) property
        vm.Unrelated = "NewValue";

        // Assert — target updated
        await Assert.That(textBlock.Text).IsEqualTo("NewValue");
    }

    // --- Tests: Old intermediate no longer triggers update ---

    [Test]
    public async Task OldIntermediateObject_DoesNotTriggerUpdate()
    {
        // Arrange — bind TextBlock.Text to "Inner.Name"
        var vm = new OuterViewModel();
        var oldInner = vm.Inner;
        var textBlock = new TextBlock();
        textBlock.DataContext = vm;

        var binding = new Binding("Inner.Name") { Mode = BindingMode.OneWay };
        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty, binding);

        await Assert.That(textBlock.Text).IsEqualTo("Initial");

        // Act — replace intermediate, then change property on the OLD inner
        vm.Inner = new InnerViewModel { Name = "NewInner" };
        await Assert.That(textBlock.Text).IsEqualTo("NewInner");

        oldInner.Name = "ShouldBeIgnored";

        // Assert — target still shows value from the new inner, not the old one
        await Assert.That(textBlock.Text).IsEqualTo("NewInner");
    }
}
