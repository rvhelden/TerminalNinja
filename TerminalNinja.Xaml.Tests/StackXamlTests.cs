namespace TerminalNinja.Xaml.Tests;

public class StackXamlTests
{
    [Test]
    public async Task Load_StackWithChildren_ParsesSuccessfully()
    {
        // Arrange
        var xaml = """
            <Stack xmlns="clr-namespace:TerminalNinja.Core.Elements;assembly=TerminalNinja.Core"
                   xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                   Orientation="Vertical">
                <Label Text="First" />
                <Label Text="Second" />
            </Stack>
            """;
        
        // Act & Assert - This will likely fail because XAML can't add IElement to List<StackChild>
        var stack = TerminalXaml.Load<Stack>(xaml);
        
        await Assert.That(stack).IsNotNull();
        await Assert.That(stack.Children.Count).IsEqualTo(2);
    }
}
