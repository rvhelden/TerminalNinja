using TerminalNinja.Core.Buffers;
using TerminalNinja.Core.Primitives;

namespace TerminalNinja.Core.Elements;

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
