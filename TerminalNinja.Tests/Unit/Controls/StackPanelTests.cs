namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Tests for StackPanel layout container covering:
/// - Attached properties (SizeMode, FixedSize)
/// - Orientation (Horizontal, Vertical)
/// - Child sizing modes (Fixed, Auto, Stretch)
/// - Layout calculation and rendering
/// - Edge cases (empty stack, zero sizes, overflow)
/// </summary>
/// <remarks>
/// NotInParallel is required because StackPanel uses AttachablePropertyServices which
/// uses a static Dictionary that is not thread-safe for concurrent modifications.
/// </remarks>
[NotInParallel]
public class StackPanelTests
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

    #region Attached Properties - SizeMode

    [Test]
    public async Task GetSizeMode_DefaultValue_ReturnsAuto()
    {
        // Arrange
        var control = new Rectangle();

        // Act
        var sizeMode = StackPanel.GetSizeMode(control);

        // Assert
        await Assert.That(sizeMode).IsEqualTo(ChildSizeMode.Auto);
    }

    [Test]
    public async Task SetSizeMode_Fixed_SetsSizeMode()
    {
        // Arrange
        var control = new Rectangle();

        // Act
        StackPanel.SetSizeMode(control, ChildSizeMode.Fixed);

        // Assert
        await Assert.That(StackPanel.GetSizeMode(control)).IsEqualTo(ChildSizeMode.Fixed);
    }

    [Test]
    public async Task SetSizeMode_Stretch_SetsSizeMode()
    {
        // Arrange
        var control = new Rectangle();

        // Act
        StackPanel.SetSizeMode(control, ChildSizeMode.Stretch);

        // Assert
        await Assert.That(StackPanel.GetSizeMode(control)).IsEqualTo(ChildSizeMode.Stretch);
    }

    [Test]
    public async Task GetSizeMode_NullControl_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => StackPanel.GetSizeMode(null!))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task SetSizeMode_NullControl_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => StackPanel.SetSizeMode(null!, ChildSizeMode.Fixed))
            .ThrowsExactly<ArgumentNullException>();
    }

    #endregion

    #region Attached Properties - FixedSize

    [Test]
    public async Task GetFixedSize_DefaultValue_ReturnsZero()
    {
        // Arrange
        var control = new Rectangle();

        // Act
        var fixedSize = StackPanel.GetFixedSize(control);

        // Assert
        await Assert.That(fixedSize).IsEqualTo(0);
    }

    [Test]
    public async Task SetFixedSize_ValidValue_SetsFixedSize()
    {
        // Arrange
        var control = new Rectangle();

        // Act
        StackPanel.SetFixedSize(control, 10);

        // Assert
        await Assert.That(StackPanel.GetFixedSize(control)).IsEqualTo(10);
    }

    [Test]
    public async Task SetFixedSize_NegativeValue_ClampsToZero()
    {
        // Arrange
        var control = new Rectangle();

        // Act
        StackPanel.SetFixedSize(control, -5);

        // Assert
        await Assert.That(StackPanel.GetFixedSize(control)).IsEqualTo(0);
    }

    [Test]
    public async Task GetFixedSize_NullControl_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => StackPanel.GetFixedSize(null!))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task SetFixedSize_NullControl_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => StackPanel.SetFixedSize(null!, 10))
            .ThrowsExactly<ArgumentNullException>();
    }

    #endregion

    #region Orientation Property

    [Test]
    public async Task Orientation_DefaultValue_IsVertical()
    {
        // Arrange
        var stackPanel = new StackPanel();

        // Assert
        await Assert.That(stackPanel.Orientation).IsEqualTo(Orientation.Vertical);
    }

    [Test]
    public async Task Orientation_SetHorizontal_UpdatesOrientation()
    {
        // Arrange
        var stackPanel = new StackPanel();

        // Act
        stackPanel.Orientation = Orientation.Horizontal;

        // Assert
        await Assert.That(stackPanel.Orientation).IsEqualTo(Orientation.Horizontal);
    }

    #endregion

    #region Render - Empty Stack

    [Test]
    public void Render_EmptyStackPanel_DoesNotCrash()
    {
        // Arrange
        var stackPanel = new StackPanel();
        var bounds = new Rect(0, 0, 100, 50);

        // Act & Assert - No exception thrown
        stackPanel.Render(_buffer, bounds);
    }

    #endregion

    #region Render - Vertical Orientation

    [Test]
    public async Task Render_VerticalStack_FixedChildren_ArrangesSequentially()
    {
        // Arrange
        var child1 = new Rectangle { BackgroundColor = Color.Red };
        var child2 = new Rectangle { BackgroundColor = Color.Green };
        var child3 = new Rectangle { BackgroundColor = Color.Blue };

        StackPanel.SetSizeMode(child1, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(child1, 5);
        StackPanel.SetSizeMode(child2, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(child2, 10);
        StackPanel.SetSizeMode(child3, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(child3, 3);

        var stackPanel = new StackPanel { Orientation = Orientation.Vertical };
        stackPanel.Children.Add(child1);
        stackPanel.Children.Add(child2);
        stackPanel.Children.Add(child3);

        var bounds = new Rect(0, 0, 50, 20);

        // Act
        stackPanel.Render(_buffer, bounds);

        // Assert - Children stacked vertically: y=0-4 (Red), y=5-14 (Green), y=15-17 (Blue)
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(0, 4).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(0, 5).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(0, 14).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(0, 15).Background).IsEqualTo(Color.Blue);
        await Assert.That(_buffer.GetCell(0, 17).Background).IsEqualTo(Color.Blue);
    }

    [Test]
    public async Task Render_VerticalStack_StretchChildren_DividesSpaceEvenly()
    {
        // Arrange
        var child1 = new Rectangle { BackgroundColor = Color.Red };
        var child2 = new Rectangle { BackgroundColor = Color.Green };

        StackPanel.SetSizeMode(child1, ChildSizeMode.Stretch);
        StackPanel.SetSizeMode(child2, ChildSizeMode.Stretch);

        var stackPanel = new StackPanel { Orientation = Orientation.Vertical };
        stackPanel.Children.Add(child1);
        stackPanel.Children.Add(child2);

        var bounds = new Rect(0, 0, 50, 20);

        // Act
        stackPanel.Render(_buffer, bounds);

        // Assert - Each child gets 10 height (20/2)
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(0, 9).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(0, 10).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(0, 19).Background).IsEqualTo(Color.Green);
    }

    [Test]
    public async Task Render_VerticalStack_MixedSizing_CalculatesCorrectly()
    {
        // Arrange
        var child1 = new Rectangle { BackgroundColor = Color.Red };
        var child2 = new Rectangle { BackgroundColor = Color.Green };
        var child3 = new Rectangle { BackgroundColor = Color.Blue };

        StackPanel.SetSizeMode(child1, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(child1, 5);
        StackPanel.SetSizeMode(child2, ChildSizeMode.Stretch);
        StackPanel.SetSizeMode(child3, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(child3, 3);

        var stackPanel = new StackPanel { Orientation = Orientation.Vertical };
        stackPanel.Children.Add(child1);
        stackPanel.Children.Add(child2);
        stackPanel.Children.Add(child3);

        var bounds = new Rect(0, 0, 50, 20);

        // Act
        stackPanel.Render(_buffer, bounds);

        // Assert - child1: 5, child2: 12 (20 - 5 - 3), child3: 3
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(0, 4).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(0, 5).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(0, 16).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(0, 17).Background).IsEqualTo(Color.Blue);
        await Assert.That(_buffer.GetCell(0, 19).Background).IsEqualTo(Color.Blue);
    }

    #endregion

    #region Render - Horizontal Orientation

    [Test]
    public async Task Render_HorizontalStack_FixedChildren_ArrangesSequentially()
    {
        // Arrange
        var child1 = new Rectangle { BackgroundColor = Color.Red };
        var child2 = new Rectangle { BackgroundColor = Color.Green };
        var child3 = new Rectangle { BackgroundColor = Color.Blue };

        StackPanel.SetSizeMode(child1, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(child1, 10);
        StackPanel.SetSizeMode(child2, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(child2, 15);
        StackPanel.SetSizeMode(child3, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(child3, 5);

        var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };
        stackPanel.Children.Add(child1);
        stackPanel.Children.Add(child2);
        stackPanel.Children.Add(child3);

        var bounds = new Rect(0, 0, 50, 20);

        // Act
        stackPanel.Render(_buffer, bounds);

        // Assert - Children stacked horizontally: x=0-9 (Red), x=10-24 (Green), x=25-29 (Blue)
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(9, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(10, 0).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(24, 0).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(25, 0).Background).IsEqualTo(Color.Blue);
        await Assert.That(_buffer.GetCell(29, 0).Background).IsEqualTo(Color.Blue);
    }

    [Test]
    public async Task Render_HorizontalStack_StretchChildren_DividesSpaceEvenly()
    {
        // Arrange
        var child1 = new Rectangle { BackgroundColor = Color.Red };
        var child2 = new Rectangle { BackgroundColor = Color.Green };

        StackPanel.SetSizeMode(child1, ChildSizeMode.Stretch);
        StackPanel.SetSizeMode(child2, ChildSizeMode.Stretch);

        var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };
        stackPanel.Children.Add(child1);
        stackPanel.Children.Add(child2);

        var bounds = new Rect(0, 0, 50, 20);

        // Act
        stackPanel.Render(_buffer, bounds);

        // Assert - Each child gets 25 width (50/2)
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(24, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(25, 0).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(49, 0).Background).IsEqualTo(Color.Green);
    }

    [Test]
    public async Task Render_HorizontalStack_MixedSizing_CalculatesCorrectly()
    {
        // Arrange
        var child1 = new Rectangle { BackgroundColor = Color.Red };
        var child2 = new Rectangle { BackgroundColor = Color.Green };
        var child3 = new Rectangle { BackgroundColor = Color.Blue };

        StackPanel.SetSizeMode(child1, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(child1, 10);
        StackPanel.SetSizeMode(child2, ChildSizeMode.Stretch);
        StackPanel.SetSizeMode(child3, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(child3, 5);

        var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };
        stackPanel.Children.Add(child1);
        stackPanel.Children.Add(child2);
        stackPanel.Children.Add(child3);

        var bounds = new Rect(0, 0, 50, 20);

        // Act
        stackPanel.Render(_buffer, bounds);

        // Assert - child1: 10, child2: 35 (50 - 10 - 5), child3: 5
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(9, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(10, 0).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(44, 0).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(45, 0).Background).IsEqualTo(Color.Blue);
        await Assert.That(_buffer.GetCell(49, 0).Background).IsEqualTo(Color.Blue);
    }

    #endregion

    #region CalculateChildSizes

    [Test]
    public async Task CalculateChildSizes_AllFixed_ReturnsFixedSizes()
    {
        // Arrange
        var child1 = new Rectangle();
        var child2 = new Rectangle();

        StackPanel.SetSizeMode(child1, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(child1, 10);
        StackPanel.SetSizeMode(child2, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(child2, 20);

        var stackPanel = new StackPanel { Orientation = Orientation.Vertical };
        stackPanel.Children.Add(child1);
        stackPanel.Children.Add(child2);

        var bounds = new Rect(0, 0, 50, 100);

        // Act
        var sizes = stackPanel.CalculateChildSizes(bounds);

        // Assert
        await Assert.That(sizes.Length).IsEqualTo(2);
        await Assert.That(sizes[0]).IsEqualTo(10);
        await Assert.That(sizes[1]).IsEqualTo(20);
    }

    [Test]
    public async Task CalculateChildSizes_AllStretch_DividesEvenly()
    {
        // Arrange
        var child1 = new Rectangle();
        var child2 = new Rectangle();
        var child3 = new Rectangle();

        StackPanel.SetSizeMode(child1, ChildSizeMode.Stretch);
        StackPanel.SetSizeMode(child2, ChildSizeMode.Stretch);
        StackPanel.SetSizeMode(child3, ChildSizeMode.Stretch);

        var stackPanel = new StackPanel { Orientation = Orientation.Vertical };
        stackPanel.Children.Add(child1);
        stackPanel.Children.Add(child2);
        stackPanel.Children.Add(child3);

        var bounds = new Rect(0, 0, 50, 90);

        // Act
        var sizes = stackPanel.CalculateChildSizes(bounds);

        // Assert - 90/3 = 30 each
        await Assert.That(sizes.Length).IsEqualTo(3);
        await Assert.That(sizes[0]).IsEqualTo(30);
        await Assert.That(sizes[1]).IsEqualTo(30);
        await Assert.That(sizes[2]).IsEqualTo(30);
    }

    [Test]
    public async Task CalculateChildSizes_MixedSizing_DistributesCorrectly()
    {
        // Arrange
        var child1 = new Rectangle();
        var child2 = new Rectangle();
        var child3 = new Rectangle();

        StackPanel.SetSizeMode(child1, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(child1, 20);
        StackPanel.SetSizeMode(child2, ChildSizeMode.Stretch);
        StackPanel.SetSizeMode(child3, ChildSizeMode.Stretch);

        var stackPanel = new StackPanel { Orientation = Orientation.Vertical };
        stackPanel.Children.Add(child1);
        stackPanel.Children.Add(child2);
        stackPanel.Children.Add(child3);

        var bounds = new Rect(0, 0, 50, 100);

        // Act
        var sizes = stackPanel.CalculateChildSizes(bounds);

        // Assert - child1: 20, remaining 80 split between child2 and child3 (40 each)
        await Assert.That(sizes.Length).IsEqualTo(3);
        await Assert.That(sizes[0]).IsEqualTo(20);
        await Assert.That(sizes[1]).IsEqualTo(40);
        await Assert.That(sizes[2]).IsEqualTo(40);
    }

    [Test]
    public async Task CalculateChildSizes_FixedExceedsAvailable_ReturnsFixedSizes()
    {
        // Arrange
        var child1 = new Rectangle();
        var child2 = new Rectangle();

        StackPanel.SetSizeMode(child1, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(child1, 60);
        StackPanel.SetSizeMode(child2, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(child2, 50);

        var stackPanel = new StackPanel { Orientation = Orientation.Vertical };
        stackPanel.Children.Add(child1);
        stackPanel.Children.Add(child2);

        var bounds = new Rect(0, 0, 50, 100);

        // Act
        var sizes = stackPanel.CalculateChildSizes(bounds);

        // Assert - Fixed sizes are returned even if they exceed bounds
        await Assert.That(sizes[0]).IsEqualTo(60);
        await Assert.That(sizes[1]).IsEqualTo(50);
    }

    [Test]
    public async Task CalculateChildSizes_StretchWithNoRemainingSpace_ReturnsZero()
    {
        // Arrange
        var child1 = new Rectangle();
        var child2 = new Rectangle();

        StackPanel.SetSizeMode(child1, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(child1, 100);
        StackPanel.SetSizeMode(child2, ChildSizeMode.Stretch);

        var stackPanel = new StackPanel { Orientation = Orientation.Vertical };
        stackPanel.Children.Add(child1);
        stackPanel.Children.Add(child2);

        var bounds = new Rect(0, 0, 50, 100);

        // Act
        var sizes = stackPanel.CalculateChildSizes(bounds);

        // Assert - child1 uses all 100, child2 gets 0
        await Assert.That(sizes[0]).IsEqualTo(100);
        await Assert.That(sizes[1]).IsEqualTo(0);
    }

    #endregion

    #region CreateChildBounds

    [Test]
    public async Task CreateChildBounds_Vertical_CreatesCorrectBounds()
    {
        // Arrange
        var stackPanel = new StackPanel { Orientation = Orientation.Vertical };
        var stackBounds = new Rect(10, 20, 50, 100);

        // Act
        var childBounds = stackPanel.CreateChildBounds(stackBounds, 30, 15);

        // Assert - Vertical: X=stack.X, Y=position, Width=stack.Width, Height=size
        await Assert.That(childBounds.X).IsEqualTo(10);
        await Assert.That(childBounds.Y).IsEqualTo(30);
        await Assert.That(childBounds.Width).IsEqualTo(50);
        await Assert.That(childBounds.Height).IsEqualTo(15);
    }

    [Test]
    public async Task CreateChildBounds_Horizontal_CreatesCorrectBounds()
    {
        // Arrange
        var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };
        var stackBounds = new Rect(10, 20, 50, 100);

        // Act
        var childBounds = stackPanel.CreateChildBounds(stackBounds, 25, 12);

        // Assert - Horizontal: X=position, Y=stack.Y, Width=size, Height=stack.Height
        await Assert.That(childBounds.X).IsEqualTo(25);
        await Assert.That(childBounds.Y).IsEqualTo(20);
        await Assert.That(childBounds.Width).IsEqualTo(12);
        await Assert.That(childBounds.Height).IsEqualTo(100);
    }

    #endregion

    #region GetPreferredSize

    [Test]
    public async Task GetPreferredSize_EmptyStackPanel_ReturnsZero()
    {
        // Arrange
        var stackPanel = new StackPanel();
        var parent = new Rect(0, 0, 100, 50);

        // Act
        var size = stackPanel.GetPreferredSize(parent);

        // Assert
        await Assert.That(size.Width).IsEqualTo(0);
        await Assert.That(size.Height).IsEqualTo(0);
    }

    [Test]
    public async Task GetPreferredSize_VerticalStack_SumsHeights()
    {
        // Arrange
        var child1 = new Label { Text = "A" }; // Preferred size: 1x1
        var child2 = new Label { Text = "BBB" }; // Preferred size: 3x1

        StackPanel.SetSizeMode(child1, ChildSizeMode.Auto);
        StackPanel.SetSizeMode(child2, ChildSizeMode.Auto);

        var stackPanel = new StackPanel { Orientation = Orientation.Vertical };
        stackPanel.Children.Add(child1);
        stackPanel.Children.Add(child2);

        var parent = new Rect(0, 0, 100, 50);

        // Act
        var size = stackPanel.GetPreferredSize(parent);

        // Assert - Height: 1+1=2, Width: max(1,3)=3
        await Assert.That(size.Width).IsEqualTo(3);
        await Assert.That(size.Height).IsEqualTo(2);
    }

    [Test]
    public async Task GetPreferredSize_HorizontalStack_SumsWidths()
    {
        // Arrange
        var child1 = new Label { Text = "A" }; // Preferred size: 1x1
        var child2 = new Label { Text = "BBB" }; // Preferred size: 3x1

        StackPanel.SetSizeMode(child1, ChildSizeMode.Auto);
        StackPanel.SetSizeMode(child2, ChildSizeMode.Auto);

        var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };
        stackPanel.Children.Add(child1);
        stackPanel.Children.Add(child2);

        var parent = new Rect(0, 0, 100, 50);

        // Act
        var size = stackPanel.GetPreferredSize(parent);

        // Assert - Width: 1+3=4, Height: max(1,1)=1
        await Assert.That(size.Width).IsEqualTo(4);
        await Assert.That(size.Height).IsEqualTo(1);
    }

    [Test]
    public async Task GetPreferredSize_FixedSizeChildren_UsesFixedSizes()
    {
        // Arrange
        var child1 = new Rectangle();
        var child2 = new Rectangle();

        StackPanel.SetSizeMode(child1, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(child1, 10);
        StackPanel.SetSizeMode(child2, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(child2, 20);

        var stackPanel = new StackPanel { Orientation = Orientation.Vertical };
        stackPanel.Children.Add(child1);
        stackPanel.Children.Add(child2);

        var parent = new Rect(0, 0, 100, 50);

        // Act
        var size = stackPanel.GetPreferredSize(parent);

        // Assert - Height: 10+20=30, Width: 0 (Rectangle has no preferred width)
        await Assert.That(size.Height).IsEqualTo(30);
    }

    #endregion

    #region CalculateBounds

    [Test]
    public async Task CalculateBounds_ReturnsParentBounds()
    {
        // Arrange
        var stackPanel = new StackPanel();
        var parent = new Rect(10, 20, 80, 40);

        // Act
        var bounds = stackPanel.CalculateBounds(parent);

        // Assert
        await Assert.That(bounds).IsEqualTo(parent);
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task Render_ZeroSizeChild_Skipped()
    {
        // Arrange
        var child1 = new Rectangle { BackgroundColor = Color.Red };
        var child2 = new Rectangle { BackgroundColor = Color.Green };

        StackPanel.SetSizeMode(child1, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(child1, 0); // Zero size
        StackPanel.SetSizeMode(child2, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(child2, 10);

        var stackPanel = new StackPanel { Orientation = Orientation.Vertical };
        stackPanel.Children.Add(child1);
        stackPanel.Children.Add(child2);

        var bounds = new Rect(0, 0, 50, 20);

        // Act
        stackPanel.Render(_buffer, bounds);

        // Assert - child1 is skipped, child2 starts at y=0
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(0, 9).Background).IsEqualTo(Color.Green);
    }

    [Test]
    public async Task Render_SingleChild_FillsEntireStack()
    {
        // Arrange
        var child = new Rectangle { BackgroundColor = Color.Red };
        StackPanel.SetSizeMode(child, ChildSizeMode.Stretch);

        var stackPanel = new StackPanel { Orientation = Orientation.Vertical };
        stackPanel.Children.Add(child);

        var bounds = new Rect(0, 0, 50, 20);

        // Act
        stackPanel.Render(_buffer, bounds);

        // Assert - Child fills entire stack
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(49, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(0, 19).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(49, 19).Background).IsEqualTo(Color.Red);
    }

    #endregion
}
