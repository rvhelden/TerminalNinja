using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Base class for all objects that have a visual representation in the terminal UI.
/// Provides visual tree management (parent/child relationships) and replaces the
/// former <c>IChildContainer</c> interface with a virtual method.
/// </summary>
public abstract class Visual : DependencyObject
{
    /// <summary>
    /// Gets or sets the parent visual in the visual tree.
    /// </summary>
    public Visual? Parent { get; set; }

    /// <summary>
    /// Enumerates each direct child together with the bounds that should be passed
    /// as <c>parentBounds</c> when the child performs layout or rendering.
    /// The default implementation returns an empty enumerable (leaf node).
    /// </summary>
    /// <param name="myBounds">The resolved bounds of this visual (after its own CalculateBounds).</param>
    public virtual IEnumerable<(Visual Child, Rect ChildParentBounds)> GetChildrenWithBounds(Rect myBounds)
    {
        return [];
    }
}
