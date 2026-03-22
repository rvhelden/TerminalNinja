using TerminalNinja.Input;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Manages keyboard focus and mouse hover state for focusable elements in the UI tree.
/// Works directly with <see cref="UIElement"/> — no separate interface needed.
/// </summary>
public sealed class FocusManager
{
    /// <summary>
    /// Gets the currently focused element, or null if no element has focus.
    /// </summary>
    public UIElement? FocusedElement { get; private set; }

    /// <summary>
    /// Gets the currently hovered element, or null if no element is hovered.
    /// </summary>
    public UIElement? HoveredElement { get; private set; }

    /// <summary>
    /// Sets focus to the specified element. Pass null to clear focus.
    /// </summary>
    /// <param name="element">The element to focus, or null to clear focus.</param>
    public void SetFocus(UIElement? element)
    {
        if (FocusedElement == element)
        {
            return;
        }

        // Blur previous element
        if (FocusedElement is not null)
        {
            FocusedElement.IsFocused = false;
            FocusedElement.OnLostFocus();
        }
        
        FocusedElement = element;
        
        // Focus new element
        if (FocusedElement is not null)
        {
            FocusedElement.IsFocused = true;
            FocusedElement.OnGotFocus();
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
        {
            return;
        }

        if (FocusedElement is null)
        {
            SetFocus(focusableElements[0].Element);
            return;
        }
        
        var currentIndex = focusableElements.FindIndex(x => x.Element == FocusedElement);
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
        {
            return;
        }

        if (FocusedElement is null)
        {
            SetFocus(focusableElements[^1].Element);
            return;
        }
        
        var currentIndex = focusableElements.FindIndex(x => x.Element == FocusedElement);
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
        
        if (HoveredElement == hitElement)
        {
            return hitElement;
        }

        // Leave previous element
        if (HoveredElement is not null)
        {
            HoveredElement.IsMouseOver = false;
            HoveredElement.OnMouseLeave();
        }
        
        HoveredElement = hitElement;
        
        // Enter new element
        if (HoveredElement is not null)
        {
            HoveredElement.IsMouseOver = true;
            HoveredElement.OnMouseEnter();
        }
        
        return hitElement;
    }
    
    /// <summary>
    /// Performs hit testing to find the focusable element at the specified coordinates.
    /// Uses spatial pruning to skip subtrees whose bounds do not contain the point.
    /// </summary>
    /// <param name="rootControl">The root element to search.</param>
    /// <param name="rootBounds">The bounds of the root element.</param>
    /// <param name="x">The X coordinate to test.</param>
    /// <param name="y">The Y coordinate to test.</param>
    /// <returns>The topmost focusable element at the coordinates, or null if none.</returns>
    public UIElement? HitTest(UIElement rootControl, Rect rootBounds, int x, int y)
    {
        return HitTestFocusable(rootControl, rootBounds, x, y);
    }
    
    /// <summary>
    /// Handles a mouse event by dispatching it to the appropriate element.
    /// Mouse events are delivered to the deepest element at the mouse position
    /// (regardless of <see cref="UIElement.Focusable"/>). Focus is only transferred
    /// to the nearest focusable ancestor.
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
        
        // Find the deepest element at the mouse position (any element, not just focusable)
        var targetElement = HitTestDeep(rootControl, rootBounds, mouseEvent.X, mouseEvent.Y);
        
        // Focus the nearest focusable ancestor on left mouse button press
        if (mouseEvent.Action == MouseAction.Press && mouseEvent.Button == MouseButton.Left)
        {
            var focusTarget = FindFocusableAncestorOrSelf(targetElement);
            SetFocus(focusTarget);
        }
        
        // Bubble the event from the deepest target up through ancestors.
        // This mirrors WPF's bubbling routed event strategy — every element
        // in the ancestor chain gets a chance to handle the event.
        BubbleMouseEvent(targetElement, mouseEvent);
    }

    /// <summary>
    /// Dispatches a mouse event to the target element and then walks up the
    /// <see cref="Visual.Parent"/> chain, giving each ancestor a chance to handle it.
    /// </summary>
    private static void BubbleMouseEvent(UIElement? target, MouseEvent mouseEvent)
    {
        var current = target as Visual;
        while (current != null)
        {
            if (current is UIElement uie)
            {
                uie.OnMouseEvent(mouseEvent);
            }

            current = current.Parent;
        }
    }
    
