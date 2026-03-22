using System.ComponentModel;
using System.Runtime.InteropServices;
using TerminalNinja.Xaml.TypeConverters;

namespace TerminalNinja.Primitives;

/// <summary>
/// Represents a 24-bit RGB color with an alpha channel for transparency.
/// A == 0 means fully transparent (do not paint); A == 255 (default) means fully opaque.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
[TypeConverter(typeof(ColorTypeConverter))]
public readonly record struct Color(byte R, byte G, byte B, byte A = 255)
{
    /// <summary>Returns true when this color is fully transparent (A == 0).</summary>
    public bool IsTransparent => A == 0;

    /// <summary>Gets the transparent color (no paint).</summary>
    public static readonly Color Transparent = new(0, 0, 0, 0);

    /// <summary>Gets the black color (0, 0, 0).</summary>
    public static readonly Color Black = new(0, 0, 0);

    /// <summary>Gets the white color (255, 255, 255).</summary>
    public static readonly Color White = new(255, 255, 255);
    
    /// <summary>Gets the red color (255, 0, 0).</summary>
    public static readonly Color Red = new(255, 0, 0);
    
    /// <summary>Gets the green color (0, 255, 0).</summary>
    public static readonly Color Green = new(0, 255, 0);
    
    /// <summary>Gets the blue color (0, 0, 255).</summary>
    public static readonly Color Blue = new(0, 0, 255);
    
    /// <summary>Gets the cyan color (0, 255, 255).</summary>
    public static readonly Color Cyan = new(0, 255, 255);
    
    /// <summary>Gets the magenta color (255, 0, 255).</summary>
    public static readonly Color Magenta = new(255, 0, 255);
    
    /// <summary>Gets the yellow color (255, 255, 0).</summary>
    public static readonly Color Yellow = new(255, 255, 0);
    
    /// <summary>Gets the gray color (128, 128, 128).</summary>
    public static readonly Color Gray = new(128, 128, 128);
    
    /// <summary>Gets the dark gray color (64, 64, 64).</summary>
    public static readonly Color DarkGray = new(64, 64, 64);

    /// <summary>
    /// Creates a color from ARGB channel order.
    /// </summary>
    /// <param name="a">Alpha channel (0-255).</param>
    /// <param name="r">Red channel (0-255).</param>
    /// <param name="g">Green channel (0-255).</param>
    /// <param name="b">Blue channel (0-255).</param>
    public static Color FromArgb(byte a, byte r, byte g, byte b) => new(r, g, b, a);

    /// <summary>
    /// Creates a color from a packed 32-bit ARGB value (0xAARRGGBB).
    /// </summary>
    /// <param name="argb">Packed ARGB value in 0xAARRGGBB order.</param>
    public static Color FromArgb(uint argb)
    {
        var a = (byte)(argb >> 24);
        var r = (byte)(argb >> 16);
        var g = (byte)(argb >> 8);
        var b = (byte)argb;
        return new Color(r, g, b, a);
    }

    /// <summary>
    /// Converts this color to a packed 32-bit ARGB value (0xAARRGGBB).
    /// </summary>
    public uint ToArgb() => ((uint)A << 24) | ((uint)R << 16) | ((uint)G << 8) | B;

    /// <summary>
    /// Converts this color to the OKLCH color space.
    /// </summary>
    public Oklch ToOklch()
    {
        var r = SrgbToLinear(R / 255d);
        var g = SrgbToLinear(G / 255d);
        var b = SrgbToLinear(B / 255d);

        var l = 0.4122214708d * r + 0.5363325363d * g + 0.0514459929d * b;
        var m = 0.2119034982d * r + 0.6806995451d * g + 0.1073969566d * b;
        var s = 0.0883024619d * r + 0.2817188376d * g + 0.6299787005d * b;

        var lRoot = Math.Cbrt(l);
        var mRoot = Math.Cbrt(m);
        var sRoot = Math.Cbrt(s);

        var okl = 0.2104542553d * lRoot + 0.7936177850d * mRoot - 0.0040720468d * sRoot;
        var okA = 1.9779984951d * lRoot - 2.4285922050d * mRoot + 0.4505937099d * sRoot;
        var okB = 0.0259040371d * lRoot + 0.7827717662d * mRoot - 0.8086757660d * sRoot;

        var c = Math.Sqrt(okA * okA + okB * okB);
        var h = Math.Atan2(okB, okA) * (180d / Math.PI);
        if (h < 0d)
        {
            h += 360d;
        }

        return new Oklch(okl, c, h);
    }

    /// <summary>
    /// Creates a color from OKLCH values.
    /// </summary>
    /// <param name="oklch">The source OKLCH color.</param>
    /// <param name="alpha">Alpha channel (0-255).</param>
    public static Color FromOklch(Oklch oklch, byte alpha = 255)
    {
        if (double.IsNaN(oklch.L) || double.IsInfinity(oklch.L))
        {
            throw new ArgumentOutOfRangeException(nameof(oklch), "L must be a finite number.");
        }

        if (double.IsNaN(oklch.C) || double.IsInfinity(oklch.C) || oklch.C < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(oklch), "C must be a finite number greater than or equal to zero.");
        }

        if (double.IsNaN(oklch.H) || double.IsInfinity(oklch.H))
        {
            throw new ArgumentOutOfRangeException(nameof(oklch), "H must be a finite number.");
        }

        var h = oklch.H % 360d;
        if (h < 0d)
        {
            h += 360d;
        }

        var hRadians = h * (Math.PI / 180d);
        var okA = oklch.C * Math.Cos(hRadians);
        var okB = oklch.C * Math.Sin(hRadians);

        var lRoot = oklch.L + 0.3963377774d * okA + 0.2158037573d * okB;
        var mRoot = oklch.L - 0.1055613458d * okA - 0.0638541728d * okB;
        var sRoot = oklch.L - 0.0894841775d * okA - 1.2914855480d * okB;

        var l = lRoot * lRoot * lRoot;
        var m = mRoot * mRoot * mRoot;
        var s = sRoot * sRoot * sRoot;

        var rLinear = +4.0767416621d * l - 3.3077115913d * m + 0.2309699292d * s;
        var gLinear = -1.2684380046d * l + 2.6097574011d * m - 0.3413193965d * s;
        var bLinear = -0.0041960863d * l - 0.7034186147d * m + 1.7076147010d * s;

        var r = ToByte(LinearToSrgb(rLinear));
        var g = ToByte(LinearToSrgb(gLinear));
        var b = ToByte(LinearToSrgb(bLinear));

        return new Color(r, g, b, alpha);
    }
    
    /// <summary>
    /// Parses a color from a hex string.
    /// </summary>
    /// <param name="hex">Hex string in format "#RRGGBB" or "RRGGBB".</param>
    /// <returns>The parsed color.</returns>
    public static Color FromHex(ReadOnlySpan<char> hex)
    {
        if (hex.Length > 0 && hex[0] == '#')
        {
            hex = hex[1..];
        }

        if (hex.Length != 6)
        {
            throw new ArgumentException("Hex color must be 6 characters (RRGGBB)", nameof(hex));
        }

        var r = byte.Parse(hex[0..2], System.Globalization.NumberStyles.HexNumber);
        var g = byte.Parse(hex[2..4], System.Globalization.NumberStyles.HexNumber);
        var b = byte.Parse(hex[4..6], System.Globalization.NumberStyles.HexNumber);
        
        return new Color(r, g, b);
    }
    
    /// <summary>
    /// Converts the color to a hex string.
    /// </summary>
    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    private static double SrgbToLinear(double value)
    {
        var clamped = Math.Clamp(value, 0d, 1d);
        return clamped <= 0.04045d
            ? clamped / 12.92d
            : Math.Pow((clamped + 0.055d) / 1.055d, 2.4d);
    }

    private static double LinearToSrgb(double value)
    {
        var clamped = Math.Clamp(value, 0d, 1d);
        return clamped <= 0.0031308d
            ? 12.92d * clamped
            : 1.055d * Math.Pow(clamped, 1d / 2.4d) - 0.055d;
    }

    private static byte ToByte(double value)
    {
        var scaled = Math.Round(Math.Clamp(value, 0d, 1d) * 255d, MidpointRounding.AwayFromZero);
        return (byte)scaled;
    }
}
