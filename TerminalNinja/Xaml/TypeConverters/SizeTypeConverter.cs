using System.ComponentModel;
using System.Globalization;
using TerminalNinja.Primitives;

namespace TerminalNinja.Xaml.TypeConverters;

/// <summary>
/// Converts string values to Size struct for XAML parsing.
/// Supports: "100" (absolute), "50%" (percent), "*" or "Stretch" (stretch).
/// </summary>
public class SizeTypeConverter : TypeConverter
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
            
            // Handle "*" or "Stretch"
            if (str == "*" || str.Equals("Stretch", StringComparison.OrdinalIgnoreCase))
            {
                return Size.Stretch;
            }
            
            // Handle percentage like "50%"
            if (str.EndsWith('%'))
            {
                var percentStr = str[..^1];
                if (float.TryParse(percentStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
                {
                    return Size.Percent(percent);
                }
                throw new FormatException($"Invalid percentage value: {str}");
            }
            
            // Handle absolute integer like "100"
            if (int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var absolute))
            {
                return Size.Absolute(absolute);
            }
            
            throw new FormatException($"Invalid Size value: {str}. Expected: integer, percentage (50%), or * (stretch)");
        }
        
        return base.ConvertFrom(context, culture, value);
    }
    
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
    }
    
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is Size size)
        {
            return size.Mode switch
            {
                SizeMode.Absolute => ((int)size.Value).ToString(CultureInfo.InvariantCulture),
                SizeMode.Relative => $"{size.Value * 100:F0}%",
                SizeMode.Stretch => "*",
                _ => size.ToString()
            };
        }
        
        return base.ConvertTo(context, culture, value, destinationType);
    }
}
