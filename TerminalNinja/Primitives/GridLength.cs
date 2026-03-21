using System.ComponentModel;
using TerminalNinja.Xaml.TypeConverters;

namespace TerminalNinja.Primitives;

/// <summary>
/// Represents the length of elements that support Star, Auto, and Pixel sizing.
/// Similar to WPF's GridLength.
/// </summary>
[TypeConverter(typeof(GridLengthTypeConverter))]
public readonly record struct GridLength : IEquatable<GridLength>
{
    /// <summary>
    /// Gets the value of this GridLength.
    /// For Pixel, this is the exact size. For Star, this is the weight (e.g., 2 for "2*").
    /// For Auto, this value is ignored.
    /// </summary>
    public double Value { get; }
    
    /// <summary>
    /// Gets the type of sizing for this GridLength.
    /// </summary>
    public GridUnitType UnitType { get; }
    
    /// <summary>
    /// Creates a new GridLength with the specified value and unit type.
    /// </summary>
    public GridLength(double value, GridUnitType unitType = GridUnitType.Pixel)
    {
        if (unitType != GridUnitType.Auto && value < 0)
            throw new ArgumentException("Value cannot be negative", nameof(value));
            
        Value = value;
        UnitType = unitType;
    }
    
    /// <summary>
    /// Gets whether this GridLength represents automatic sizing.
    /// </summary>
    public bool IsAuto => UnitType == GridUnitType.Auto;
    
    /// <summary>
    /// Gets whether this GridLength represents star (proportional) sizing.
    /// </summary>
    public bool IsStar => UnitType == GridUnitType.Star;
    
    /// <summary>
    /// Gets whether this GridLength represents absolute pixel sizing.
    /// </summary>
    public bool IsAbsolute => UnitType == GridUnitType.Pixel;
    
    /// <summary>
    /// Gets a GridLength that represents automatic sizing.
    /// </summary>
    public static GridLength Auto => new(1, GridUnitType.Auto);
    
    /// <summary>
    /// Creates a GridLength with star (proportional) sizing.
    /// </summary>
    /// <param name="value">The star weight (default 1).</param>
    public static GridLength Star(double value = 1) => new(value, GridUnitType.Star);
    
    /// <summary>
    /// Creates a GridLength with absolute pixel sizing.
    /// </summary>
    /// <param name="value">The absolute size in pixels/characters.</param>
    public static GridLength Pixel(int value) => new(value, GridUnitType.Pixel);
    
    /// <summary>
    /// Implicit conversion from int to GridLength (creates Pixel).
    /// </summary>
    public static implicit operator GridLength(int value) => Pixel(value);
    
    /// <summary>
    /// Implicit conversion from double to GridLength (creates Pixel).
    /// </summary>
    public static implicit operator GridLength(double value) => new(value, GridUnitType.Pixel);
    
    /// <summary>
    /// Parses a GridLength from a span without allocation.
    /// Supports: "Auto", integer pixels, "*", "2*".
    /// </summary>
    public static GridLength Parse(ReadOnlySpan<char> span)
    {
        span = span.Trim();

        if (span.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            return Auto;

        if (span.Length > 0 && span[^1] == '*')
        {
            var weightSpan = span[..^1];
            if (weightSpan.IsEmpty) return Star(1);
            if (double.TryParse(weightSpan, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var weight))
                return Star(weight);
            throw new FormatException($"Invalid star value: {span.ToString()}");
        }

        if (double.TryParse(span, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var pixels))
            return new GridLength(pixels, GridUnitType.Pixel);

        throw new FormatException($"Invalid GridLength: {span.ToString()}");
    }

    public override string ToString() => UnitType switch
    {
        GridUnitType.Auto => "Auto",
        GridUnitType.Star => Value == 1 ? "*" : $"{Value}*",
        GridUnitType.Pixel => Value.ToString("F0"),
        _ => Value.ToString()
    };
}

/// <summary>
/// Specifies the type of sizing for a GridLength.
/// </summary>
public enum GridUnitType
{
    /// <summary>
    /// Size is determined by the content.
    /// </summary>
    Auto,
    
    /// <summary>
    /// Size is a fixed number of pixels/characters.
    /// </summary>
    Pixel,
    
    /// <summary>
    /// Size is a weighted proportion of remaining space.
    /// </summary>
    Star
}
