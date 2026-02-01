using TerminalNinja.Core.Input;
using TerminalNinja.Core.Primitives;

namespace TerminalNinja.Core.Elements;

/// <summary>
/// Manages keyboard focus and mouse hover state for focusable elements in the UI tree.
/// </summary>
public sealed class FocusManager
{
    private IFocusable? _focusedElement;
    private IFocusable? _hoveredElement;
    
    /// <summary>
    /// Gets the currently focused element, or null if no element has focus.
    /// </summary>
    public IFocusable? FocusedElement => _focusedElement;
    
    /// <summary>
    /// Gets the currently hovered element, or null if no element is hovered.
    /// </summary>
    public IFocusable? HoveredElement => _hoveredElement;
    
    /// <summary>
    /// Sets focus to the specified element. Pass null to clear focus.
    /// </summary>
    /// <param name="element">The element to focus, or null to clear focus.</param>
    public void SetFocus(IFocusable? element)
    {
        if (_focusedElement == element)
            return;
        
        // Blur previous element
        if (_focusedElement is not null)
        {
            _focusedElement.IsFocused = false;
            _focusedElement.OnBlur();
        }
        
        _focusedElement = element;
        
        // Focus new element
        if (_focusedElement is not null)
        {
            _focusedElement.IsFocused = true;
            _focusedElement.OnFocus();
        }
    }
    
    /// <summary>
    /// Clears keyboard focus from the current element.
    /// </summary>
    public void ClearFocus() => SetFocus(null);
    
    /// <summary>
    /// Moves focus to the next focusable element in tab order.
    /// </summary>
    /// <param name="rootElement">The root element to search for focusable children.</param>
    /// <param name="rootBounds">The bounds of the root element.</param>
    public void FocusNext(IElement rootElement, Rect rootBounds)
    {
        var focusableElements = CollectFocusableElements(rootElement, rootBounds)
            .OrderBy(x => x.Element.TabIndex)
            .ThenBy(x => x.Bounds.Y)
            .ThenBy(x => x.Bounds.X)
            .ToList();
        
        if (focusableElements.Count == 0)
            return;
        
        if (_focusedElement is null)
        {
            SetFocus(focusableElements[0].Element);
            return;
        }
        
        var currentIndex = focusableElements.FindIndex(x => x.Element == _focusedElement);
        if (currentIndex == -1)
        {
            SetFocus(focusableElements[0].Element);
            return;
        }
        
        var nextIndex = (currentIndex + 1) % focusableElements.Count;
        SetFocus(focusableElements[nextIndex].Element);
    }
    
    /// <summary>
    /// Moves focus to the previous focusable element in tab order.
    /// </summary>
    /// <param name="rootElement">The root element to search for focusable children.</param>
    /// <param name="rootBounds">The bounds of the root element.</param>
    public void FocusPrevious(IElement rootElement, Rect rootBounds)
    {
        var focusableElements = CollectFocusableElements(rootElement, rootBounds)
            .OrderBy(x => x.Element.TabIndex)
            .ThenBy(x => x.Bounds.Y)
            .ThenBy(x => x.Bounds.X)
            .ToList();
        
        if (focusableElements.Count == 0)
            return;
        
        if (_focusedElement is null)
        {
            SetFocus(focusableElements[^1].Element);
            return;
        }
        
        var currentIndex = focusableElements.FindIndex(x => x.Element == _focusedElement);
        if (currentIndex == -1)
        {
            SetFocus(focusableElements[^1].Element);
            return;
        }
        
        var prevIndex = currentIndex == 0 ? focusableElements.Count - 1 : currentIndex - 1;
        SetFocus(focusableElements[prevIndex].Element);
    }
    
    /// <summary>
    /// Updates hover state based on mouse position.
    /// </summary>
    /// <param name="rootElement">The root element to search for focusable children.</param>
    /// <param name="rootBounds">The bounds of the root element.</param>
    /// <param name="mouseX">The mouse X coordinate.</param>
    /// <param name="mouseY">The mouse Y coordinate.</param>
    /// <returns>The element under the mouse cursor, or null if none.</returns>
    public IFocusable? UpdateHover(IElement rootElement, Rect rootBounds, int mouseX, int mouseY)
    {
        var hitElement = HitTest(rootElement, rootBounds, mouseX, mouseY);
        
        if (_hoveredElement == hitElement)
            return hitElement;
        
        // Leave previous element
        if (_hoveredElement is not null)
        {
            _hoveredElement.IsHovered = false;
            _hoveredElement.OnMouseLeave();
        }
        
        _hoveredElement = hitElement;
        
        // Enter new element
        if (_hoveredElement is not null)
        {
            _hoveredElement.IsHovered = true;
            _hoveredElement.OnMouseEnter();
        }
        
        return hitElement;
    }
    
