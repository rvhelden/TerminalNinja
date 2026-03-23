namespace TerminalNinja.Primitives;

/// <summary>
/// Specifies the position of a <see cref="Controls.Popup"/> relative to its placement target.
/// </summary>
public enum PlacementMode
{
    /// <summary>
    /// Position below the placement target's bottom edge, left-aligned.
    /// </summary>
    Bottom,

    /// <summary>
    /// Position above the placement target's top edge, left-aligned.
    /// </summary>
    Top,

    /// <summary>
    /// Position to the right of the placement target's right edge, top-aligned.
    /// </summary>
    Right,

    /// <summary>
    /// Position to the left of the placement target's left edge, top-aligned.
    /// </summary>
    Left,

    /// <summary>
    /// Center the popup over the placement target.
    /// </summary>
    Center,

    /// <summary>
    /// Position relative to the top-left corner of the placement target.
    /// Use HorizontalOffset and VerticalOffset for exact positioning.
    /// </summary>
    Relative,

    /// <summary>
    /// Position at absolute coordinates within the viewport.
    /// Uses HorizontalOffset as X and VerticalOffset as Y.
    /// </summary>
    Absolute
}
