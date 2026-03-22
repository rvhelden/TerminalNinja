namespace TerminalNinja.Primitives;

/// <summary>
/// Represents a color in the OKLCH color space.
/// </summary>
/// <param name="L">Perceptual lightness component, typically in range [0, 1].</param>
/// <param name="C">Chroma component, typically in range [0, ~0.4] for sRGB gamut.</param>
/// <param name="H">Hue angle in degrees.</param>
public readonly record struct Oklch(double L, double C, double H)
{
    /// <summary>
    /// Converts this OKLCH value to a <see cref="Color"/>.
    /// </summary>
    /// <param name="alpha">Alpha channel (0-255).</param>
    public Color ToColor(byte alpha = 255) => Color.FromOklch(this, alpha);

    /// <summary>
    /// Converts this OKLCH value to a packed ARGB value (0xAARRGGBB).
    /// </summary>
    /// <param name="alpha">Alpha channel (0-255).</param>
    public uint ToArgb(byte alpha = 255) => ToColor(alpha).ToArgb();

    /// <summary>
    /// Creates an OKLCH color from a <see cref="Color"/>.
    /// </summary>
    public static Oklch FromColor(Color color) => color.ToOklch();

    /// <summary>
    /// Creates an OKLCH color from a packed ARGB value (0xAARRGGBB).
    /// </summary>
    /// <param name="argb">Packed ARGB value in 0xAARRGGBB order.</param>
    public static Oklch FromArgb(uint argb) => Color.FromArgb(argb).ToOklch();
}