    /// <summary>
    /// Performs hit testing to find the focusable element at the specified coordinates.
    /// </summary>
    /// <param name="rootElement">The root element to search.</param>
    /// <param name="rootBounds">The bounds of the root element.</param>
    /// <param name="x">The X coordinate to test.</param>
    /// <param name="y">The Y coordinate to test.</param>
    /// <returns>The topmost focusable element at the coordinates, or null if none.</returns>
    public IFocusable? HitTest(IElement rootElement, Rect rootBounds, int x, int y)
    {
        // Collect all focusable elements with their bounds
        var focusableElements = CollectFocusableElements(rootElement, rootBounds);
        
        // Find all elements that contain the point (later elements are on top)
        IFocusable? hitElement = null;
        
        foreach (var (element, bounds) in focusableElements)
        {
            if (bounds.Contains(x, y))
                hitElement = element;
        }
        
        return hitElement;
    }
    
    /// <summary>
    /// Handles a mouse event by dispatching it to the appropriate element.
    /// </summary>
    /// <param name="rootElement">The root element to search for hit targets.</param>
    /// <param name="rootBounds">The bounds of the root element.</param>
    /// <param name="mouseEvent">The mouse event to handle.</param>
    public void HandleMouseEvent(IElement rootElement, Rect rootBounds, MouseEvent mouseEvent)
    {
        // Update hover state on mouse move
        if (mouseEvent.Action == MouseAction.Move)
        {
            UpdateHover(rootElement, rootBounds, mouseEvent.X, mouseEvent.Y);
        }
        
        // Find element to receive the event
        var targetElement = HitTest(rootElement, rootBounds, mouseEvent.X, mouseEvent.Y);
        
        // Focus element on left mouse button press
        if (mouseEvent.Action == MouseAction.Press && mouseEvent.Button == MouseButton.Left)
        {
            SetFocus(targetElement);
        }
        
        // Dispatch event to target element
        targetElement?.OnMouseEvent(mouseEvent);
    }
    
    /// <summary>
    /// Handles a keyboard event by dispatching it to the focused element.
    /// </summary>
    /// <param name="keyEvent">The keyboard event to handle.</param>
    public void HandleKeyEvent(KeyEvent keyEvent)
    {
        _focusedElement?.OnKeyEvent(keyEvent);
    }
    
    /// <summary>
    /// Recursively collects all focusable elements from the element tree.
    /// </summary>
    private List<(IFocusable Element, Rect Bounds)> CollectFocusableElements(IElement element, Rect parentBounds)
    {
        var result = new List<(IFocusable, Rect)>();
        
        // Check if this element is focusable
        if (element is IFocusable focusable && focusable.CanFocus)
        {
            var bounds = element.CalculateBounds(parentBounds);
            result.Add((focusable, bounds));
        }
        
        // Recursively search children in Stack elements
        if (element is Stack stack)
        {
            var stackBounds = element.CalculateBounds(parentBounds);
            
            // Mirror the layout logic from Stack.Render() to calculate actual child positions
            var childSizes = stack.CalculateChildSizes(stackBounds);
            var position = stack.Orientation == StackOrientation.Horizontal ? stackBounds.X : stackBounds.Y;
            
            for (var i = 0; i < stack.Children.Count; i++)
            {
                var child = stack.Children[i];
                var size = childSizes[i];
                
                if (size <= 0) continue; // Skip zero-size children
                
                var childBounds = stack.CreateChildBounds(stackBounds, position, size);
                var childResults = CollectFocusableElements(child.Element, childBounds);
                result.AddRange(childResults);
                
                position += size;
            }
        }
        
        // Recursively search children in Rectangle elements
        if (element is Rectangle rectangle && rectangle.Child is not null)
        {
            var rectBounds = element.CalculateBounds(parentBounds);
            // Calculate inner bounds (subtract border if present) - same as Rectangle.Render()
            var innerBounds = rectangle.Border.HasBorder && rectBounds.Width >= 2 && rectBounds.Height >= 2
                ? new Rect(rectBounds.X + 1, rectBounds.Y + 1, rectBounds.Width - 2, rectBounds.Height - 2)
                : rectBounds;
            var childResults = CollectFocusableElements(rectangle.Child, innerBounds);
            result.AddRange(childResults);
        }
        
        return result;
    }
}
