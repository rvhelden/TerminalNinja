namespace TerminalNinja.Primitives;

/// <summary>
/// Describes how content is resized to fill its allocated space.
/// Used by <see cref="Controls.Image"/> to control image scaling.
/// Matches WPF's System.Windows.Media.Stretch.
/// </summary>
public enum Stretch : byte
{
    /// <summary>Content is rendered at its original size, no scaling.</summary>
    None = 0,

    /// <summary>Content is stretched to fill the bounds. Aspect ratio may not be preserved.</summary>
    Fill = 1,

    /// <summary>Content is scaled to fit within the bounds while preserving aspect ratio. May have empty space.</summary>
    Uniform = 2,

    /// <summary>Content is scaled to fill the bounds while preserving aspect ratio. May be cropped.</summary>
    UniformToFill = 3
}
