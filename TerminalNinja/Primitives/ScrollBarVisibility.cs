namespace TerminalNinja.Primitives;

/// <summary>
/// Specifies the visibility of a scroll bar within a <see cref="Controls.ScrollViewer"/>.
/// Matches WPF's System.Windows.Controls.ScrollBarVisibility.
/// </summary>
public enum ScrollBarVisibility : byte
{
    /// <summary>
    /// Scrolling is disabled. Content is clipped to the viewport.
    /// </summary>
    Disabled = 0,

    /// <summary>
    /// The scroll indicator appears only when content exceeds the viewport.
    /// </summary>
    Auto = 1,

    /// <summary>
    /// Scrolling is enabled but no indicator is shown.
    /// </summary>
    Hidden = 2,

    /// <summary>
    /// The scroll indicator is always shown.
    /// </summary>
    Visible = 3
}
