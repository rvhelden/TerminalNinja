namespace TerminalNinja.Skia;

/// <summary>
/// Configuration for a <see cref="SkiaApplication"/> host.
/// </summary>
public sealed class SkiaApplicationOptions
{
    /// <summary>Window title shown in the OS chrome.</summary>
    public string Title { get; init; } = "TerminalNinja";

    /// <summary>
    /// Base pixel width of a single terminal cell at 100% display scale. The host multiplies
    /// this by the queried display scale (when <see cref="AutoScale"/> is true) and rounds
    /// to the nearest integer to keep cell origins on the pixel grid.
    /// </summary>
    public int CellWidth { get; init; } = 9;

    /// <summary>
    /// Base pixel height of a single terminal cell at 100% display scale. See <see cref="CellWidth"/>.
    /// </summary>
    public int CellHeight { get; init; } = 18;

    /// <summary>Initial cell-grid width.</summary>
    public int CellsWide { get; init; } = 80;

    /// <summary>Initial cell-grid height.</summary>
    public int CellsTall { get; init; } = 24;

    /// <summary>
    /// Base font pixel size at 100% display scale. Scaled and rounded to integer pixels
    /// alongside <see cref="CellWidth"/> / <see cref="CellHeight"/> when <see cref="AutoScale"/>
    /// is true. Should match <see cref="CellHeight"/> minus line-gap room.
    /// </summary>
    public float FontPixelSize { get; init; } = 14f;

    /// <summary>
    /// When true (default) the host queries the window's display scale via
    /// <c>SDL_GetWindowDisplayScale</c> and applies it to cell metrics + font size so text
    /// renders crisply on HiDPI displays (1.5×, 2.0×, etc.) and re-renders cleanly when the
    /// window moves to a display with different scale. Set false for hand-tuned setups
    /// that already provide post-scale pixel values.
    /// </summary>
    public bool AutoScale { get; init; } = true;

    /// <summary>
    /// Family name of the font to load. Resolved by Skia's font manager; passing null
    /// falls back to <see cref="SkiaSharp.SKTypeface.Default"/>.
    /// </summary>
    public string? FontFamily { get; init; }

    /// <summary>Enable VSync via SDL_GL_SetSwapInterval(1). Default true.</summary>
    public bool VSync { get; init; } = true;

    /// <summary>
    /// When true, pressing Escape exits the run loop. Convenient for samples and debugging;
    /// disable in production apps that need Escape to reach focused controls.
    /// </summary>
    public bool EscapeQuits { get; init; } = true;

    /// <summary>
    /// When true, mouse tracking is enabled at startup. Toggleable at runtime via the
    /// host's input backend.
    /// </summary>
    public bool EnableMouseTracking { get; init; } = true;

    /// <summary>
    /// When true, Tab / Shift+Tab is intercepted by the host to advance focus via
    /// <see cref="Input.FocusManager"/>. Set false when focused controls need raw Tab
    /// (e.g. a TextBox that inserts tab characters).
    /// </summary>
    public bool EnableTabNavigation { get; init; } = true;
}
