namespace TerminalNinja.Xaml.Binding;

/// <summary>
/// Describes the location of the binding source relative to the binding target.
/// </summary>
public enum RelativeSourceMode
{
    /// <summary>
    /// The binding source is the element itself (the target element is used as the source).
    /// Example: <c>{Binding Width, RelativeSource={RelativeSource Self}}</c>
    /// </summary>
    Self,

    /// <summary>
    /// The binding source is an ancestor in the visual tree of the specified <see cref="RelativeSource.AncestorType"/>.
    /// Example: <c>{Binding Title, RelativeSource={RelativeSource FindAncestor, AncestorType=Window}}</c>
    /// </summary>
    FindAncestor,

    /// <summary>
    /// The binding source is the control whose template contains this element.
    /// Reserved for future use — the template system is not yet implemented.
    /// </summary>
    TemplatedParent
}
