using TerminalNinja.Core.Buffers;
using TerminalNinja.Core.Elements;
using TerminalNinja.Core.Primitives;

namespace TerminalNinja.Core.Tests.Integration;

/// <summary>
/// Integration tests for Stack layout container with various child sizing modes.
/// </summary>
public class StackLayoutTests
{
    #region Horizontal Stack Tests
    
    [Test]
    public async Task Horizontal_AllFixed_RendersAtCorrectPositions()
    {
        // Arrange
        var buffer = new CellBuffer(100, 50);
        var parentBounds = new Rect(0, 0, 100, 50);
        
        var stack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children =
            [
                StackChild.Fixed(CreateColoredRect(Color.Red), 10),
                StackChild.Fixed(CreateColoredRect(Color.Green), 20),
                StackChild.Fixed(CreateColoredRect(Color.Blue), 15)
            ]
        };
        
        // Act
        stack.Render(buffer, parentBounds);
        
        // Assert - verify first child (Red) occupies x=0 to x=9
        await Assert.That(buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(buffer.GetCell(9, 0).Background).IsEqualTo(Color.Red);
        
        // Assert - verify second child (Green) occupies x=10 to x=29
        await Assert.That(buffer.GetCell(10, 0).Background).IsEqualTo(Color.Green);
        await Assert.That(buffer.GetCell(29, 0).Background).IsEqualTo(Color.Green);
        
        // Assert - verify third child (Blue) occupies x=30 to x=44
        await Assert.That(buffer.GetCell(30, 0).Background).IsEqualTo(Color.Blue);
        await Assert.That(buffer.GetCell(44, 0).Background).IsEqualTo(Color.Blue);
    }
    
    [Test]
    public async Task Horizontal_AllStretch_DistributesEvenly()
    {
        // Arrange
        var buffer = new CellBuffer(60, 50);
        var parentBounds = new Rect(0, 0, 60, 50);
        
        var stack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children =
            [
                StackChild.Stretch(CreateColoredRect(Color.Red)),
                StackChild.Stretch(CreateColoredRect(Color.Green)),
                StackChild.Stretch(CreateColoredRect(Color.Blue))
            ]
        };
        
        // Act
        stack.Render(buffer, parentBounds);
        
        // Assert - each child should get 20 cells (60 / 3)
        await Assert.That(buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(buffer.GetCell(19, 0).Background).IsEqualTo(Color.Red);
        
        await Assert.That(buffer.GetCell(20, 0).Background).IsEqualTo(Color.Green);
        await Assert.That(buffer.GetCell(39, 0).Background).IsEqualTo(Color.Green);
        
        await Assert.That(buffer.GetCell(40, 0).Background).IsEqualTo(Color.Blue);
        await Assert.That(buffer.GetCell(59, 0).Background).IsEqualTo(Color.Blue);
    }
    
    [Test]
    public async Task Horizontal_FixedAndStretch_StretchFillsRemaining()
    {
        // Arrange
        var buffer = new CellBuffer(100, 50);
        var parentBounds = new Rect(0, 0, 100, 50);
        
        var stack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children =
            [
                StackChild.Fixed(CreateColoredRect(Color.Red), 20),
                StackChild.Stretch(CreateColoredRect(Color.Green)),
                StackChild.Fixed(CreateColoredRect(Color.Blue), 10)
            ]
        };
        
        // Act
        stack.Render(buffer, parentBounds);
        
        // Assert - Fixed(20) + Stretch(70) + Fixed(10) = 100
        await Assert.That(buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(buffer.GetCell(19, 0).Background).IsEqualTo(Color.Red);
        
        await Assert.That(buffer.GetCell(20, 0).Background).IsEqualTo(Color.Green);
        await Assert.That(buffer.GetCell(89, 0).Background).IsEqualTo(Color.Green);
        
        await Assert.That(buffer.GetCell(90, 0).Background).IsEqualTo(Color.Blue);
        await Assert.That(buffer.GetCell(99, 0).Background).IsEqualTo(Color.Blue);
    }
    
