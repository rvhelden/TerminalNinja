using System.ComponentModel;
using System.Globalization;
using TerminalNinja.Primitives;

namespace TerminalNinja.Xaml.TypeConverters;

/// <summary>
/// Converts strings like "Bold", "Bold,Underline", "Italic, Strikethrough" to <see cref="TextDecorations"/>.
/// </summary>
public sealed class TextDecorationsTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text)
        {
            text = text.Trim();
            if (string.IsNullOrEmpty(text) || text.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                return TextDecorations.None;
            }

            var result = TextDecorations.None;
            foreach (var part in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                result |= part.ToLowerInvariant() switch
                {
                    "bold" => TextDecorations.Bold,
                    "dim" => TextDecorations.Dim,
                    "italic" => TextDecorations.Italic,
                    "underline" => TextDecorations.Underline,
                    "blink" => TextDecorations.Blink,
                    "inverse" => TextDecorations.Inverse,
                    "strikethrough" => TextDecorations.Strikethrough,
                    _ => throw new FormatException($"Unknown text decoration: '{part}'")
                };
            }

            return result;
        }

        return base.ConvertFrom(context, culture, value);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
        destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is TextDecorations decorations)
        {
            if (decorations == TextDecorations.None)
            {
                return "None";
            }

            var parts = new List<string>();
            if (decorations.HasFlag(TextDecorations.Bold))
            {
                parts.Add("Bold");
            }

            if (decorations.HasFlag(TextDecorations.Dim))
            {
                parts.Add("Dim");
            }

            if (decorations.HasFlag(TextDecorations.Italic))
            {
                parts.Add("Italic");
            }

            if (decorations.HasFlag(TextDecorations.Underline))
            {
                parts.Add("Underline");
            }

            if (decorations.HasFlag(TextDecorations.Blink))
            {
                parts.Add("Blink");
            }

            if (decorations.HasFlag(TextDecorations.Inverse))
            {
                parts.Add("Inverse");
            }

            if (decorations.HasFlag(TextDecorations.Strikethrough))
            {
                parts.Add("Strikethrough");
            }

            return string.Join(",", parts);
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
