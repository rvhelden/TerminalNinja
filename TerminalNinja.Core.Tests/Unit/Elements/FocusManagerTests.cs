namespace TerminalNinja.Core.Tests.Unit.Elements;

/// <summary>
/// Tests for the FocusManager class.
/// </summary>
public class FocusManagerTests
{
    /// <summary>
    /// Creates a simple mock focusable element for testing.
    /// </summary>
    private class MockFocusable : IFocusable
    {
        public string? Name { get; set; }
        public bool IsFocused { get; set; }
        public bool IsHovered { get; set; }
        public bool CanFocus { get; init; } = true;
        public int TabIndex { get; init; }
        
        // IElement members
        public object? DataContext { get; set; }
        public IElement? Parent { get; set; }
        public Action? InvalidationCallback { get; set; }
        public void InvalidateVisual() => InvalidationCallback?.Invoke();
        
        public int FocusCount { get; private set; }
        public int BlurCount { get; private set; }
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
        
        public void OnFocus() => FocusCount++;
        public void OnBlur() => BlurCount++;
        public void OnMouseEnter() => MouseEnterCount++;
        public void OnMouseLeave() => MouseLeaveCount++;
        public void OnKeyEvent(KeyEvent e) => ReceivedKeyEvents.Add(e);
        public void OnMouseEvent(MouseEvent e) => ReceivedMouseEvents.Add(e);
        
        public Size2D GetPreferredSize(Rect parent) => new(_bounds.Width, _bounds.Height);
        public Rect CalculateBounds(Rect parent) => _bounds;
        public void Render(CellBuffer buffer, Rect parentBounds) { }
        
        public Rect? HitTest(int x, int y, Rect parentBounds)
        {
            return _bounds.Contains(x, y) ? _bounds : null;
        }
    }
    
    [Test]
    public async Task SetFocus_WhenElementIsNull_ClearsFocus()
    {
        // Arrange
        var manager = new FocusManager();
        var element = new MockFocusable(new Rect(0, 0, 10, 3));
        manager.SetFocus(element);
        
        // Act
        manager.SetFocus(null);
        
        // Assert
        await Assert.That(manager.FocusedElement).IsNull();
        await Assert.That(element.IsFocused).IsFalse();
        await Assert.That(element.BlurCount).IsEqualTo(1);
    }
    
    [Test]
    public async Task SetFocus_WhenElementIsNew_SetsFocusAndCallsCallbacks()
    {
        // Arrange
        var manager = new FocusManager();
        var element = new MockFocusable(new Rect(0, 0, 10, 3));
        
        // Act
        manager.SetFocus(element);
        
        // Assert
        await Assert.That(manager.FocusedElement).IsEqualTo(element);
        await Assert.That(element.IsFocused).IsTrue();
        await Assert.That(element.FocusCount).IsEqualTo(1);
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
        await Assert.That(element1.BlurCount).IsEqualTo(1);
        await Assert.That(element2.IsFocused).IsTrue();
        await Assert.That(element2.FocusCount).IsEqualTo(1);
    }
    
    [Test]
    public async Task SetFocus_WhenSameElement_DoesNothing()
    {
        // Arrange
        var manager = new FocusManager();
        var element = new MockFocusable(new Rect(0, 0, 10, 3));
        manager.SetFocus(element);
        
        // Act
        manager.SetFocus(element);
        
        // Assert
        await Assert.That(element.FocusCount).IsEqualTo(1);
        await Assert.That(element.BlurCount).IsEqualTo(0);
    }
    
    [Test]
    public async Task ClearFocus_RemovesFocusFromCurrentElement()
    {
        // Arrange
        var manager = new FocusManager();
        var element = new MockFocusable(new Rect(0, 0, 10, 3));
        manager.SetFocus(element);
        
        // Act
        manager.ClearFocus();
        
        // Assert
        await Assert.That(manager.FocusedElement).IsNull();
        await Assert.That(element.IsFocused).IsFalse();
    }
    
