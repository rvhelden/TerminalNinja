using System.ComponentModel;
using System.Globalization;
using TerminalNinja.Primitives;

namespace TerminalNinja.Xaml.TypeConverters;

/// <summary>
/// Converts string values to Thickness struct for XAML parsing.
/// Supports: "4" (uniform), "4,2" (horizontal,vertical), "1,2,3,4" (left,top,right,bottom).
/// </summary>
public class ThicknessTypeConverter : TypeConverter
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
            var parts = str.Split(',');
            
            var values = new int[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out values[i]))
                {
                    throw new FormatException($"Invalid integer in Thickness: {parts[i]}");
                }
            }
            
            return parts.Length switch
            {
                1 => new Thickness(values[0]),  // Uniform
                2 => new Thickness(values[0], values[1]),  // Horizontal, Vertical
                4 => new Thickness(values[0], values[1], values[2], values[3]),  // Left, Top, Right, Bottom
                _ => throw new FormatException($"Invalid Thickness value: {str}. Expected: 1 (uniform), 2 (h,v), or 4 (l,t,r,b) values")
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
        if (destinationType == typeof(string) && value is Thickness thickness)
        {
            // Uniform
            if (thickness.Left == thickness.Top && 
                thickness.Top == thickness.Right && 
                thickness.Right == thickness.Bottom)
            {
                return thickness.Left.ToString(CultureInfo.InvariantCulture);
            }
            
            // Horizontal/Vertical
            if (thickness.Left == thickness.Right && thickness.Top == thickness.Bottom)
            {
                return $"{thickness.Left},{thickness.Top}";
            }
            
            // Full
            return $"{thickness.Left},{thickness.Top},{thickness.Right},{thickness.Bottom}";
        }
        
        return base.ConvertTo(context, culture, value, destinationType);
    }
}
