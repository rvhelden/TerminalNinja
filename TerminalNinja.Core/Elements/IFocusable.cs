using TerminalNinja.Core.Input;
using TerminalNinja.Core.Primitives;

namespace TerminalNinja.Core.Elements;

/// <summary>
/// Defines the contract for UI elements that can receive keyboard and mouse focus.
/// </summary>
public interface IFocusable : IElement
{
    /// <summary>
    /// Gets or sets whether this element currently has keyboard focus.
    /// Managed by FocusManager - elements should not set this directly.
    /// </summary>
    bool IsFocused { get; set; }
    
    /// <summary>
    /// Gets or sets whether the mouse is currently hovering over this element.
    /// Managed by FocusManager - elements should not set this directly.
    /// </summary>
    bool IsHovered { get; set; }
    
    /// <summary>
    /// Gets whether this element can receive keyboard focus.
    /// </summary>
    bool CanFocus { get; }
    
    /// <summary>
    /// Gets the tab order index for keyboard navigation.
    /// Lower values receive focus first. Elements with the same TabIndex
    /// are focused in the order they appear in the element tree.
    /// </summary>
    int TabIndex { get; }
    
    /// <summary>
    /// Called when this element receives keyboard focus.
    /// </summary>
    void OnFocus();
    
    /// <summary>
    /// Called when this element loses keyboard focus.
    /// </summary>
    void OnBlur();
    
    /// <summary>
    /// Called when the mouse cursor enters this element's bounds.
    /// </summary>
    void OnMouseEnter();
    
    /// <summary>
    /// Called when the mouse cursor leaves this element's bounds.
    /// </summary>
    void OnMouseLeave();
    
    /// <summary>
    /// Handles keyboard input when this element has focus.
    /// </summary>
    /// <param name="e">The keyboard event data.</param>
    void OnKeyEvent(KeyEvent e);
    
    /// <summary>
    /// Handles mouse events that occur within this element's bounds.
    /// </summary>
    /// <param name="e">The mouse event data.</param>
    void OnMouseEvent(MouseEvent e);
    
    /// <summary>
    /// Tests if the specified point (in absolute screen coordinates) is within this element's bounds.
    /// </summary>
    /// <param name="x">The absolute X coordinate to test.</param>
    /// <param name="y">The absolute Y coordinate to test.</param>
    /// <param name="parentBounds">The parent container's bounds for calculating absolute position.</param>
    /// <returns>The element's absolute bounds if the point is inside, null otherwise.</returns>
    Rect? HitTest(int x, int y, Rect parentBounds);
}
