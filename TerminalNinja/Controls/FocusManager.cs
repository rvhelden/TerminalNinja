using TerminalNinja.Input;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Manages keyboard focus and mouse hover state for focusable elements in the UI tree.
/// Works directly with <see cref="UIElement"/> — no separate interface needed.
/// </summary>
public sealed class FocusManager
{
    private UIElement? _focusedElement;
    private UIElement? _hoveredElement;
    
    /// <summary>
    /// Gets the currently focused element, or null if no element has focus.
    /// </summary>
    public UIElement? FocusedElement => _focusedElement;
    
    /// <summary>
    /// Gets the currently hovered element, or null if no element is hovered.
    /// </summary>
    public UIElement? HoveredElement => _hoveredElement;
    
    /// <summary>
    /// Sets focus to the specified element. Pass null to clear focus.
    /// </summary>
    /// <param name="element">The element to focus, or null to clear focus.</param>
    public void SetFocus(UIElement? element)
    {
        if (_focusedElement == element)
            return;
        
        // Blur previous element
        if (_focusedElement is not null)
        {
            _focusedElement.IsFocused = false;
            _focusedElement.OnLostFocus();
        }
        
        _focusedElement = element;
        
        // Focus new element
        if (_focusedElement is not null)
        {
            _focusedElement.IsFocused = true;
            _focusedElement.OnGotFocus();
        }
    }
    
    /// <summary>
    /// Clears keyboard focus from the current element.
    /// </summary>
    public void ClearFocus() => SetFocus(null);
    
    /// <summary>
    /// Moves focus to the next focusable element in tab order.
    /// </summary>
    /// <param name="rootControl">The root element to search for focusable children.</param>
    /// <param name="rootBounds">The bounds of the root element.</param>
    public void FocusNext(UIElement rootControl, Rect rootBounds)
    {
        var focusableElements = CollectFocusableElements(rootControl, rootBounds)
            .OrderBy(x => (x.Element as Control)?.TabIndex ?? 0)
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
    /// <param name="rootControl">The root element to search for focusable children.</param>
    /// <param name="rootBounds">The bounds of the root element.</param>
    public void FocusPrevious(UIElement rootControl, Rect rootBounds)
    {
        var focusableElements = CollectFocusableElements(rootControl, rootBounds)
            .OrderBy(x => (x.Element as Control)?.TabIndex ?? 0)
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
    /// <param name="rootControl">The root element to search for focusable children.</param>
    /// <param name="rootBounds">The bounds of the root element.</param>
    /// <param name="mouseX">The mouse X coordinate.</param>
    /// <param name="mouseY">The mouse Y coordinate.</param>
    /// <returns>The element under the mouse cursor, or null if none.</returns>
    public UIElement? UpdateHover(UIElement rootControl, Rect rootBounds, int mouseX, int mouseY)
    {
        var hitElement = HitTest(rootControl, rootBounds, mouseX, mouseY);
        
        if (_hoveredElement == hitElement)
            return hitElement;
        
        // Leave previous element
        if (_hoveredElement is not null)
        {
            _hoveredElement.IsMouseOver = false;
            _hoveredElement.OnMouseLeave();
        }
        
        _hoveredElement = hitElement;
        
        // Enter new element
        if (_hoveredElement is not null)
        {
            _hoveredElement.IsMouseOver = true;
            _hoveredElement.OnMouseEnter();
        }
        
        return hitElement;
    }
    
    /// <summary>
    /// Performs hit testing to find the focusable element at the specified coordinates.
    /// </summary>
    /// <param name="rootControl">The root element to search.</param>
    /// <param name="rootBounds">The bounds of the root element.</param>
    /// <param name="x">The X coordinate to test.</param>
    /// <param name="y">The Y coordinate to test.</param>
    /// <returns>The topmost focusable element at the coordinates, or null if none.</returns>
    public UIElement? HitTest(UIElement rootControl, Rect rootBounds, int x, int y)
    {
        // Collect all focusable elements with their bounds
        var focusableElements = CollectFocusableElements(rootControl, rootBounds);
        
        // Find all elements that contain the point (later elements are on top)
        UIElement? hitElement = null;
        
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
    /// <param name="rootControl">The root element to search for hit targets.</param>
    /// <param name="rootBounds">The bounds of the root element.</param>
    /// <param name="mouseEvent">The mouse event to handle.</param>
    public void HandleMouseEvent(UIElement rootControl, Rect rootBounds, MouseEvent mouseEvent)
    {
        // Update hover state on mouse move
        if (mouseEvent.Action == MouseAction.Move)
        {
            UpdateHover(rootControl, rootBounds, mouseEvent.X, mouseEvent.Y);
        }
        
        // Find element to receive the event
        var targetElement = HitTest(rootControl, rootBounds, mouseEvent.X, mouseEvent.Y);
        
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
    /// Recursively collects all focusable elements from the UI tree
    /// using the Visual.GetChildrenWithBounds traversal.
    /// </summary>
    private List<(UIElement Element, Rect Bounds)> CollectFocusableElements(UIElement element, Rect parentBounds)
    {
        var result = new List<(UIElement, Rect)>();

        var myBounds = element.CalculateBounds(parentBounds);

        if (element.Focusable)
            result.Add((element, myBounds));

        foreach (var (child, childParentBounds) in element.GetChildrenWithBounds(myBounds))
        {
            if (child is UIElement childElement)
                result.AddRange(CollectFocusableElements(childElement, childParentBounds));
        }

        return result;
    }
}
