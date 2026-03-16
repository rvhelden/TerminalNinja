using System.ComponentModel;
using System.Globalization;
using TerminalNinja.Primitives;
using TerminalNinja.Styling;

namespace TerminalNinja.Xaml.TypeConverters;

/// <summary>
/// Converts string values to BorderStyle struct for XAML parsing.
/// Supports: "None", "Single", "Double", "Rounded", "Ascii".
/// Note: Border color should be set via BorderColor attribute separately.
/// </summary>
public class BorderTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }
    
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string str)
        {
            str = str.Trim();
            
            // Default color is white
            var color = Color.White;
            
            return str.ToLowerInvariant() switch
            {
                "none" => BorderStyle.None,
                "single" => BorderStyle.Single(color),
                "double" => BorderStyle.Double(color),
                "rounded" => BorderStyle.Rounded(color),
                "ascii" => BorderStyle.Ascii(color),
                _ => throw new FormatException($"Invalid Border value: {str}. Expected: None, Single, Double, Rounded, or Ascii")
            };
        }
        
        return base.ConvertFrom(context, culture, value);
    }
    
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
    }
    
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is BorderStyle border)
        {
            if (border == BorderStyle.None) return "None";
            
            // Try to identify the border type by comparing characters
            if (border.Chars == BorderChars.Single) return "Single";
            if (border.Chars == BorderChars.Double) return "Double";
            if (border.Chars == BorderChars.Rounded) return "Rounded";
            if (border.Chars == BorderChars.Ascii) return "Ascii";
            
            return border.ToString();
        }
        
        return base.ConvertTo(context, culture, value, destinationType);
    }
}
