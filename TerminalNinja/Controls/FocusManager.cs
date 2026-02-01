using TerminalNinja.Input;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Manages keyboard focus and mouse hover state for focusable controls in the UI tree.
/// </summary>
public sealed class FocusManager
{
    private IFocusable? _focusedControl;
    private IFocusable? _hoveredControl;
    
    /// <summary>
    /// Gets the currently focused control, or null if no control has focus.
    /// </summary>
    public IFocusable? FocusedControl => _focusedControl;
    
    /// <summary>
    /// Gets the currently hovered control, or null if no control is hovered.
    /// </summary>
    public IFocusable? HoveredControl => _hoveredControl;
    
    /// <summary>
    /// Sets focus to the specified control. Pass null to clear focus.
    /// </summary>
    /// <param name="control">The control to focus, or null to clear focus.</param>
    public void SetFocus(IFocusable? control)
    {
        if (_focusedControl == control)
            return;
        
        // Blur previous control
        if (_focusedControl is not null)
        {
            _focusedControl.IsFocused = false;
            _focusedControl.OnBlur();
        }
        
        _focusedControl = control;
        
        // Focus new control
        if (_focusedControl is not null)
        {
            _focusedControl.IsFocused = true;
            _focusedControl.OnFocus();
        }
    }
    
    /// <summary>
    /// Clears keyboard focus from the current control.
    /// </summary>
    public void ClearFocus() => SetFocus(null);
    
    /// <summary>
    /// Moves focus to the next focusable control in tab order.
    /// </summary>
    /// <param name="rootControl">The root control to search for focusable children.</param>
    /// <param name="rootBounds">The bounds of the root control.</param>
    public void FocusNext(IControl rootControl, Rect rootBounds)
    {
        var focusableElements = CollectFocusableControls(rootControl, rootBounds)
            .OrderBy(x => x.Control.TabIndex)
            .ThenBy(x => x.Bounds.Y)
            .ThenBy(x => x.Bounds.X)
            .ToList();
        
        if (focusableElements.Count == 0)
            return;
        
        if (_focusedControl is null)
        {
            SetFocus(focusableElements[0].Control);
            return;
        }
        
        var currentIndex = focusableElements.FindIndex(x => x.Control == _focusedControl);
        if (currentIndex == -1)
        {
            SetFocus(focusableElements[0].Control);
            return;
        }
        
        var nextIndex = (currentIndex + 1) % focusableElements.Count;
        SetFocus(focusableElements[nextIndex].Control);
    }
    
    /// <summary>
    /// Moves focus to the previous focusable control in tab order.
    /// </summary>
    /// <param name="rootControl">The root control to search for focusable children.</param>
    /// <param name="rootBounds">The bounds of the root control.</param>
    public void FocusPrevious(IControl rootControl, Rect rootBounds)
    {
        var focusableElements = CollectFocusableControls(rootControl, rootBounds)
            .OrderBy(x => x.Control.TabIndex)
            .ThenBy(x => x.Bounds.Y)
            .ThenBy(x => x.Bounds.X)
            .ToList();
        
        if (focusableElements.Count == 0)
            return;
        
        if (_focusedControl is null)
        {
            SetFocus(focusableElements[^1].Control);
            return;
        }
        
        var currentIndex = focusableElements.FindIndex(x => x.Control == _focusedControl);
        if (currentIndex == -1)
        {
            SetFocus(focusableElements[^1].Control);
            return;
        }
        
        var prevIndex = currentIndex == 0 ? focusableElements.Count - 1 : currentIndex - 1;
        SetFocus(focusableElements[prevIndex].Control);
    }
    
    /// <summary>
    /// Updates hover state based on mouse position.
    /// </summary>
    /// <param name="rootControl">The root control to search for focusable children.</param>
    /// <param name="rootBounds">The bounds of the root control.</param>
    /// <param name="mouseX">The mouse X coordinate.</param>
    /// <param name="mouseY">The mouse Y coordinate.</param>
    /// <returns>The control under the mouse cursor, or null if none.</returns>
    public IFocusable? UpdateHover(IControl rootControl, Rect rootBounds, int mouseX, int mouseY)
    {
        var hitElement = HitTest(rootControl, rootBounds, mouseX, mouseY);
        
        if (_hoveredControl == hitElement)
            return hitElement;
        
        // Leave previous control
        if (_hoveredControl is not null)
        {
            _hoveredControl.IsHovered = false;
            _hoveredControl.OnMouseLeave();
        }
        
        _hoveredControl = hitElement;
        
        // Enter new control
        if (_hoveredControl is not null)
        {
            _hoveredControl.IsHovered = true;
            _hoveredControl.OnMouseEnter();
        }
        
        return hitElement;
    }
    
