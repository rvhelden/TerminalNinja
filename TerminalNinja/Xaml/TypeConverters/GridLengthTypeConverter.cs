using System.ComponentModel;
using System.Globalization;
using TerminalNinja.Primitives;

namespace TerminalNinja.Xaml.TypeConverters;

/// <summary>
/// Converts string values to GridLength for XAML parsing.
/// Supports: "Auto", "100" (pixel), "*" (star), "2*" (weighted star).
/// </summary>
public class GridLengthTypeConverter : TypeConverter
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
            
            // Handle "Auto"
            if (str.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            {
                return GridLength.Auto;
            }
            
            // Handle star values: "*" or "2*" or "0.5*"
            if (str.EndsWith('*'))
            {
                var starStr = str[..^1].Trim();
                
                // Just "*" means 1*
                if (string.IsNullOrEmpty(starStr))
                {
                    return GridLength.Star(1);
                }
                
                if (double.TryParse(starStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var weight))
                {
                    return GridLength.Star(weight);
                }
                
                throw new FormatException($"Invalid star value: {str}");
            }
            
            // Handle absolute pixel value like "100"
            if (double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var pixels))
            {
                return new GridLength(pixels, GridUnitType.Pixel);
            }
            
            throw new FormatException($"Invalid GridLength value: {str}. Expected: Auto, integer (pixel), or star value (*, 2*)");
        }
        
        return base.ConvertFrom(context, culture, value);
    }
    
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
    }
    
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is GridLength length)
        {
            return length.ToString();
        }
        
        return base.ConvertTo(context, culture, value, destinationType);
    }
}
