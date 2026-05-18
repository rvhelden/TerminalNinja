namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Tests for the FocusManager class.
/// </summary>
public class FocusManagerTests
{
    /// <summary>
    /// Creates a simple mock focusable control for testing.
    /// Extends Control so that Focusable defaults to true.
    /// </summary>
    private class MockFocusable : Control
    {
        public int GotFocusCount { get; private set; }
        public int LostFocusCount { get; private set; }
        public int MouseEnterCount { get; private set; }
        public int MouseLeaveCount { get; private set; }
        public List<KeyEvent> ReceivedKeyEvents { get; } = new();
        public List<MouseEvent> ReceivedMouseEvents { get; } = new();
        
        private readonly Rect _bounds;
        
        public MockFocusable(Rect bounds, int tabIndex = 0)
        {
            _bounds = bounds;
            TabIndex = tabIndex;
        }
        
        public override void OnGotFocus() => GotFocusCount++;
        public override void OnLostFocus() => LostFocusCount++;
        public override void OnMouseEnter() => MouseEnterCount++;
        public override void OnMouseLeave() => MouseLeaveCount++;
        public override void OnKeyEvent(KeyEvent e) => ReceivedKeyEvents.Add(e);
        public override void OnMouseEvent(MouseEvent e) => ReceivedMouseEvents.Add(e);
        
        public override Size2D GetPreferredSize(Rect parent) => new(_bounds.Width, _bounds.Height);
        public override Rect CalculateBounds(Rect parent) => _bounds;
        protected override void OnRender(CellBuffer buffer, Rect parentBounds) { }
    }
    
    [Test]
    public async Task SetFocus_WhenElementIsNull_ClearsFocus()
    {
        // Arrange
        var manager = new FocusManager();
        var control = new MockFocusable(new Rect(0, 0, 10, 3));
        manager.SetFocus(control);
        
        // Act
        manager.SetFocus(null);
        
        // Assert
        await Assert.That(manager.FocusedElement).IsNull();
        await Assert.That(control.IsFocused).IsFalse();
        await Assert.That(control.LostFocusCount).IsEqualTo(1);
    }
    
    [Test]
    public async Task SetFocus_WhenElementIsNew_SetsFocusAndCallsCallbacks()
    {
        // Arrange
        var manager = new FocusManager();
        var control = new MockFocusable(new Rect(0, 0, 10, 3));
        
        // Act
        manager.SetFocus(control);
        
        // Assert
        await Assert.That(manager.FocusedElement).IsEqualTo(control);
        await Assert.That(control.IsFocused).IsTrue();
        await Assert.That(control.GotFocusCount).IsEqualTo(1);
    }
    
    [Test]
    public async Task SetFocus_WhenChangingFocus_BlursPreviousAndFocusesNew()
    {
        // Arrange
        var manager = new FocusManager();
        var element1 = new MockFocusable(new Rect(0, 0, 10, 3));
        var element2 = new MockFocusable(new Rect(0, 3, 10, 3));
        manager.SetFocus(element1);
        
        // Act
        manager.SetFocus(element2);
        
        // Assert
        await Assert.That(manager.FocusedElement).IsEqualTo(element2);
        await Assert.That(element1.IsFocused).IsFalse();
        await Assert.That(element1.LostFocusCount).IsEqualTo(1);
        await Assert.That(element2.IsFocused).IsTrue();
        await Assert.That(element2.GotFocusCount).IsEqualTo(1);
    }
    
    [Test]
    public async Task SetFocus_WhenSameElement_DoesNothing()
    {
        // Arrange
        var manager = new FocusManager();
        var control = new MockFocusable(new Rect(0, 0, 10, 3));
        manager.SetFocus(control);
        
        // Act
        manager.SetFocus(control);
        
        // Assert
        await Assert.That(control.GotFocusCount).IsEqualTo(1);
        await Assert.That(control.LostFocusCount).IsEqualTo(0);
    }
    
    [Test]
    public async Task ClearFocus_RemovesFocusFromCurrentElement()
    {
        // Arrange
        var manager = new FocusManager();
        var control = new MockFocusable(new Rect(0, 0, 10, 3));
        manager.SetFocus(control);
        
        // Act
        manager.ClearFocus();
        
        // Assert
        await Assert.That(manager.FocusedElement).IsNull();
        await Assert.That(control.IsFocused).IsFalse();
    }
    
    [Test]
    public async Task UpdateHover_WhenMouseEntersElement_SetsHoverAndCallsCallback()
    {
        // Arrange
        var manager = new FocusManager();
        var control = new MockFocusable(new Rect(5, 5, 10, 3));
        
        // Act
        var result = manager.UpdateHover(control, new Rect(0, 0, 80, 24), 7, 6);
        
        // Assert
        await Assert.That(result).IsEqualTo(control);
        await Assert.That(manager.HoveredElement).IsEqualTo(control);
        await Assert.That(control.IsMouseOver).IsTrue();
        await Assert.That(control.MouseEnterCount).IsEqualTo(1);
    }
    
