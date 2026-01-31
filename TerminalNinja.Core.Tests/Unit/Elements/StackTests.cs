namespace TerminalNinja.Core.Tests.Unit.Elements;

/// <summary>
/// Comprehensive tests for Stack element covering:
/// - GetPreferredSize with different orientations and child size modes
/// - CalculateBounds (always returns parent)
/// - Render with Horizontal and Vertical orientations
/// - Size calculation for Fixed, Stretch, and Auto children
/// - Empty stack handling
/// - Edge cases (zero-size children, no space for stretch)
/// </summary>
public class StackTests
{
    private CellBuffer _buffer = null!;
    private const int BufferWidth = 100;
    private const int BufferHeight = 50;

    [Before(Test)]
    public Task Setup()
    {
        _buffer = new CellBuffer(BufferWidth, BufferHeight);
        return Task.CompletedTask;
    }

    #region GetPreferredSize

    [Test]
    public async Task GetPreferredSize_EmptyStack_ReturnsZeroSize()
    {
        // Arrange
        var stack = new Stack { Children = [] };
        var parent = new Rect(0, 0, 100, 50);

        // Act
        var size = stack.GetPreferredSize(parent);

        // Assert
        await Assert.That(size.Width).IsEqualTo(0);
        await Assert.That(size.Height).IsEqualTo(0);
    }

    [Test]
    public async Task GetPreferredSize_HorizontalStack_SumsWidthsAndMaxHeight()
    {
        // Arrange
        var child1 = new Rectangle { Width = Size.Absolute(10), Height = Size.Absolute(5) };
        var child2 = new Rectangle { Width = Size.Absolute(20), Height = Size.Absolute(8) };
        var stack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children = [
                StackChild.Auto(child1),
                StackChild.Auto(child2)
            ]
        };
        var parent = new Rect(0, 0, 100, 50);

        // Act
        var size = stack.GetPreferredSize(parent);