    /// <summary>
    /// Handles a keyboard event by dispatching it to the focused element.
    /// </summary>
    /// <param name="keyEvent">The keyboard event to handle.</param>
    public void HandleKeyEvent(KeyEvent keyEvent)
    {
        FocusedElement?.OnKeyEvent(keyEvent);
    }
    
    /// <summary>
    /// Recursively collects all focusable elements from the UI tree
    /// using the Visual.GetChildrenWithBounds traversal.
    /// </summary>
    private List<(UIElement Element, Rect Bounds)> CollectFocusableElements(UIElement element, Rect parentBounds)
    {
        var result = new List<(UIElement, Rect)>();
        CollectFocusableElementsCore(element, parentBounds, result);
        return result;
    }

    private static void CollectFocusableElementsCore(UIElement element, Rect parentBounds,
        List<(UIElement Element, Rect Bounds)> result)
    {
        // Prune invisible or disabled subtrees — they cannot receive focus
        if (element.Visibility != Visibility.Visible || !element.IsEnabled)
        {
            return;
        }

        var myBounds = element.CalculateBounds(parentBounds);

        if (element.Focusable)
        {
            result.Add((element, myBounds));
        }

        foreach (var (child, childParentBounds) in element.GetChildrenWithBounds(myBounds))
        {
            if (child is UIElement childElement)
            {
                CollectFocusableElementsCore(childElement, childParentBounds, result);
            }
        }
    }

    /// <summary>
    /// Performs a deep hit test that finds the leaf-most UIElement at the given
    /// coordinates, regardless of whether it is focusable.
    /// </summary>
    private UIElement? HitTestDeep(UIElement element, Rect parentBounds, int x, int y)
    {
        // Prune invisible or disabled subtrees
        if (element.Visibility != Visibility.Visible || !element.IsEnabled)
        {
            return null;
        }

        var myBounds = element.CalculateBounds(parentBounds);

        if (!myBounds.Contains(x, y))
        {
            return null;
        }

        // Recurse into children (last child wins — later children are visually on top)
        UIElement? deepest = null;
        foreach (var (child, childParentBounds) in element.GetChildrenWithBounds(myBounds))
        {
            if (child is UIElement childElement)
            {
                var hit = HitTestDeep(childElement, childParentBounds, x, y);
                if (hit != null)
                {
                    deepest = hit;
                }
            }
        }

        return deepest ?? element;
    }

    /// <summary>
    /// Performs a spatial-pruning hit test that finds the deepest focusable element at
    /// the given coordinates without collecting all focusable elements into a list.
    /// Skips invisible and disabled subtrees.
    /// </summary>
    private UIElement? HitTestFocusable(UIElement element, Rect parentBounds, int x, int y)
    {
        // Prune invisible or disabled subtrees
        if (element.Visibility != Visibility.Visible || !element.IsEnabled)
        {
            return null;
        }

        var myBounds = element.CalculateBounds(parentBounds);

        if (!myBounds.Contains(x, y))
        {
            return null;
        }

        // Recurse into children (last child wins — later children are visually on top)
        UIElement? deepest = null;
        foreach (var (child, childParentBounds) in element.GetChildrenWithBounds(myBounds))
        {
            if (child is UIElement childElement)
            {
                var hit = HitTestFocusable(childElement, childParentBounds, x, y);
                if (hit != null)
                {
                    deepest = hit;
                }
            }
        }

        if (deepest != null)
        {
            return deepest;
        }

        return element.Focusable ? element : null;
    }

    /// <summary>
    /// Walks up the parent chain from the given element to find the nearest
    /// element with <see cref="UIElement.Focusable"/> set to <c>true</c>.
    /// Returns null if no focusable ancestor exists.
    /// </summary>
    private static UIElement? FindFocusableAncestorOrSelf(UIElement? element)
    {
        var current = element as Visual;
        while (current != null)
        {
            if (current is UIElement uie && uie.Focusable)
            {
                return uie;
            }

            current = current.Parent;
        }
        return null;
    }
}
