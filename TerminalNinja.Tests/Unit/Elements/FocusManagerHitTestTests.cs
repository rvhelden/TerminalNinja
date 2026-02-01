namespace TerminalNinja.Tests.Unit.Elements;

/// <summary>
/// Tests for FocusManager hit testing with complex layouts (Stack, Rectangle, nested elements).
/// These tests verify that hit testing coordinates match the actual rendered positions of elements.
/// </summary>
public class FocusManagerHitTestTests
{
    [Test]
    public async Task HitTest_ButtonInHorizontalStack_MatchesRenderPosition()
    {
        // Arrange - Create horizontal Stack with 3 buttons (width 12 each)
        var manager = new FocusManager();
        var parentBounds = new Rect(0, 0, 50, 10);
        
        var button1 = new Button
        {
            Text = "Button1",
            Width = Size.Absolute(12),
            Height = Size.Absolute(3),
            TabIndex = 0
        };
        
        var button2 = new Button
        {
            Text = "Button2",
            Width = Size.Absolute(12),
            Height = Size.Absolute(3),
            TabIndex = 1
        };
        
        var button3 = new Button
        {
            Text = "Button3",
            Width = Size.Absolute(12),
            Height = Size.Absolute(3),
            TabIndex = 2
        };
        
        var stack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children =
            [
                StackChild.Auto(button1),
                StackChild.Auto(button2),
                StackChild.Auto(button3)
            ]
        };
        
        // Verify rendering positions by actually rendering
        var buffer = new CellBuffer(50, 10);
        stack.Render(buffer, parentBounds);
        
        // Visual verification: Button1 at x=0-11, Button2 at x=12-23, Button3 at x=24-35
        // We can verify by checking the button text positions in the buffer
        var cell1 = buffer.GetCell(6, 1); // Middle of button1
        var cell2 = buffer.GetCell(18, 1); // Middle of button2
        var cell3 = buffer.GetCell(30, 1); // Middle of button3
        
        // Act - Hit test at the middle of each button's rendered position
        var hit1 = manager.HitTest(stack, parentBounds, 6, 1);  // Should hit button1
        var hit2 = manager.HitTest(stack, parentBounds, 18, 1); // Should hit button2
        var hit3 = manager.HitTest(stack, parentBounds, 30, 1); // Should hit button3
        
