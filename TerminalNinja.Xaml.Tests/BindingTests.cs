using TerminalNinja.Core.Elements;
using TerminalNinja.Xaml;
using TerminalNinja.Xaml.Binding;
using TerminalNinja.Xaml.Mvvm;
using TerminalNinja.Core.Commands;

namespace TerminalNinja.Xaml.Tests;

/// <summary>
/// Tests for data binding functionality.
/// </summary>
public class BindingTests
{
    private class TestViewModel : ViewModelBase
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
            <Label xmlns="clr-namespace:TerminalNinja.Core.Elements;assembly=TerminalNinja.Core"
                   xmlns:b="clr-namespace:TerminalNinja.Xaml.Markup;assembly=TerminalNinja.Xaml"
                   Text="{b:Binding Path=Text}" />
            """;
        
        var viewModel = new TestViewModel();
        var bindingManager = new BindingManager();
        
        // Act
        var label = TerminalXaml.Load<Label>(xaml, viewModel, bindingManager);
        
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
            <Button xmlns="clr-namespace:TerminalNinja.Core.Elements;assembly=TerminalNinja.Core"
                    xmlns:b="clr-namespace:TerminalNinja.Xaml.Markup;assembly=TerminalNinja.Xaml"
                    Text="Click Me"
                    Command="{b:Binding Path=TestCommand}" />
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
            <Stack xmlns="clr-namespace:TerminalNinja.Core.Elements;assembly=TerminalNinja.Core"
                   xmlns:b="clr-namespace:TerminalNinja.Xaml.Markup;assembly=TerminalNinja.Xaml">
                <Label Text="{b:Binding Path=Text}" />
                <Button Text="Test" Command="{b:Binding Path=TestCommand}" />
            </Stack>
            """;
        
        var viewModel = new TestViewModel();
        var bindingManager = new BindingManager();
        
        // Act
        var stack = TerminalXaml.Load<Stack>(xaml, viewModel, bindingManager);
        var label = (Label)stack.Children[0].Element;
        var button = (Button)stack.Children[1].Element;
        
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
            <Label xmlns="clr-namespace:TerminalNinja.Core.Elements;assembly=TerminalNinja.Core"
                   xmlns:b="clr-namespace:TerminalNinja.Xaml.Markup;assembly=TerminalNinja.Xaml"
                   Text="{b:Binding Path=Text}" />
            """;
        
        var viewModel1 = new TestViewModel { Text = "VM1" };
        var viewModel2 = new TestViewModel { Text = "VM2" };
        var bindingManager = new BindingManager();
        
        // Act
        var label = TerminalXaml.Load<Label>(xaml, viewModel1, bindingManager);
        
        // Assert - bound to first VM
        await Assert.That(label.Text).IsEqualTo("VM1");
        
        // Change DataContext
        bindingManager.SetDataContext(label, viewModel2);
        
        // Assert - bound to second VM
        await Assert.That(label.Text).IsEqualTo("VM2");
        
        bindingManager.Dispose();
    }
}