    [Test]
    public async Task Horizontal_AutoSizing_UsesPreferredSize()
    {
        // Arrange
        var buffer = new CellBuffer(100, 50);
        var parentBounds = new Rect(0, 0, 100, 50);
        
        var stack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children =
            [
                StackChild.Auto(CreateFixedSizeRect(Color.Red, 15, 10)),
                StackChild.Stretch(CreateColoredRect(Color.Green)),
                StackChild.Auto(CreateFixedSizeRect(Color.Blue, 25, 10))
            ]
        };
        
        // Act
        stack.Render(buffer, parentBounds);
        
        // Assert - Auto(15) + Stretch(60) + Auto(25) = 100
        await Assert.That(buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(buffer.GetCell(14, 0).Background).IsEqualTo(Color.Red);
        
        await Assert.That(buffer.GetCell(15, 0).Background).IsEqualTo(Color.Green);
        await Assert.That(buffer.GetCell(74, 0).Background).IsEqualTo(Color.Green);
        
        await Assert.That(buffer.GetCell(75, 0).Background).IsEqualTo(Color.Blue);
        await Assert.That(buffer.GetCell(99, 0).Background).IsEqualTo(Color.Blue);
    }
    
    [Test]
    public async Task Horizontal_MultipleStretch_DistributesRemainingEvenly()
    {
        // Arrange
        var buffer = new CellBuffer(100, 50);
        var parentBounds = new Rect(0, 0, 100, 50);
        
        var stack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children =
            [
                StackChild.Fixed(CreateColoredRect(Color.Red), 10),
                StackChild.Stretch(CreateColoredRect(Color.Green)),
                StackChild.Stretch(CreateColoredRect(Color.Blue)),
                StackChild.Fixed(CreateColoredRect(Color.Yellow), 10)
            ]
        };
        
        // Act
        stack.Render(buffer, parentBounds);
        
        // Assert - Fixed(10) + Stretch(40) + Stretch(40) + Fixed(10) = 100
        await Assert.That(buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(buffer.GetCell(9, 0).Background).IsEqualTo(Color.Red);
        
        await Assert.That(buffer.GetCell(10, 0).Background).IsEqualTo(Color.Green);
        await Assert.That(buffer.GetCell(49, 0).Background).IsEqualTo(Color.Green);
        
        await Assert.That(buffer.GetCell(50, 0).Background).IsEqualTo(Color.Blue);
        await Assert.That(buffer.GetCell(89, 0).Background).IsEqualTo(Color.Blue);
        
        await Assert.That(buffer.GetCell(90, 0).Background).IsEqualTo(Color.Yellow);
        await Assert.That(buffer.GetCell(99, 0).Background).IsEqualTo(Color.Yellow);
    }
    
    #endregion
    
    #region Vertical Stack Tests
    