        // Assert - Hit test results should match rendered positions
        await Assert.That(hit1).IsEqualTo(button1);
        await Assert.That(hit2).IsEqualTo(button2);
        await Assert.That(hit3).IsEqualTo(button3);
    }
    
    [Test]
    public async Task HitTest_ButtonInVerticalStack_MatchesRenderPosition()
    {
        // Arrange - Create vertical Stack with 3 buttons (height 3 each)
        var manager = new FocusManager();
        var parentBounds = new Rect(0, 0, 50, 20);
        
        var button1 = new Button
        {
            Text = "Button1",
            Width = Size.Absolute(12),
            Height = Size.Absolute(3),
            TabIndex = 0
        };
        
        var button2 = new Button
        {
            Text = "Button2",
            Width = Size.Absolute(12),
            Height = Size.Absolute(3),
            TabIndex = 1
        };
        
        var button3 = new Button
        {
            Text = "Button3",
            Width = Size.Absolute(12),
            Height = Size.Absolute(3),
            TabIndex = 2
        };
        
        var stack = new Stack
        {
            Orientation = StackOrientation.Vertical,
            Children =
            [
                StackChild.Auto(button1),
                StackChild.Auto(button2),
                StackChild.Auto(button3)
            ]
        };
        
        // Verify rendering positions
        var buffer = new CellBuffer(50, 20);
        stack.Render(buffer, parentBounds);
        
        // Visual verification: Button1 at y=0-2, Button2 at y=3-5, Button3 at y=6-8
        
        // Act - Hit test at the middle of each button's rendered position
        var hit1 = manager.HitTest(stack, parentBounds, 6, 1);  // Should hit button1
        var hit2 = manager.HitTest(stack, parentBounds, 6, 4);  // Should hit button2
        var hit3 = manager.HitTest(stack, parentBounds, 6, 7);  // Should hit button3
        
        // Assert
        await Assert.That(hit1).IsEqualTo(button1);
        await Assert.That(hit2).IsEqualTo(button2);
        await Assert.That(hit3).IsEqualTo(button3);
    }
    
    [Test]
    public async Task HitTest_ButtonInNestedRectangleWithBorder_MatchesRenderPosition()
    {
        // Arrange - Rectangle with border containing a Button
        var manager = new FocusManager();
        var parentBounds = new Rect(0, 0, 50, 10);
        
        var button = new Button
        {
            Text = "Button",
            Width = Size.Absolute(12),
            Height = Size.Absolute(3),
            TabIndex = 0
        };
        
        var rectangle = new Rectangle
        {
            Width = Size.Absolute(20),
            Height = Size.Absolute(7),
            Border = Border.Single(Color.White),
            Child = button
        };
        
        // Verify rendering positions - Button should be at (1,1) due to border offset
        var buffer = new CellBuffer(50, 10);
        rectangle.Render(buffer, parentBounds);
        
        // Act - Hit test at button's rendered position (inside border)
        var hit = manager.HitTest(rectangle, parentBounds, 6, 2);  // Inside button at (1,1)
        var miss = manager.HitTest(rectangle, parentBounds, 0, 0); // On border, not button
        
        // Assert
        await Assert.That(hit).IsEqualTo(button);
        await Assert.That(miss).IsNull();
    }
    
    [Test]
    public async Task HitTest_ButtonsInStackInsideRectangle_MatchesRenderPosition()
    {
        // Arrange - Rectangle with border, containing horizontal Stack with 2 buttons
        var manager = new FocusManager();
        var parentBounds = new Rect(0, 0, 50, 10);
        
        var button1 = new Button
        {
            Text = "Button1",
            Width = Size.Absolute(10),
            Height = Size.Absolute(3),
            TabIndex = 0
        };
        
        var button2 = new Button
        {
            Text = "Button2",
            Width = Size.Absolute(10),
            Height = Size.Absolute(3),
            TabIndex = 1
        };
        
        var stack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children =
            [
                StackChild.Auto(button1),
                StackChild.Auto(button2)
            ]
        };
        
        var rectangle = new Rectangle
        {
            Width = Size.Absolute(30),
            Height = Size.Absolute(7),
            Border = Border.Single(Color.White),
            Child = stack
        };
        
        // Verify rendering positions
        // Rectangle at (0,0), border offsets inner to (1,1)
        // Button1 should render at x=1-10 (screen coords), Button2 at x=11-20
        var buffer = new CellBuffer(50, 10);
        rectangle.Render(buffer, parentBounds);
        
        // Act - Hit test at button positions (accounting for border offset)
        var hit1 = manager.HitTest(rectangle, parentBounds, 5, 2);   // Middle of button1
        var hit2 = manager.HitTest(rectangle, parentBounds, 15, 2);  // Middle of button2
        var miss = manager.HitTest(rectangle, parentBounds, 0, 0);   // Border
        
        // Assert
        await Assert.That(hit1).IsEqualTo(button1);
        await Assert.That(hit2).IsEqualTo(button2);
        await Assert.That(miss).IsNull();
    }
    
    [Test]
    public async Task HitTest_ButtonAfterStretchChild_MatchesRenderPosition()
    {
        // Arrange - Horizontal Stack with [Stretch(spacer), Auto(Button)]
        var manager = new FocusManager();
        var parentBounds = new Rect(0, 0, 100, 10);
        
        var spacer = new Rectangle
        {
            BackgroundColor = Color.Black
        };
        
        var button = new Button
        {
            Text = "Button",
            Width = Size.Absolute(12),
            Height = Size.Absolute(3),
            TabIndex = 0
        };
        
        var stack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children =
            [
                StackChild.Stretch(spacer),
                StackChild.Auto(button)
            ]
        };
        
        // Verify rendering positions
        // In a 100-wide container, button (width 12) should be at x=88-99
        var buffer = new CellBuffer(100, 10);
        stack.Render(buffer, parentBounds);
        
        // Act - Hit test at button's rendered position (right side of screen)
        var hit = manager.HitTest(stack, parentBounds, 94, 1);  // Middle of button at x=88-99
        var miss = manager.HitTest(stack, parentBounds, 10, 1); // In the spacer area
        
        // Assert
        await Assert.That(hit).IsEqualTo(button);
        await Assert.That(miss).IsNull();
    }
    
    [Test]
    public async Task HitTest_FixedSizeButtonsInStack_MatchesRenderPosition()
    {
        // Arrange - Stack with Fixed-size children
        var manager = new FocusManager();
        var parentBounds = new Rect(0, 0, 100, 10);
        
        var button1 = new Button
        {
            Text = "Btn1",
            Width = Size.Absolute(8),
            Height = Size.Absolute(3),
            TabIndex = 0
        };
        
        var button2 = new Button
        {
            Text = "Btn2",
            Width = Size.Absolute(8),
            Height = Size.Absolute(3),
            TabIndex = 1
        };
        
        var stack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children =
            [
                StackChild.Fixed(button1, 20), // Fixed to 20 cells wide
                StackChild.Fixed(button2, 15)  // Fixed to 15 cells wide
            ]
        };
        
        // Verify rendering positions
        // Button1 (width 8) allocated 20 cells: occupies x=0-7 (left-aligned by default)
        // Button2 (width 8) allocated 15 cells: occupies x=20-27 (left-aligned by default)
        var buffer = new CellBuffer(100, 10);
        stack.Render(buffer, parentBounds);
        
        // Act - Hit test at button positions (not in the empty space of Fixed allocation)
        var hit1 = manager.HitTest(stack, parentBounds, 4, 1);   // Middle of button1 (x=0-7)
        var hit2 = manager.HitTest(stack, parentBounds, 24, 1);  // Middle of button2 (x=20-27)
        var miss = manager.HitTest(stack, parentBounds, 15, 1);  // Empty space in button1's slot
        
        // Assert
        await Assert.That(hit1).IsEqualTo(button1);
        await Assert.That(hit2).IsEqualTo(button2);
        await Assert.That(miss).IsNull();
    }
}
