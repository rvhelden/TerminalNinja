namespace TerminalNinja.Controls;

/// <summary>
/// Specifies the display state of a UI element.
/// </summary>
public enum Visibility
{
    /// <summary>
    /// Display the element and include it in layout.
    /// </summary>
    Visible,

    /// <summary>
    /// Do not display the element, but reserve space for it in layout.
    /// </summary>
    Hidden,

    /// <summary>
    /// Do not display the element and do not reserve space for it in layout.
    /// </summary>
    Collapsed
}
