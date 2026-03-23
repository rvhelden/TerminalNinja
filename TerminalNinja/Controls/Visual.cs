using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Base class for all objects that have a visual representation in the terminal UI.
/// Provides visual tree management (parent/child relationships) and replaces the
/// former <c>IChildContainer</c> interface with a virtual method.
/// </summary>
public abstract class Visual : DependencyObject
{
    private Visual? _parent;

    /// <summary>
    /// Gets or sets the parent visual in the visual tree.
    /// Setting this property calls <see cref="OnVisualParentChanged"/> on the child,
    /// allowing subclasses (e.g., <see cref="FrameworkElement"/>) to react to reparenting
    /// (for example, to re-evaluate <c>RelativeSource FindAncestor</c> bindings).
    /// </summary>
    public Visual? Parent
    {
        get => _parent;
        set
        {
            if (ReferenceEquals(_parent, value))
            {
                return;
            }

            var oldParent = _parent;
            _parent = value;
            OnVisualParentChanged(oldParent);
        }
    }

    /// <summary>
    /// Called when this visual's <see cref="Parent"/> changes.
    /// Override in subclasses to react to reparenting (e.g., re-evaluate ancestor bindings).
    /// </summary>
    /// <param name="oldParent">The previous parent, or <c>null</c> if this visual was not previously parented.</param>
    protected virtual void OnVisualParentChanged(Visual? oldParent) { }

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