    [Test]
    public async Task UpdateHover_WhenMouseEntersElement_SetsHoverAndCallsCallback()
    {
        // Arrange
        var manager = new FocusManager();
        var element = new MockFocusable(new Rect(5, 5, 10, 3));
        
        // Act
        var result = manager.UpdateHover(element, new Rect(0, 0, 80, 24), 7, 6);
        
        // Assert
        await Assert.That(result).IsEqualTo(element);
        await Assert.That(manager.HoveredElement).IsEqualTo(element);
        await Assert.That(element.IsHovered).IsTrue();
        await Assert.That(element.MouseEnterCount).IsEqualTo(1);
    }
    
    [Test]
    public async Task UpdateHover_WhenMouseLeavesElement_ClearsHoverAndCallsCallback()
    {
        // Arrange
        var manager = new FocusManager();
        var element = new MockFocusable(new Rect(5, 5, 10, 3));
        manager.UpdateHover(element, new Rect(0, 0, 80, 24), 7, 6);
        
        // Act
        var result = manager.UpdateHover(element, new Rect(0, 0, 80, 24), 0, 0);
        
        // Assert
        await Assert.That(result).IsNull();
        await Assert.That(manager.HoveredElement).IsNull();
        await Assert.That(element.IsHovered).IsFalse();
        await Assert.That(element.MouseLeaveCount).IsEqualTo(1);
    }
    
    [Test]
    public async Task UpdateHover_WhenSameElement_DoesNothing()
    {
        // Arrange
        var manager = new FocusManager();
        var element = new MockFocusable(new Rect(5, 5, 10, 3));
        manager.UpdateHover(element, new Rect(0, 0, 80, 24), 7, 6);
        
        // Act
        manager.UpdateHover(element, new Rect(0, 0, 80, 24), 8, 6);
        
        // Assert
        await Assert.That(element.MouseEnterCount).IsEqualTo(1);
        await Assert.That(element.MouseLeaveCount).IsEqualTo(0);
    }
    
    [Test]
    public async Task HitTest_WhenPointInside_ReturnsElement()
    {
        // Arrange
        var manager = new FocusManager();
        var element = new MockFocusable(new Rect(5, 5, 10, 3));
        
        // Act
        var result = manager.HitTest(element, new Rect(0, 0, 80, 24), 7, 6);
        
        // Assert
        await Assert.That(result).IsEqualTo(element);
    }
    
    [Test]
    public async Task HitTest_WhenPointOutside_ReturnsNull()
    {
        // Arrange
        var manager = new FocusManager();
        var element = new MockFocusable(new Rect(5, 5, 10, 3));
        
        // Act
        var result = manager.HitTest(element, new Rect(0, 0, 80, 24), 0, 0);
        
        // Assert
        await Assert.That(result).IsNull();
    }
    
    [Test]
    public async Task HandleMouseEvent_WithPress_FocusesElement()
    {
        // Arrange
        var manager = new FocusManager();
        var element = new MockFocusable(new Rect(5, 5, 10, 3));
        var mouseEvent = new MouseEvent(7, 6, MouseButton.Left, MouseAction.Press);
        
        // Act
        manager.HandleMouseEvent(element, new Rect(0, 0, 80, 24), mouseEvent);
        
        // Assert
        await Assert.That(manager.FocusedElement).IsEqualTo(element);
        await Assert.That(element.ReceivedMouseEvents.Count).IsEqualTo(1);
    }
    
    [Test]
    public async Task HandleMouseEvent_WithMove_UpdatesHover()
    {
        // Arrange
        var manager = new FocusManager();
        var element = new MockFocusable(new Rect(5, 5, 10, 3));
        var mouseEvent = new MouseEvent(7, 6, MouseButton.None, MouseAction.Move);
        
        // Act
        manager.HandleMouseEvent(element, new Rect(0, 0, 80, 24), mouseEvent);
        
        // Assert
        await Assert.That(manager.HoveredElement).IsEqualTo(element);
    }
    
    [Test]
    public async Task HandleKeyEvent_DispatchesToFocusedElement()
    {
        // Arrange
        var manager = new FocusManager();
        var element = new MockFocusable(new Rect(5, 5, 10, 3));
        manager.SetFocus(element);
        var keyEvent = new KeyEvent(ConsoleKey.Enter, '\r', false, false, false);
        
        // Act
        manager.HandleKeyEvent(keyEvent);
        
        // Assert
        await Assert.That(element.ReceivedKeyEvents.Count).IsEqualTo(1);
        await Assert.That(element.ReceivedKeyEvents[0]).IsEqualTo(keyEvent);
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
