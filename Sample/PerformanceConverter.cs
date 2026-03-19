using TerminalNinja.Xaml.Data;

namespace Sample;

/// <summary>
/// Converter for formatting performance statistics.
/// </summary>
public class PerformanceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter)
    {
        if (value == null || parameter is not string format)
            return value?.ToString();

        return format switch
        {
            "Memory" when value is double mb => $"MEM: {mb:F1}MB",
            "Cpu" when value is double cpu => $"CPU: {cpu:F1}%",
            "CurrentFps" when value is int fps => $"{fps}",
            "TargetFps" when value is int fps => $"{fps}",
            "TTFR" when value is double ms => $"TTFR: {ms:F0}ms",
            _ => value.ToString()
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter)
    {
        throw new NotSupportedException();
    }
}
