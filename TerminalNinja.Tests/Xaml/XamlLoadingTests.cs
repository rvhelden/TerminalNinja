namespace TerminalNinja.Tests.Xaml;

public class XamlLoadingTests
{
    [Test]
    public async Task Load_SimpleLabel_ParsesSuccessfully()
    {
        // Arrange
        var xaml = """
            <Label xmlns="http://schemas.terminalninja.dev/xaml"
                   Text="Hello World"
                   ForegroundColor="Cyan" />
            """;
        
        // Act
        var label = TerminalXaml.Load<Label>(xaml);
        
        // Assert
        await Assert.That(label).IsNotNull();
        await Assert.That(label.Text).IsEqualTo("Hello World");
        await Assert.That(label.ForegroundColor).IsEqualTo(Color.Cyan);
    }
    
    [Test]
    public async Task Load_SimpleButton_ParsesSuccessfully()
    {
        // Arrange
        var xaml = """
            <Button xmlns="http://schemas.terminalninja.dev/xaml"
                    Text="Click Me"
                    Width="10"
                    Height="3"
                    ForegroundColor="White"
                    BackgroundColor="#1E1E1E" />
            """;
        
        // Act
        var button = TerminalXaml.Load<Button>(xaml);
        
        // Assert
        await Assert.That(button).IsNotNull();
        await Assert.That(button.Text).IsEqualTo("Click Me");
        await Assert.That(button.Width).IsEqualTo(Size.Absolute(10));
        await Assert.That(button.Height).IsEqualTo(Size.Absolute(3));
        await Assert.That(button.ForegroundColor).IsEqualTo(Color.White);
        await Assert.That(button.BackgroundColor.R).IsEqualTo((byte)30);
        await Assert.That(button.BackgroundColor.G).IsEqualTo((byte)30);
        await Assert.That(button.BackgroundColor.B).IsEqualTo((byte)30);
    }
    
    [Test]
    public async Task Load_RectangleWithChild_ParsesSuccessfully()
    {
        // Arrange
        var xaml = """
            <Rectangle xmlns="http://schemas.terminalninja.dev/xaml"
                       Width="50"
                       Height="10"
                       Border="Double"
                       BackgroundColor="Black">
                <Rectangle.Child>
                    <Label Text="Inside Rectangle" ForegroundColor="Green" />
                </Rectangle.Child>
            </Rectangle>
            """;
        
        // Act
        var rectangle = TerminalXaml.Load<Rectangle>(xaml);
        
        // Assert
        await Assert.That(rectangle).IsNotNull();
        await Assert.That(rectangle.Width).IsEqualTo(Size.Absolute(50));
        await Assert.That(rectangle.Height).IsEqualTo(Size.Absolute(10));
        await Assert.That(rectangle.Child).IsNotNull();
        
        var label = rectangle.Child as Label;
        await Assert.That(label).IsNotNull();
        await Assert.That(label!.Text).IsEqualTo("Inside Rectangle");
        await Assert.That(label.ForegroundColor).IsEqualTo(Color.Green);
    }
    
    [Test]
    public async Task FindByName_FindsNamedElement()
    {
        // Arrange
        var xaml = """
            <Rectangle xmlns="http://schemas.terminalninja.dev/xaml"
                       xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                       xmlns:e="http://schemas.terminalninja.dev/xaml">
                <e:Label x:Name="titleLabel" Text="Title" ForegroundColor="Cyan" />
            </Rectangle>
            """;
        
        // Act
        var root = TerminalXaml.Load<Rectangle>(xaml);
        var label = root.FindByName<Label>("titleLabel");
        
        // Assert
        await Assert.That(label).IsNotNull();
        await Assert.That(label!.Text).IsEqualTo("Title");
        await Assert.That(label.ForegroundColor).IsEqualTo(Color.Cyan);
    }
}
