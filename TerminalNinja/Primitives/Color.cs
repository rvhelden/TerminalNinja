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
    /// Parses a color from a hex string.
    /// </summary>
    /// <param name="hex">Hex string in format "#RRGGBB" or "RRGGBB".</param>
    /// <returns>The parsed color.</returns>
    public static Color FromHex(ReadOnlySpan<char> hex)
    {
        if (hex.Length > 0 && hex[0] == '#')
            hex = hex[1..];
        
        if (hex.Length != 6)
            throw new ArgumentException("Hex color must be 6 characters (RRGGBB)", nameof(hex));
        
        var r = byte.Parse(hex[0..2], System.Globalization.NumberStyles.HexNumber);
        var g = byte.Parse(hex[2..4], System.Globalization.NumberStyles.HexNumber);
        var b = byte.Parse(hex[4..6], System.Globalization.NumberStyles.HexNumber);
        
        return new Color(r, g, b);
    }
    
    /// <summary>
    /// Converts the color to a hex string.
    /// </summary>
    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";
}
