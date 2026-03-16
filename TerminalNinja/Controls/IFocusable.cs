using TerminalNinja.Input;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Defines the contract for UI controls that can receive keyboard and mouse focus.
/// Implemented by controls that inherit from UIElement (or deeper) and want to
/// participate in keyboard/mouse focus management.
/// </summary>
public interface IFocusable
{
    /// <summary>
    /// Gets or sets whether this control currently has keyboard focus.
    /// Managed by FocusManager - controls should not set this directly.
    /// </summary>
    bool IsFocused { get; set; }
    
    /// <summary>
    /// Gets or sets whether the mouse is currently hovering over this control.
    /// Managed by FocusManager - controls should not set this directly.
    /// </summary>
    bool IsHovered { get; set; }
    
    /// <summary>
    /// Gets whether this control can receive keyboard focus.
    /// </summary>
    bool CanFocus { get; }
    
    /// <summary>
    /// Gets the tab order index for keyboard navigation.
    /// Lower values receive focus first. Controls with the same TabIndex
    /// are focused in the order they appear in the control tree.
    /// </summary>
    int TabIndex { get; }
    
    /// <summary>
    /// Called when this control receives keyboard focus.
    /// </summary>
    void OnFocus();
    
    /// <summary>
    /// Called when this control loses keyboard focus.
    /// </summary>
    void OnBlur();
    
    /// <summary>
    /// Called when the mouse cursor enters this control's bounds.
    /// </summary>
    void OnMouseEnter();
    
    /// <summary>
    /// Called when the mouse cursor leaves this control's bounds.
    /// </summary>
    void OnMouseLeave();
    
    /// <summary>
    /// Handles keyboard input when this control has focus.
    /// </summary>
    /// <param name="e">The keyboard event data.</param>
    void OnKeyEvent(KeyEvent e);
    
    /// <summary>
    /// Handles mouse events that occur within this control's bounds.
    /// </summary>
    /// <param name="e">The mouse event data.</param>
    void OnMouseEvent(MouseEvent e);
    
    /// <summary>
    /// Tests if the specified point (in absolute screen coordinates) is within this control's bounds.
    /// </summary>
    /// <param name="x">The absolute X coordinate to test.</param>
    /// <param name="y">The absolute Y coordinate to test.</param>
    /// <param name="parentBounds">The parent container's bounds for calculating absolute position.</param>
    /// <returns>The control's absolute bounds if the point is inside, null otherwise.</returns>
    Rect? HitTest(int x, int y, Rect parentBounds);
}
