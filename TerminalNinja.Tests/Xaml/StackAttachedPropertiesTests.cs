using TerminalNinja.Primitives;

namespace TerminalNinja.Tests.Xaml;

public class StackAttachedPropertiesTests
{
    [Test]
    public async Task Load_StackWithAttachedProperties_ParsesCorrectly()
    {
        // Arrange
        var xaml = """
            <Stack xmlns="http://schemas.terminalninja.dev/xaml"
                   xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                   xmlns:e="http://schemas.terminalninja.dev/xaml"
                   Orientation="Vertical">
                <Label Text="Auto" e:Stack.SizeMode="Auto" />
                <Button Text="Fixed" e:Stack.SizeMode="Fixed" e:Stack.FixedSize="5" />
                <Rectangle e:Stack.SizeMode="Stretch" />
            </Stack>
            """;
        
        // Act
        var stack = TerminalXaml.Load<Stack>(xaml);
        
        // Assert
        await Assert.That(stack.Children.Count).IsEqualTo(3);
        
        // First child: Auto
        var label = stack.Children[0].Content as Label;
        await Assert.That(label).IsNotNull();
        await Assert.That(stack.Children[0].SizeMode).IsEqualTo(ChildSizeMode.Auto);
        
        // Second child: Fixed with size 5
        var button = stack.Children[1].Content as Button;
        await Assert.That(button).IsNotNull();
        await Assert.That(stack.Children[1].SizeMode).IsEqualTo(ChildSizeMode.Fixed);
        await Assert.That(stack.Children[1].FixedSize).IsEqualTo(5);
        
        // Third child: Stretch
        var rectangle = stack.Children[2].Content as Rectangle;
        await Assert.That(rectangle).IsNotNull();
        await Assert.That(stack.Children[2].SizeMode).IsEqualTo(ChildSizeMode.Stretch);
    }
    
    [Test]
    public async Task Load_HorizontalStack_ParsesOrientation()
    {
        // Arrange
        var xaml = """
            <Stack xmlns="http://schemas.terminalninja.dev/xaml"
                   Orientation="Horizontal">
                <Label Text="Left" />
                <Label Text="Right" />
            </Stack>
            """;
        
        // Act
        var stack = TerminalXaml.Load<Stack>(xaml);
        
        // Assert
        await Assert.That(stack.Orientation).IsEqualTo(StackOrientation.Horizontal);
        await Assert.That(stack.Children.Count).IsEqualTo(2);
    }
    
    [Test]
    public async Task Load_StackWithMixedSizeModes_CreatesCorrectStackChildren()
    {
        // Arrange
        var xaml = """
            <Stack xmlns="http://schemas.terminalninja.dev/xaml"
                   xmlns:e="http://schemas.terminalninja.dev/xaml">
                <Label Text="Header" e:Stack.SizeMode="Auto" />
                <Rectangle e:Stack.SizeMode="Stretch">
                    <Label Text="Content" />
                </Rectangle>
                <Label Text="Footer" e:Stack.SizeMode="Fixed" e:Stack.FixedSize="3" />
            </Stack>
            """;
        
        // Act
        var stack = TerminalXaml.Load<Stack>(xaml);
        
        // Assert
        await Assert.That(stack.Children.Count).IsEqualTo(3);
        
        var label1 = stack.Children[0].Content as Label;
        await Assert.That(label1).IsNotNull();
        await Assert.That(label1!.Text).IsEqualTo("Header");
        await Assert.That(stack.Children[0].SizeMode).IsEqualTo(ChildSizeMode.Auto);
        
        var rect = stack.Children[1].Content as Rectangle;
        await Assert.That(rect).IsNotNull();
        await Assert.That(stack.Children[1].SizeMode).IsEqualTo(ChildSizeMode.Stretch);
        
        var label2 = stack.Children[2].Content as Label;
        await Assert.That(label2).IsNotNull();
        await Assert.That(label2!.Text).IsEqualTo("Footer");
        await Assert.That(stack.Children[2].SizeMode).IsEqualTo(ChildSizeMode.Fixed);
        await Assert.That(stack.Children[2].FixedSize).IsEqualTo(3);
    }
}
