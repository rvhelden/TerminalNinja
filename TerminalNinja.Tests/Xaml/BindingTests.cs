using TerminalNinja.Controls;
using TerminalNinja.Xaml;
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
        var bindingManager = new BindingManager();
        
        // Act
        var label = TerminalXaml.Load<TextBlock>(xaml, viewModel, bindingManager);
        
        // Assert - initial value
        await Assert.That(label.Text).IsEqualTo("Initial");
        
        // Change source
        viewModel.Text = "Updated";
        
        // Assert - target updated
        await Assert.That(label.Text).IsEqualTo("Updated");
        
        bindingManager.Dispose();
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
        var bindingManager = new BindingManager();
        
        // Act
        var button = TerminalXaml.Load<Button>(xaml, viewModel, bindingManager);
        
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
        
        bindingManager.Dispose();
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
        var bindingManager = new BindingManager();
        
        // Act
        var stackPanel = TerminalXaml.Load<StackPanel>(xaml, viewModel, bindingManager);
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
        
        bindingManager.Dispose();
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
        var bindingManager = new BindingManager();
        
        // Act
        var label = TerminalXaml.Load<TextBlock>(xaml, viewModel1, bindingManager);
        
        // Assert - bound to first VM
        await Assert.That(label.Text).IsEqualTo("VM1");
        
        // Change DataContext
        bindingManager.SetDataContext(label, viewModel2);
        
        // Assert - bound to second VM
        await Assert.That(label.Text).IsEqualTo("VM2");
        
        bindingManager.Dispose();
    }
}
