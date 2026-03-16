namespace TerminalNinja.Tests.Xaml;

public class XamlLoadingTests
{
    [Test]
    public async Task Load_SimpleTextBlock_ParsesSuccessfully()
    {
        // Arrange
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml"
                   Text="Hello World"
                   Foreground="Cyan" />
            """;
        
        // Act
        var label = TerminalXaml.Load<TextBlock>(xaml);
        
        // Assert
        await Assert.That(label).IsNotNull();
        await Assert.That(label.Text).IsEqualTo("Hello World");
        await Assert.That(label.Foreground).IsEqualTo(Color.Cyan);
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
                    Foreground="White"
                    Background="#1E1E1E" />
            """;
        
        // Act
        var button = TerminalXaml.Load<Button>(xaml);
        
        // Assert
        await Assert.That(button).IsNotNull();
        await Assert.That(button.Text).IsEqualTo("Click Me");
        await Assert.That(button.Width).IsEqualTo(Size.Absolute(10));
        await Assert.That(button.Height).IsEqualTo(Size.Absolute(3));
        await Assert.That(button.Foreground).IsEqualTo(Color.White);
        await Assert.That(button.Background.R).IsEqualTo((byte)30);
        await Assert.That(button.Background.G).IsEqualTo((byte)30);
        await Assert.That(button.Background.B).IsEqualTo((byte)30);
    }
    
    [Test]
    public async Task Load_BorderWithChild_ParsesSuccessfully()
    {
        // Arrange
        var xaml = """
            <Border xmlns="http://schemas.terminalninja.dev/xaml"
                       Width="50"
                       Height="10"
                       BorderStyle="Double"
                       Background="Black">
                <Border.Child>
                    <TextBlock Text="Inside Border" Foreground="Green" />
                </Border.Child>
            </Border>
            """;
        
        // Act
        var rectangle = TerminalXaml.Load<global::TerminalNinja.Controls.Border>(xaml);
        
        // Assert
        await Assert.That(rectangle).IsNotNull();
        await Assert.That(rectangle.Width).IsEqualTo(Size.Absolute(50));
        await Assert.That(rectangle.Height).IsEqualTo(Size.Absolute(10));
        await Assert.That(rectangle.Child).IsNotNull();
        
        var label = rectangle.Child as TextBlock;
        await Assert.That(label).IsNotNull();
        await Assert.That(label!.Text).IsEqualTo("Inside Border");
        await Assert.That(label.Foreground).IsEqualTo(Color.Green);
    }
    
    [Test]
    public async Task FindByName_FindsNamedElement()
    {
        // Arrange
        var xaml = """
            <Border xmlns="http://schemas.terminalninja.dev/xaml"
                       xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                       xmlns:e="http://schemas.terminalninja.dev/xaml">
                <e:TextBlock x:Name="titleTextBlock" Text="Title" Foreground="Cyan" />
            </Border>
            """;
        
        // Act
        var root = TerminalXaml.Load<global::TerminalNinja.Controls.Border>(xaml);
        var label = root.FindByName<TextBlock>("titleTextBlock");
        
        // Assert
        await Assert.That(label).IsNotNull();
        await Assert.That(label!.Text).IsEqualTo("Title");
        await Assert.That(label.Foreground).IsEqualTo(Color.Cyan);
    }
}
