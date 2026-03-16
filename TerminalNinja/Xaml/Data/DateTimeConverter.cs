using System.Globalization;

namespace TerminalNinja.Xaml.Data;

/// <summary>
/// Converts string values to DateTime for XAML parsing.
/// Supports: "2024-06-01", "2024-06-01T12:30:00", "now", "today".
/// </summary>
public class DateTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter)
    {
        if (value is string str)
        {
            str = str.Trim().ToLowerInvariant();
            
            if (str == "now")
            {
                return DateTime.Now;
            }
            
            if (str == "today")
            {
                return DateTime.Today;
            }

            if (!DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
                throw new FormatException($"Invalid DateTime value: {str}. Expected format: '2024-06-01', '2024-06-01T12:30:00', 'now', or 'today'.");
            
            if (parameter is string format)
            {
                if (!DateTime.TryParseExact(str, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime))
                    throw new FormatException(
                        $"Invalid DateTime value: {str}. Expected format: '{format}'.");
            }
            
            return dateTime;
        }
        
        throw new InvalidCastException($"Cannot convert value of type {value?.GetType()} to DateTime. Expected a string.");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter)
    {
        throw new NotImplementedException();
    }
}