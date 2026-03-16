namespace TerminalNinja.Xaml.Data;

/// <summary>
/// Provides a way to apply custom logic to a binding.
/// </summary>
public interface IValueConverter
{
    /// <summary>
    /// Converts a value from the source to the target.
    /// </summary>
    /// <param name="value">The value produced by the binding source.</param>
    /// <param name="targetType">The type of the binding target property.</param>
    /// <param name="parameter">The converter parameter to use.</param>
    /// <returns>A converted value.</returns>
    object? Convert(object? value, Type targetType, object? parameter);
    
    /// <summary>
    /// Converts a value from the target back to the source.
    /// </summary>
    /// <param name="value">The value produced by the binding target.</param>
    /// <param name="targetType">The type to convert to.</param>
    /// <param name="parameter">The converter parameter to use.</param>
    /// <returns>A converted value.</returns>
    object? ConvertBack(object? value, Type targetType, object? parameter);
}
