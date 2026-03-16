using System.Globalization;

namespace TerminalNinja.Xaml.Data;

/// <summary>
/// Converts string values to DateTime for XAML parsing.
/// Supports: "2024-06-01", "2024-06-01T12:30:00", "now", "today".
/// </summary>
public class DateTimeToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter)
    {
        if (value is not DateTime casted)
        {
            return null;
        }
        
        if (parameter is string format)
        {
            return casted.ToString(format, CultureInfo.InvariantCulture);
        }
        
        return casted.ToString(CultureInfo.InvariantCulture);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter)
    {
        throw new NotImplementedException();
    }
}