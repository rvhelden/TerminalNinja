using System.ComponentModel;
using System.Globalization;

namespace TerminalNinja.Xaml.TypeConverters;

/// <summary>
/// The <see cref="TypeConverter"/> used for every enum reached from XAML — attribute values,
/// <c>&lt;Setter Value="..."/&gt;</c> in a style, and resource-dictionary entries alike.
/// <para>
/// It behaves like <see cref="EnumConverter"/> except that it goes through
/// <see cref="XamlEnumValues"/>, so WPF spellings (<c>CharacterEllipsis</c>, <c>Left</c>,
/// <c>Right</c>) resolve to the matching TerminalNinja member and an unrecognised value fails
/// with a message that names the value and lists what is accepted.
/// </para>
/// </summary>
public sealed class XamlEnumTypeConverter : EnumConverter
{
    private readonly Type _enumType;

    /// <summary>
    /// Creates a converter for <paramref name="enumType"/>.
    /// </summary>
    public XamlEnumTypeConverter(Type enumType) : base(enumType)
    {
        _enumType = enumType;
    }

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text)
        {
            return XamlEnumValues.Parse(_enumType, text, context?.PropertyDescriptor?.Name);
        }

        return base.ConvertFrom(context, culture, value);
    }
}
