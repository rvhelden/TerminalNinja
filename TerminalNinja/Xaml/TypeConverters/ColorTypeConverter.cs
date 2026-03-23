using System.ComponentModel;
using System.Globalization;
using TerminalNinja.Primitives;

namespace TerminalNinja.Xaml.TypeConverters;

/// <summary>
/// Converts string values to Color struct for XAML parsing.
/// Supports: "White" (named), "#1E1E1E" (hex), "255,128,0" (RGB).
/// </summary>
public class ColorTypeConverter : TypeConverter
{
    private static readonly Dictionary<string, Color> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Transparent", Color.Transparent },
        { "Black", Color.Black },
        { "White", Color.White },
        { "Red", Color.Red },
        { "Green", Color.Green },
        { "Blue", Color.Blue },
        { "Cyan", Color.Cyan },
        { "Magenta", Color.Magenta },
        { "Yellow", Color.Yellow },
        { "Gray", Color.Gray },
        { "Grey", Color.Gray },  // British spelling
        { "DarkGray", Color.DarkGray },
        { "DarkGrey", Color.DarkGray }  // British spelling
    };
    
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }
    
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string str)
        {
            str = str.Trim();
            
            // Handle named colors
            if (NamedColors.TryGetValue(str, out var namedColor))
            {
                return namedColor;
            }
            
            // Handle hex colors like "#1E1E1E" or "1E1E1E"
            if (str.StartsWith('#') || (str.Length == 6 && !str.Contains(',')))
            {
                try
                {
                    return Color.FromHex(str);
                }
                catch (Exception ex)
                {
                    throw new FormatException($"Invalid hex color: {str}", ex);
                }
            }
            
            // Handle RGB format like "255,128,0"
            if (str.Contains(','))
            {
                var parts = str.Split(',');
                if (parts.Length == 3 &&
                    byte.TryParse(parts[0].Trim(), out var r) &&
                    byte.TryParse(parts[1].Trim(), out var g) &&
                    byte.TryParse(parts[2].Trim(), out var b))
                {
                    return new Color(r, g, b);
                }
                throw new FormatException($"Invalid RGB color: {str}. Expected format: R,G,B (0-255)");
            }
            
            throw new FormatException($"Invalid Color value: {str}. Expected: named color, hex (#RRGGBB), or RGB (R,G,B)");
        }
        
        return base.ConvertFrom(context, culture, value);
    }
    
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
    }
    
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is Color color)
        {
            // Try to find named color
            foreach (var kvp in NamedColors)
            {
                if (kvp.Value == color)
                {
                    return kvp.Key;
                }
            }
            
            // Return as hex
            return color.ToHex();
        }
        
        return base.ConvertTo(context, culture, value, destinationType);
    }
}