        // Assert
        await Assert.That(size.Width).IsEqualTo(30); // 10 + 20
        await Assert.That(size.Height).IsEqualTo(8); // max(5, 8)
    }

    [Test]
    public async Task GetPreferredSize_VerticalStack_SumsHeightsAndMaxWidth()
    {
        // Arrange
        var child1 = new Rectangle { Width = Size.Absolute(10), Height = Size.Absolute(5) };
        var child2 = new Rectangle { Width = Size.Absolute(20), Height = Size.Absolute(8) };
        var stack = new Stack
        {
            Orientation = StackOrientation.Vertical,
            Children = [
                StackChild.Auto(child1),
                StackChild.Auto(child2)
            ]
        };
        var parent = new Rect(0, 0, 100, 50);

        // Act
        var size = stack.GetPreferredSize(parent);

        // Assert
        await Assert.That(size.Width).IsEqualTo(20); // max(10, 20)
        await Assert.That(size.Height).IsEqualTo(13); // 5 + 8
    }

    [Test]
    public async Task GetPreferredSize_WithFixedChildren_UsesFixedSizes()
    {
        // Arrange
        var child = new Rectangle { Width = Size.Absolute(100), Height = Size.Absolute(100) };
        var stack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children = [
                StackChild.Fixed(child, 15), // Fixed size overrides element's preferred size
                StackChild.Fixed(child, 25)
            ]
        };
        var parent = new Rect(0, 0, 100, 50);

        // Act
        var size = stack.GetPreferredSize(parent);

        // Assert
        await Assert.That(size.Width).IsEqualTo(40); // 15 + 25
    }

    #endregion

    #region CalculateBounds

    [Test]
    public async Task CalculateBounds_AlwaysReturnsParent()
    {
        // Arrange
        var stack = new Stack
        {
            Children = [
                StackChild.Auto(new Rectangle())
            ]
        };
        var parent = new Rect(10, 20, 80, 30);

        // Act
        var bounds = stack.CalculateBounds(parent);

        // Assert
        await Assert.That(bounds).IsEqualTo(parent);
    }

    #endregion

    #region Render - Horizontal

    [Test]
    public async Task Render_HorizontalStack_PositionsChildrenLeftToRight()
    {
        // Arrange
        var stack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children = [
                StackChild.Fixed(new Rectangle { BackgroundColor = Color.Red }, 10),
                StackChild.Fixed(new Rectangle { BackgroundColor = Color.Green }, 15)
            ]
        };
        var bounds = new Rect(0, 0, 100, 50);

        // Act
        stack.Render(_buffer, bounds);

        // Assert - Check that first child is rendered at x=0, second at x=10
        var cell1 = _buffer.GetCell(0, 0);
        var cell2 = _buffer.GetCell(10, 0);
        
        await Assert.That(cell1.Background).IsEqualTo(Color.Red);
        await Assert.That(cell2.Background).IsEqualTo(Color.Green);
    }

    [Test]
    public async Task Render_HorizontalStackWithFixedChildren_CalculatesCorrectSizes()
    {
        // Arrange
        var stack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children = [
                StackChild.Fixed(new Rectangle { BackgroundColor = Color.Red }, 20),
                StackChild.Fixed(new Rectangle { BackgroundColor = Color.Green }, 30)
            ]
        };
        var bounds = new Rect(0, 0, 100, 10);

        // Act
        stack.Render(_buffer, bounds);

        // Assert - Verify first child occupies cells 0-19, second occupies 20-49
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(19, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(20, 0).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(49, 0).Background).IsEqualTo(Color.Green);
    }

    [Test]
    public async Task Render_HorizontalStackWithStretchChildren_DistributesSpace()
    {
        // Arrange
        var stack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children = [
                StackChild.Stretch(new Rectangle { BackgroundColor = Color.Red }),
                StackChild.Stretch(new Rectangle { BackgroundColor = Color.Green })
            ]
        };
        var bounds = new Rect(0, 0, 100, 10);

        // Act
        stack.Render(_buffer, bounds);

        // Assert - Each stretch child gets 50 cells (100 / 2)
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(49, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(50, 0).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(99, 0).Background).IsEqualTo(Color.Green);
    }

    [Test]
    public async Task Render_HorizontalStackWithMixedChildren_CalculatesCorrectly()
    {
        // Arrange
        var stack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children = [
                StackChild.Fixed(new Rectangle { BackgroundColor = Color.Red }, 20),
                StackChild.Stretch(new Rectangle { BackgroundColor = Color.Green }),
                StackChild.Fixed(new Rectangle { BackgroundColor = Color.Blue }, 10)
            ]
        };
        var bounds = new Rect(0, 0, 100, 10);

        // Act
        stack.Render(_buffer, bounds);

        // Assert
        // Fixed: 20 + 10 = 30, Remaining: 70, Stretch gets 70
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);   // 0-19 Red
        await Assert.That(_buffer.GetCell(19, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(20, 0).Background).IsEqualTo(Color.Green); // 20-89 Green
        await Assert.That(_buffer.GetCell(89, 0).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(90, 0).Background).IsEqualTo(Color.Blue);  // 90-99 Blue
        await Assert.That(_buffer.GetCell(99, 0).Background).IsEqualTo(Color.Blue);
    }

    #endregion

    #region Render - Vertical

    [Test]
    public async Task Render_VerticalStack_PositionsChildrenTopToBottom()
    {
        // Arrange
        var stack = new Stack
        {
            Orientation = StackOrientation.Vertical,
            Children = [
                StackChild.Fixed(new Rectangle { BackgroundColor = Color.Red }, 5),
                StackChild.Fixed(new Rectangle { BackgroundColor = Color.Green }, 10)
            ]
        };
        var bounds = new Rect(0, 0, 100, 50);

        // Act
        stack.Render(_buffer, bounds);

        // Assert - Check that first child is rendered at y=0, second at y=5
        var cell1 = _buffer.GetCell(0, 0);
        var cell2 = _buffer.GetCell(0, 5);
        
        await Assert.That(cell1.Background).IsEqualTo(Color.Red);
        await Assert.That(cell2.Background).IsEqualTo(Color.Green);
    }

    [Test]
    public async Task Render_VerticalStackWithStretchChildren_DistributesSpace()
    {
        // Arrange
        var stack = new Stack
        {
            Orientation = StackOrientation.Vertical,
            Children = [
                StackChild.Stretch(new Rectangle { BackgroundColor = Color.Red }),
                StackChild.Stretch(new Rectangle { BackgroundColor = Color.Green })
            ]
        };
        var bounds = new Rect(0, 0, 100, 50);

        // Act
        stack.Render(_buffer, bounds);

        // Assert - Each stretch child gets 25 rows (50 / 2)
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(0, 24).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(0, 25).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(0, 49).Background).IsEqualTo(Color.Green);
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task Render_EmptyStack_DoesNotCrash()
    {
        // Arrange
        var stack = new Stack { Children = [] };
        var bounds = new Rect(0, 0, 100, 50);

        // Act
        stack.Render(_buffer, bounds);

        // Assert - Should not throw, buffer should remain empty (all cells black)
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Black);
    }

    [Test]
    public async Task Render_ChildWithZeroSize_IsSkipped()
    {
        // Arrange
        var stack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children = [
                StackChild.Fixed(new Rectangle { BackgroundColor = Color.Red }, 0),
                StackChild.Fixed(new Rectangle { BackgroundColor = Color.Green }, 10)
            ]
        };
        var bounds = new Rect(0, 0, 100, 10);

        // Act
        stack.Render(_buffer, bounds);

        // Assert - Green should start at x=0 since red has zero size
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Green);
    }

    [Test]
    public async Task Render_StretchWithNoSpace_GetsZeroSize()
    {
        // Arrange
        var stack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children = [
                StackChild.Fixed(new Rectangle { BackgroundColor = Color.Red }, 100), // Uses all space
                StackChild.Stretch(new Rectangle { BackgroundColor = Color.Green })   // Gets nothing
            ]
        };
        var bounds = new Rect(0, 0, 100, 10);

        // Act
        stack.Render(_buffer, bounds);

        // Assert - Only red should be visible
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(99, 0).Background).IsEqualTo(Color.Red);
    }

    [Test]
    public async Task Render_AutoChild_UsesElementPreferredSize()
    {
        // Arrange
        var child = new Rectangle 
        { 
            Width = Size.Absolute(25), 
            Height = Size.Absolute(10),
            BackgroundColor = Color.Cyan 
        };
        var stack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children = [
                StackChild.Auto(child),
                StackChild.Fixed(new Rectangle { BackgroundColor = Color.Magenta }, 15)
            ]
        };
        var bounds = new Rect(0, 0, 100, 10);

        // Act
        stack.Render(_buffer, bounds);

        // Assert - Auto child should use its preferred width of 25
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Cyan);
        await Assert.That(_buffer.GetCell(24, 0).Background).IsEqualTo(Color.Cyan);
        await Assert.That(_buffer.GetCell(25, 0).Background).IsEqualTo(Color.Magenta);
        await Assert.That(_buffer.GetCell(39, 0).Background).IsEqualTo(Color.Magenta);
    }

    [Test]
    public async Task Render_MultipleStretchChildren_DivideSpaceEqually()
    {
        // Arrange
        var stack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children = [
                StackChild.Stretch(new Rectangle { BackgroundColor = Color.Red }),
                StackChild.Stretch(new Rectangle { BackgroundColor = Color.Green }),
                StackChild.Stretch(new Rectangle { BackgroundColor = Color.Blue })
            ]
        };
        var bounds = new Rect(0, 0, 90, 10); // 90 / 3 = 30 each

        // Act
        stack.Render(_buffer, bounds);

        // Assert
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);    // 0-29
        await Assert.That(_buffer.GetCell(29, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(30, 0).Background).IsEqualTo(Color.Green);  // 30-59
        await Assert.That(_buffer.GetCell(59, 0).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(60, 0).Background).IsEqualTo(Color.Blue);   // 60-89
        await Assert.That(_buffer.GetCell(89, 0).Background).IsEqualTo(Color.Blue);
    }

    #endregion
}
