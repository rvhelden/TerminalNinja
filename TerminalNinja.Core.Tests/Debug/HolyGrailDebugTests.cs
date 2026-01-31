using TerminalNinja.Core.Buffers;
using TerminalNinja.Core.Elements;
using TerminalNinja.Core.Primitives;

namespace TerminalNinja.Core.Tests.Debug;

public class HolyGrailDebugTests
{
    [Test]
    public async Task Debug_HolyGrail_ShouldRenderCorrectly()
    {
        // Arrange - Create a smaller version (40x20)
        var buffer = new CellBuffer(40, 20);
        var parentBounds = new Rect(0, 0, 40, 20);
        
        var header = new Rectangle
        {
            BackgroundColor = Color.Red,
            Border = Border.Single(Color.Red)
        };
        
        var leftSidebar = new Rectangle
        {
            BackgroundColor = Color.Green,
            Border = Border.Single(Color.Green)
        };
        
        var mainContent = new Rectangle
        {
            BackgroundColor = Color.Blue,
            Border = Border.Single(Color.Blue)
        };
        
        var rightSidebar = new Rectangle
        {
            BackgroundColor = Color.Yellow,
            Border = Border.Single(Color.Yellow)
        };
        
        var footer = new Rectangle
        {
            BackgroundColor = Color.Magenta,
            Border = Border.Single(Color.Magenta)
        };
        
        var middleRow = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children =
            [
                StackChild.Fixed(leftSidebar, 8),
                StackChild.Stretch(mainContent),
                StackChild.Fixed(rightSidebar, 8)
            ]
        };
        
        var layout = new Stack
        {
            Orientation = StackOrientation.Vertical,
            Children =
            [
                StackChild.Fixed(header, 3),
                StackChild.Stretch(middleRow),
                StackChild.Fixed(footer, 2)
            ]
        };
        
        // Act
        layout.Render(buffer, parentBounds);
        
        // Assert - Header (rows 0-2)
        await Assert.That(buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(buffer.GetCell(20, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(buffer.GetCell(20, 2).Background).IsEqualTo(Color.Red);
        
        // Assert - Left sidebar (rows 3-17, cols 0-7)
        await Assert.That(buffer.GetCell(0, 3).Background).IsEqualTo(Color.Green);
        await Assert.That(buffer.GetCell(7, 10).Background).IsEqualTo(Color.Green);
        
        // Assert - Main content (rows 3-17, cols 8-31)
        await Assert.That(buffer.GetCell(8, 3).Background).IsEqualTo(Color.Blue);
        await Assert.That(buffer.GetCell(20, 10).Background).IsEqualTo(Color.Blue);
        await Assert.That(buffer.GetCell(31, 10).Background).IsEqualTo(Color.Blue);
        
        // Assert - Right sidebar (rows 3-17, cols 32-39)
        await Assert.That(buffer.GetCell(32, 10).Background).IsEqualTo(Color.Yellow);
        await Assert.That(buffer.GetCell(39, 10).Background).IsEqualTo(Color.Yellow);
        
        // Assert - Footer (rows 18-19)
        await Assert.That(buffer.GetCell(0, 18).Background).IsEqualTo(Color.Magenta);
        await Assert.That(buffer.GetCell(20, 18).Background).IsEqualTo(Color.Magenta);
    }
}
