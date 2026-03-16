using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using TerminalNinja.Controls;
using TerminalNinja.Primitives;
using TerminalNinja.Styling;
using TerminalNinja.Xaml.TypeConverters;

namespace TerminalNinja.Aot;

/// <summary>
/// AOT-safe registry of <see cref="TypeConverter"/> instances keyed by target type.
/// Replaces <c>TypeDescriptor.GetConverter(Type)</c> which is not trim-safe.
/// Populated with all known type converters at static initialization time.
/// </summary>
public static class TypeConverterRegistry
{
    private static readonly ConcurrentDictionary<Type, TypeConverter> Converters = new();

    /// <summary>
    /// Static initializer registers all known type converters from [TypeConverter] attributes.
    /// </summary>
    static TypeConverterRegistry()
    {
        // Primitive types with custom TypeConverters
        Register(typeof(Color), new ColorTypeConverter());
        Register(typeof(Thickness), new ThicknessTypeConverter());
        Register(typeof(Size), new SizeTypeConverter());
        Register(typeof(GridLength), new GridLengthTypeConverter());
        Register(typeof(Border), new BorderTypeConverter());

        // Enum types with EnumConverter (System.ComponentModel built-in)
        Register(typeof(TextWrapping), new EnumConverter(typeof(TextWrapping)));
        Register(typeof(TextTrimming), new EnumConverter(typeof(TextTrimming)));
        Register(typeof(TextAlignment), new EnumConverter(typeof(TextAlignment)));
        Register(typeof(Alignment), new EnumConverter(typeof(Alignment)));
        Register(typeof(ChildSizeMode), new EnumConverter(typeof(ChildSizeMode)));
        Register(typeof(Orientation), new EnumConverter(typeof(Orientation)));
    }

    /// <summary>
    /// Registers a type converter for the specified type.
    /// </summary>
    public static void Register(Type type, TypeConverter converter)
    {
        Converters[type] = converter;
    }

    /// <summary>
    /// Tries to get a type converter for the specified type.
    /// Returns true if a converter was found, false otherwise.
    /// </summary>
    public static bool TryGetConverter(Type type, [NotNullWhen(true)] out TypeConverter? converter)
    {
        return Converters.TryGetValue(type, out converter);
    }

    /// <summary>
    /// Gets a type converter for the specified type, returning null if none is registered.
    /// </summary>
    public static TypeConverter? GetConverter(Type type)
    {
        return Converters.TryGetValue(type, out var converter) ? converter : null;
    }

    /// <summary>
    /// Gets a type converter for the specified type, or creates an <see cref="EnumConverter"/>
    /// on-the-fly for enum types that aren't explicitly registered.
    /// </summary>
    public static TypeConverter? GetConverterOrEnum(Type type)
    {
        if (Converters.TryGetValue(type, out var converter))
            return converter;

        // Auto-create EnumConverter for any enum type
        if (type.IsEnum)
        {
            var enumConverter = new EnumConverter(type);
            Converters[type] = enumConverter;
            return enumConverter;
        }

        return null;
    }
}
