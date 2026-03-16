using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Implemented by controls that contain child controls and know how to compute
/// the parent bounds each child receives during layout.
/// FocusManager uses this to traverse the full control tree for hit testing and tab order
/// without duplicating layout logic.
/// </summary>
public interface IChildContainer
{
    /// <summary>
    /// Enumerates each direct child together with the bounds that should be passed
    /// as <c>parentBounds</c> when the child calls <see cref="IControl.CalculateBounds"/>
    /// or <see cref="IControl.Render"/>.
    /// </summary>
    /// <param name="myBounds">The resolved bounds of this container (after its own CalculateBounds).</param>
    IEnumerable<(IControl Child, Rect ChildParentBounds)> GetChildrenWithBounds(Rect myBounds);
}
