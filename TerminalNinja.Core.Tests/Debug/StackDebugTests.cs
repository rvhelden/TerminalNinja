using TerminalNinja.Core.Buffers;
using TerminalNinja.Core.Elements;
using TerminalNinja.Core.Primitives;

namespace TerminalNinja.Core.Tests.Debug;

/// <summary>
/// Debug tests to understand Stack layout issues.
/// </summary>
public class StackDebugTests
{
    [Test]
    public async Task Debug_VerticalStack_ThreeFixed_ShouldNotOverlap()
    {
        // Arrange
        var buffer = new CellBuffer(80, 30);
        var parentBounds = new Rect(0, 0, 80, 30);
        
        var header = new Rectangle
        {
            BackgroundColor = Color.Red,
            Border = Border.Single(Color.Red)
        };
        
        var middle = new Rectangle
        {
            BackgroundColor = Color.Green,
            Border = Border.Single(Color.Green)
        };
        
        var footer = new Rectangle
        {
            BackgroundColor = Color.Blue,
            Border = Border.Single(Color.Blue)
        };
        
        var stack = new Stack
        {
            Orientation = StackOrientation.Vertical,
            Children =
            [
                StackChild.Fixed(header, 5),
                StackChild.Fixed(middle, 20),
                StackChild.Fixed(footer, 5)
            ]
        };
        
        // Act
        stack.Render(buffer, parentBounds);
        
        // Assert - Header should occupy rows 0-4
        await Assert.That(buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(buffer.GetCell(0, 4).Background).IsEqualTo(Color.Red);
        
        // Assert - Middle should occupy rows 5-24
        await Assert.That(buffer.GetCell(0, 5).Background).IsEqualTo(Color.Green);
        await Assert.That(buffer.GetCell(0, 24).Background).IsEqualTo(Color.Green);
        
        // Assert - Footer should occupy rows 25-29
        await Assert.That(buffer.GetCell(0, 25).Background).IsEqualTo(Color.Blue);
        await Assert.That(buffer.GetCell(0, 29).Background).IsEqualTo(Color.Blue);
        
        // Assert - Check boundaries don't overlap
        await Assert.That(buffer.GetCell(0, 4).Background).IsEqualTo(Color.Red);
        await Assert.That(buffer.GetCell(0, 5).Background).IsEqualTo(Color.Green);
        await Assert.That(buffer.GetCell(0, 24).Background).IsEqualTo(Color.Green);
        await Assert.That(buffer.GetCell(0, 25).Background).IsEqualTo(Color.Blue);
    }
}