    [Test]
    public async Task Vertical_AllFixed_RendersAtCorrectPositions()
    {
        // Arrange
        var buffer = new CellBuffer(50, 100);
        var parentBounds = new Rect(0, 0, 50, 100);
        
        var stack = new Stack
        {
            Orientation = StackOrientation.Vertical,
            Children =
            [
                StackChild.Fixed(CreateColoredRect(Color.Red), 10),
                StackChild.Fixed(CreateColoredRect(Color.Green), 20),
                StackChild.Fixed(CreateColoredRect(Color.Blue), 15)
            ]
        };
        
        // Act
        stack.Render(buffer, parentBounds);
        
        // Assert - verify first child (Red) occupies y=0 to y=9
        await Assert.That(buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(buffer.GetCell(0, 9).Background).IsEqualTo(Color.Red);
        
        // Assert - verify second child (Green) occupies y=10 to y=29
        await Assert.That(buffer.GetCell(0, 10).Background).IsEqualTo(Color.Green);
        await Assert.That(buffer.GetCell(0, 29).Background).IsEqualTo(Color.Green);
        
        // Assert - verify third child (Blue) occupies y=30 to y=44
        await Assert.That(buffer.GetCell(0, 30).Background).IsEqualTo(Color.Blue);
        await Assert.That(buffer.GetCell(0, 44).Background).IsEqualTo(Color.Blue);
    }
    
    [Test]
    public async Task Vertical_AllStretch_DistributesEvenly()
    {
        // Arrange
        var buffer = new CellBuffer(50, 60);
        var parentBounds = new Rect(0, 0, 50, 60);
        
        var stack = new Stack
        {
            Orientation = StackOrientation.Vertical,
            Children =
            [
                StackChild.Stretch(CreateColoredRect(Color.Red)),
                StackChild.Stretch(CreateColoredRect(Color.Green)),
                StackChild.Stretch(CreateColoredRect(Color.Blue))
            ]
        };
        
        // Act
        stack.Render(buffer, parentBounds);
        
        // Assert - each child should get 20 cells (60 / 3)
        await Assert.That(buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(buffer.GetCell(0, 19).Background).IsEqualTo(Color.Red);
        
        await Assert.That(buffer.GetCell(0, 20).Background).IsEqualTo(Color.Green);
        await Assert.That(buffer.GetCell(0, 39).Background).IsEqualTo(Color.Green);
        
        await Assert.That(buffer.GetCell(0, 40).Background).IsEqualTo(Color.Blue);
        await Assert.That(buffer.GetCell(0, 59).Background).IsEqualTo(Color.Blue);
    }
    
    [Test]
    public async Task Vertical_FixedAndStretch_StretchFillsRemaining()
    {
        // Arrange
        var buffer = new CellBuffer(50, 100);
        var parentBounds = new Rect(0, 0, 50, 100);
        
        var stack = new Stack
        {
            Orientation = StackOrientation.Vertical,
            Children =
            [
                StackChild.Fixed(CreateColoredRect(Color.Red), 20),
                StackChild.Stretch(CreateColoredRect(Color.Green)),
                StackChild.Fixed(CreateColoredRect(Color.Blue), 10)
            ]
        };
        
        // Act
        stack.Render(buffer, parentBounds);
        
        // Assert - Fixed(20) + Stretch(70) + Fixed(10) = 100
        await Assert.That(buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(buffer.GetCell(0, 19).Background).IsEqualTo(Color.Red);
        
        await Assert.That(buffer.GetCell(0, 20).Background).IsEqualTo(Color.Green);
        await Assert.That(buffer.GetCell(0, 89).Background).IsEqualTo(Color.Green);
        
        await Assert.That(buffer.GetCell(0, 90).Background).IsEqualTo(Color.Blue);
        await Assert.That(buffer.GetCell(0, 99).Background).IsEqualTo(Color.Blue);
    }
    
    #endregion
    
    #region Edge Cases
    
    [Test]
    public async Task Empty_RendersNothing()
    {
        // Arrange
        var buffer = new CellBuffer(100, 50);
        var parentBounds = new Rect(0, 0, 100, 50);
        
        var stack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children = []
        };
        
        // Act
        stack.Render(buffer, parentBounds);
        
        // Assert - buffer should remain empty (default black background)
        await Assert.That(buffer.GetCell(0, 0).Background).IsEqualTo(Color.Black);
        await Assert.That(buffer.GetCell(50, 25).Background).IsEqualTo(Color.Black);
    }
    
    [Test]
    public async Task SingleChild_Stretch_FillsAll()
    {
        // Arrange
        var buffer = new CellBuffer(100, 50);
        var parentBounds = new Rect(0, 0, 100, 50);
        
        var stack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children =
            [
                StackChild.Stretch(CreateColoredRect(Color.Red))
            ]
        };
        
        // Act
        stack.Render(buffer, parentBounds);
        
        // Assert - child should fill entire width
        await Assert.That(buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(buffer.GetCell(99, 0).Background).IsEqualTo(Color.Red);
    }
    
    [Test]
    public async Task Overflow_ChildrenExceedParent_StillRenders()
    {
        // Arrange
        var buffer = new CellBuffer(50, 50);
        var parentBounds = new Rect(0, 0, 50, 50);
        
        var stack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children =
            [
                StackChild.Fixed(CreateColoredRect(Color.Red), 30),
                StackChild.Fixed(CreateColoredRect(Color.Green), 30),
                StackChild.Fixed(CreateColoredRect(Color.Blue), 30)
            ]
        };
        
        // Act
        stack.Render(buffer, parentBounds);
        
        // Assert - children render even though they exceed parent (90 > 50)
        await Assert.That(buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(buffer.GetCell(29, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(buffer.GetCell(30, 0).Background).IsEqualTo(Color.Green);
        // Note: Blue will be clipped by buffer bounds
    }
    
    #endregion
    
    #region Nested Stacks
    
    [Test]
    public async Task Nested_HorizontalInVertical_RendersCorrectly()
    {
        // Arrange
        var buffer = new CellBuffer(100, 100);
        var parentBounds = new Rect(0, 0, 100, 100);
        
        var innerHorizontalStack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children =
            [
                StackChild.Stretch(CreateColoredRect(Color.Red)),
                StackChild.Stretch(CreateColoredRect(Color.Green))
            ]
        };
        
        var outerVerticalStack = new Stack
        {
            Orientation = StackOrientation.Vertical,
            Children =
            [
                StackChild.Fixed(innerHorizontalStack, 30),
                StackChild.Stretch(CreateColoredRect(Color.Blue))
            ]
        };
        
        // Act
        outerVerticalStack.Render(buffer, parentBounds);
        
        // Assert - top row (y=0-29) should have Red (left half) and Green (right half)
        await Assert.That(buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(buffer.GetCell(49, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(buffer.GetCell(50, 0).Background).IsEqualTo(Color.Green);
        await Assert.That(buffer.GetCell(99, 0).Background).IsEqualTo(Color.Green);
        
        // Assert - bottom section (y=30-99) should be Blue
        await Assert.That(buffer.GetCell(0, 30).Background).IsEqualTo(Color.Blue);
        await Assert.That(buffer.GetCell(99, 99).Background).IsEqualTo(Color.Blue);
    }
    
    [Test]
    public async Task Nested_VerticalInHorizontal_RendersCorrectly()
    {
        // Arrange
        var buffer = new CellBuffer(100, 100);
        var parentBounds = new Rect(0, 0, 100, 100);
        
        var innerVerticalStack = new Stack
        {
            Orientation = StackOrientation.Vertical,
            Children =
            [
                StackChild.Stretch(CreateColoredRect(Color.Red)),
                StackChild.Stretch(CreateColoredRect(Color.Green))
            ]
        };
        
        var outerHorizontalStack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children =
            [
                StackChild.Fixed(innerVerticalStack, 30),
                StackChild.Stretch(CreateColoredRect(Color.Blue))
            ]
        };
        
        // Act
        outerHorizontalStack.Render(buffer, parentBounds);
        
        // Assert - left column (x=0-29) should have Red (top half) and Green (bottom half)
        await Assert.That(buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(buffer.GetCell(0, 49).Background).IsEqualTo(Color.Red);
        await Assert.That(buffer.GetCell(0, 50).Background).IsEqualTo(Color.Green);
        await Assert.That(buffer.GetCell(0, 99).Background).IsEqualTo(Color.Green);
        
        // Assert - right section (x=30-99) should be Blue
        await Assert.That(buffer.GetCell(30, 0).Background).IsEqualTo(Color.Blue);
        await Assert.That(buffer.GetCell(99, 99).Background).IsEqualTo(Color.Blue);
    }
    
    #endregion
    
    #region Helper Methods
    
    /// <summary>
    /// Creates a simple rectangle with a colored background that stretches to fill parent.
    /// </summary>
    private static Rectangle CreateColoredRect(Color bgColor) => new()
    {
        BackgroundColor = bgColor,
        Width = Size.Stretch,
        Height = Size.Stretch
    };
    
    /// <summary>
    /// Creates a rectangle with fixed absolute width and height.
    /// </summary>
    private static Rectangle CreateFixedSizeRect(Color bgColor, int width, int height) => new()
    {
        BackgroundColor = bgColor,
        Width = Size.Absolute(width),
        Height = Size.Absolute(height)
    };
    
    #endregion
}
