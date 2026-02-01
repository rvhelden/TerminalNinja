using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Elements;

/// <summary>
/// Defines the contract for a UI element that can be rendered to a buffer.
/// </summary>
public interface IElement
{
    /// <summary>
    /// Gets or sets the name of this element for lookup purposes (e.g., XAML x:Name).
    /// </summary>
    string? Name { get; set; }
    
    /// <summary>
    /// Gets or sets the data context for this element. Used as the source for data bindings.
    /// If null, bindings will walk up the Parent chain to find an inherited DataContext.
    /// </summary>
    object? DataContext { get; set; }
    
    /// <summary>
    /// Gets or sets the parent element in the visual tree. Used for DataContext inheritance.
    /// </summary>
    IElement? Parent { get; set; }
    
    /// <summary>
    /// Gets or sets the callback invoked when this element needs to be re-rendered.
    /// Set by the Application when the element joins the visual tree.
    /// </summary>
    Action? InvalidationCallback { get; set; }
    
    /// <summary>
    /// Signals that this element's visual state has changed and needs re-rendering.
    /// </summary>
    void InvalidateVisual();
    
    /// <summary>
    /// Returns the element's preferred size within the given parent bounds.
    /// Used by layout containers to determine Auto-sized children.
    /// </summary>
    /// <param name="parent">The parent container bounds.</param>
    /// <returns>The preferred width and height in cells.</returns>
    Size2D GetPreferredSize(Rect parent);
    
    /// <summary>
    /// Calculates the absolute bounds of this element within the parent bounds.
    /// </summary>
    /// <param name="parent">The parent container bounds.</param>
    /// <returns>The calculated absolute rectangle bounds.</returns>
    Rect CalculateBounds(Rect parent);
    
    /// <summary>
    /// Renders this element to the specified cell buffer.
    /// </summary>
    /// <param name="buffer">The buffer to render into.</param>
    /// <param name="parentBounds">The bounds of the parent container.</param>
    void Render(CellBuffer buffer, Rect parentBounds);
}
