using TerminalNinja.Xaml.Binding;
using TerminalNinja.Xaml.Mvvm;
using TerminalNinja.Commands;

namespace TerminalNinja.Tests.Xaml;

/// <summary>
/// Tests for data binding functionality.
/// </summary>
public class BindingTests
{
    internal class TestViewModel : ViewModelBase
    {
        private string _text = "Initial";
        private int _count = 0;
        private ICommand? _command;
        private int _slotSize = 7;

        public string Text
        {
            get => _text;
            set => SetProperty(ref _text, value);
        }

        public int Count
        {
            get => _count;
            set => SetProperty(ref _count, value);
        }

        /// <summary>Integer source for binding to attached properties such as StackPanel.FixedSize.</summary>
        public int SlotSize
        {
            get => _slotSize;
            set => SetProperty(ref _slotSize, value);
        }

        public ICommand TestCommand => _command ??= new RelayCommand(OnCommand);

        private void OnCommand()
        {
            Count++;
            Text = $"Clicked {Count}";
        }
    }
    
    [Test]
    public async Task Binding_OneWay_UpdatesTargetWhenSourceChanges()
    {
        // Arrange
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml"
                   Text="{Binding Path=Text}" />
            """;
        
        var viewModel = new TestViewModel();
        
        // Act
        var label = TerminalXaml.Load<TextBlock>(xaml, viewModel);
        
        // Assert - initial value
        await Assert.That(label.Text).IsEqualTo("Initial");
        
        // Change source
        viewModel.Text = "Updated";
        
        // Assert - target updated
        await Assert.That(label.Text).IsEqualTo("Updated");
    }
    
    [Test]
    public async Task Binding_Command_ExecutesWhenButtonClicked()
    {
        // Arrange
        var xaml = """
            <Button xmlns="http://schemas.terminalninja.dev/xaml"
                    Text="Click Me"
                    Command="{Binding Path=TestCommand}" />
            """;
        
        var viewModel = new TestViewModel();
        
        // Act
        var button = TerminalXaml.Load<Button>(xaml, viewModel);
        
        // Assert - initial state
        await Assert.That(viewModel.Count).IsEqualTo(0);
        
        // Simulate button click by executing the command
        if (button.Command?.CanExecute(null) == true)
        {
            button.Command.Execute(null);
        }
        
        // Assert - command executed
        await Assert.That(viewModel.Count).IsEqualTo(1);
        await Assert.That(viewModel.Text).IsEqualTo("Clicked 1");
    }
    
    [Test]
    public async Task Binding_Command_ProgrammaticBinding_ResolvesOnDataContextSet()
    {
        // Arrange — programmatic binding (no XAML), verifies DependencyProperty.Find
        // correctly triggers static constructors for base types (beforefieldinit).
        var viewModel = new TestViewModel();
        var button = new Button { Text = "Click Me" };
        
        var dp = DependencyProperty.Find(typeof(Button), "Command");
        await Assert.That(dp).IsNotNull();
        
        // Set binding programmatically
        var binding = new System.Windows.Markup.Binding("TestCommand");
        BindingOperations.SetBinding(button, dp!, binding);
        
        // Verify expression is attached but dormant (no DataContext yet)
        await Assert.That(BindingOperations.IsDataBound(button, dp!)).IsTrue();
        await Assert.That(button.Command).IsNull();
        
        // Set DataContext — triggers binding resolution
        button.DataContext = viewModel;
        
        // Command should now be resolved
        await Assert.That(button.Command).IsNotNull();
        await Assert.That(button.Command!.CanExecute(null)).IsTrue();
    }

    [Test]
    public async Task Binding_MultipleProperties_AllUpdate()
    {
        // Arrange
        var xaml = """
            <StackPanel xmlns="http://schemas.terminalninja.dev/xaml">
                <TextBlock Text="{Binding Path=Text}" />
                <Button Text="Test" Command="{Binding Path=TestCommand}" />
            </StackPanel>
            """;
        
        var viewModel = new TestViewModel();
        
        // Act
        var stackPanel = TerminalXaml.Load<StackPanel>(xaml, viewModel);
        var label = (TextBlock)stackPanel.Children[0];
        var button = (Button)stackPanel.Children[1];
        
        // Assert - initial state
        await Assert.That(label.Text).IsEqualTo("Initial");
        await Assert.That(viewModel.Count).IsEqualTo(0);
        
        // Execute command via button
        if (button.Command?.CanExecute(null) == true)
        {
            button.Command.Execute(null);
        }
        
        // Assert - both properties updated
        await Assert.That(viewModel.Count).IsEqualTo(1);
        await Assert.That(viewModel.Text).IsEqualTo("Clicked 1");
        await Assert.That(label.Text).IsEqualTo("Clicked 1");
    }
    
    [Test]
    public async Task BindingManager_SetDataContext_UpdatesBindings()
    {
        // Arrange
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml"
                   Text="{Binding Path=Text}" />
            """;
        
        var viewModel1 = new TestViewModel { Text = "VM1" };
        var viewModel2 = new TestViewModel { Text = "VM2" };
        
        // Act
        var label = TerminalXaml.Load<TextBlock>(xaml, viewModel1);
        
        // Assert - bound to first VM
        await Assert.That(label.Text).IsEqualTo("VM1");
        
        // Change DataContext — triggers OnDataContextChanged → InvalidateDataContextBindings
        label.DataContext = viewModel2;

        // Assert - bound to second VM
        await Assert.That(label.Text).IsEqualTo("VM2");
    }

    [Test]
    public async Task Binding_AttachedProperty_OneWay_UpdatesTargetWhenSourceChanges()
    {
        // Regression: the XAML loader used to push attached-property values straight into
        // ConvertValue, which threw FormatException when the value was a markup extension
        // like "{Binding ...}". Attached-property bindings must route through the same
        // pending-binding pipeline as regular DPs, but with the owner type carried so the
        // DP lookup walks the StackPanel hierarchy rather than the target's own hierarchy.
        var xaml = """
            <StackPanel xmlns="http://schemas.terminalninja.dev/xaml" Orientation="Horizontal">
                <Border StackPanel.SizeMode="Fixed"
                        StackPanel.FixedSize="{Binding SlotSize}" />
            </StackPanel>
            """;

        var viewModel = new TestViewModel();

        var stack = TerminalXaml.Load<StackPanel>(xaml, viewModel);
        var child = stack.Children[0];

        // Initial value flowed through the binding.
        await Assert.That(StackPanel.GetFixedSize(child)).IsEqualTo(7);

        // Source change propagates.
        viewModel.SlotSize = 42;
        await Assert.That(StackPanel.GetFixedSize(child)).IsEqualTo(42);

        // And collapsing to zero still works — used by the toggleable-panels pattern.
        viewModel.SlotSize = 0;
        await Assert.That(StackPanel.GetFixedSize(child)).IsEqualTo(0);
    }
}