    [Test]
    public async Task UpdateHover_WhenMouseLeavesElement_ClearsHoverAndCallsCallback()
    {
        // Arrange
        var manager = new FocusManager();
        var control = new MockFocusable(new Rect(5, 5, 10, 3));
        manager.UpdateHover(control, new Rect(0, 0, 80, 24), 7, 6);
        
        // Act
        var result = manager.UpdateHover(control, new Rect(0, 0, 80, 24), 0, 0);
        
        // Assert
        await Assert.That(result).IsNull();
        await Assert.That(manager.HoveredElement).IsNull();
        await Assert.That(control.IsMouseOver).IsFalse();
        await Assert.That(control.MouseLeaveCount).IsEqualTo(1);
    }
    
    [Test]
    public async Task UpdateHover_WhenSameElement_DoesNothing()
    {
        // Arrange
        var manager = new FocusManager();
        var control = new MockFocusable(new Rect(5, 5, 10, 3));
        manager.UpdateHover(control, new Rect(0, 0, 80, 24), 7, 6);
        
        // Act
        manager.UpdateHover(control, new Rect(0, 0, 80, 24), 8, 6);
        
        // Assert
        await Assert.That(control.MouseEnterCount).IsEqualTo(1);
        await Assert.That(control.MouseLeaveCount).IsEqualTo(0);
    }
    
    [Test]
    public async Task HitTest_WhenPointInside_ReturnsElement()
    {
        // Arrange
        var manager = new FocusManager();
        var control = new MockFocusable(new Rect(5, 5, 10, 3));
        
        // Act
        var result = manager.HitTest(control, new Rect(0, 0, 80, 24), 7, 6);
        
        // Assert
        await Assert.That(result).IsEqualTo(control);
    }
    
    [Test]
    public async Task HitTest_WhenPointOutside_ReturnsNull()
    {
        // Arrange
        var manager = new FocusManager();
        var control = new MockFocusable(new Rect(5, 5, 10, 3));
        
        // Act
        var result = manager.HitTest(control, new Rect(0, 0, 80, 24), 0, 0);
        
        // Assert
        await Assert.That(result).IsNull();
    }
    
    [Test]
    public async Task HandleMouseEvent_WithPress_FocusesElement()
    {
        // Arrange
        var manager = new FocusManager();
        var control = new MockFocusable(new Rect(5, 5, 10, 3));
        var mouseEvent = new MouseEvent(7, 6, MouseButton.Left, MouseAction.Press);
        
        // Act
        manager.HandleMouseEvent(control, new Rect(0, 0, 80, 24), mouseEvent);
        
        // Assert
        await Assert.That(manager.FocusedElement).IsEqualTo(control);
        await Assert.That(control.ReceivedMouseEvents.Count).IsEqualTo(1);
    }
    
    [Test]
    public async Task HandleMouseEvent_WithMove_UpdatesHover()
    {
        // Arrange
        var manager = new FocusManager();
        var control = new MockFocusable(new Rect(5, 5, 10, 3));
        var mouseEvent = new MouseEvent(7, 6, MouseButton.None, MouseAction.Move);
        
        // Act
        manager.HandleMouseEvent(control, new Rect(0, 0, 80, 24), mouseEvent);
        
        // Assert
        await Assert.That(manager.HoveredElement).IsEqualTo(control);
    }
    
    [Test]
    public async Task HandleKeyEvent_DispatchesToFocusedElement()
    {
        // Arrange
        var manager = new FocusManager();
        var control = new MockFocusable(new Rect(5, 5, 10, 3));
        manager.SetFocus(control);
        var keyEvent = new KeyEvent(ConsoleKey.Enter, '\r', false, false, false);
        
        // Act
        manager.HandleKeyEvent(keyEvent);
        
        // Assert
        await Assert.That(control.ReceivedKeyEvents.Count).IsEqualTo(1);
        await Assert.That(control.ReceivedKeyEvents[0]).IsEqualTo(keyEvent);
    }
    
    [Test]
    public async Task HandleKeyEvent_WhenNoFocus_DoesNothing()
    {
        // Arrange
        var manager = new FocusManager();
        var keyEvent = new KeyEvent(ConsoleKey.Enter, '\r', false, false, false);
        
        // Act & Assert (should not throw)
        manager.HandleKeyEvent(keyEvent);
        
        await Assert.That(manager.FocusedElement).IsNull();
    }
}