    /// <summary>
    /// Performs hit testing to find the focusable control at the specified coordinates.
    /// </summary>
    /// <param name="rootControl">The root control to search.</param>
    /// <param name="rootBounds">The bounds of the root control.</param>
    /// <param name="x">The X coordinate to test.</param>
    /// <param name="y">The Y coordinate to test.</param>
    /// <returns>The topmost focusable control at the coordinates, or null if none.</returns>
    public IFocusable? HitTest(IControl rootControl, Rect rootBounds, int x, int y)
    {
        // Collect all focusable elements with their bounds
        var focusableElements = CollectFocusableControls(rootControl, rootBounds);
        
        // Find all elements that contain the point (later elements are on top)
        IFocusable? hitElement = null;
        
        foreach (var (control, bounds) in focusableElements)
        {
            if (bounds.Contains(x, y))
                hitElement = control;
        }
        
        return hitElement;
    }
    
    /// <summary>
    /// Handles a mouse event by dispatching it to the appropriate control.
    /// </summary>
    /// <param name="rootControl">The root control to search for hit targets.</param>
    /// <param name="rootBounds">The bounds of the root control.</param>
    /// <param name="mouseEvent">The mouse event to handle.</param>
    public void HandleMouseEvent(IControl rootControl, Rect rootBounds, MouseEvent mouseEvent)
    {
        // Update hover state on mouse move
        if (mouseEvent.Action == MouseAction.Move)
        {
            UpdateHover(rootControl, rootBounds, mouseEvent.X, mouseEvent.Y);
        }
        
        // Find control to receive the event
        var targetElement = HitTest(rootControl, rootBounds, mouseEvent.X, mouseEvent.Y);
        
        // Focus control on left mouse button press
        if (mouseEvent.Action == MouseAction.Press && mouseEvent.Button == MouseButton.Left)
        {
            SetFocus(targetElement);
        }
        
        // Dispatch event to target control
        targetElement?.OnMouseEvent(mouseEvent);
    }
    
    /// <summary>
    /// Handles a keyboard event by dispatching it to the focused control.
    /// </summary>
    /// <param name="keyEvent">The keyboard event to handle.</param>
    public void HandleKeyEvent(KeyEvent keyEvent)
    {
        _focusedControl?.OnKeyEvent(keyEvent);
    }
    
    /// <summary>
    /// Recursively collects all focusable elements from the control tree.
    /// </summary>
    private List<(IFocusable Control, Rect Bounds)> CollectFocusableControls(IControl control, Rect parentBounds)
    {
        var result = new List<(IFocusable, Rect)>();
        
        // Check if this control is focusable
        if (control is IFocusable focusable && focusable.CanFocus)
        {
            var bounds = control.CalculateBounds(parentBounds);
            result.Add((focusable, bounds));
        }
        
        // Recursively search children in Stack elements
        if (control is Stack stack)
        {
            var stackBounds = control.CalculateBounds(parentBounds);
            
            // Mirror the layout logic from Stack.Render() to calculate actual child positions
            var childSizes = stack.CalculateChildSizes(stackBounds);
            var position = stack.Orientation == StackOrientation.Horizontal ? stackBounds.X : stackBounds.Y;
            
            for (var i = 0; i < stack.Children.Count; i++)
            {
                var child = stack.Children[i];
                var size = childSizes[i];
                
                if (size <= 0) continue; // Skip zero-size children
                
                var childBounds = stack.CreateChildBounds(stackBounds, position, size);
                var childResults = CollectFocusableControls(child.Content, childBounds);
                result.AddRange(childResults);
                
                position += size;
            }
        }
        
        // Recursively search children in Rectangle elements
        if (control is Rectangle rectangle && rectangle.Child is not null)
        {
            var rectBounds = control.CalculateBounds(parentBounds);
            // Calculate inner bounds (subtract border if present) - same as Rectangle.Render()
            var innerBounds = rectangle.Border.HasBorder && rectBounds.Width >= 2 && rectBounds.Height >= 2
                ? new Rect(rectBounds.X + 1, rectBounds.Y + 1, rectBounds.Width - 2, rectBounds.Height - 2)
                : rectBounds;
            var childResults = CollectFocusableControls(rectangle.Child, innerBounds);
            result.AddRange(childResults);
        }
        
        return result;
    }
}
