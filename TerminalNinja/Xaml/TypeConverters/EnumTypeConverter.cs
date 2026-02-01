using System.ComponentModel;
using System.Globalization;

namespace TerminalNinja.Xaml.TypeConverters;

/// <summary>
/// Generic enum converter that provides case-insensitive parsing for XAML enum values.
/// This is registered for all enum types in TerminalNinja.
/// </summary>
/// <typeparam name="TEnum">The enum type to convert.</typeparam>
public class EnumTypeConverter<TEnum> : TypeConverter where TEnum : struct, Enum
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
            
            // Case-insensitive enum parsing
            if (Enum.TryParse<TEnum>(str, ignoreCase: true, out var result))
            {
                return result;
            }
            
            // Get valid values for error message
            var validValues = string.Join(", ", Enum.GetNames<TEnum>());
            throw new FormatException($"Invalid {typeof(TEnum).Name} value: {str}. Valid values: {validValues}");
        }
        
        return base.ConvertFrom(context, culture, value);
    }
    
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
    }
    
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is TEnum enumValue)
        {
            return enumValue.ToString();
        }
        
        return base.ConvertTo(context, culture, value, destinationType);
    }
}
