using System.ComponentModel;
using System.Globalization;
using TerminalNinja.Controls;
using TerminalNinja.Primitives;

namespace TerminalNinja.Xaml.TypeConverters;

/// <summary>
/// Type converter that converts IControl to StackChild by reading attached properties.
/// </summary>
public class StackChildTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return typeof(IControl).IsAssignableFrom(sourceType) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is IControl control)
        {
            var sizeMode = Stack.GetSizeMode(control);
            var fixedSize = Stack.GetFixedSize(control);
            
            return new StackChild
            {
                Content = control,
                SizeMode = sizeMode,
                FixedSize = fixedSize
            };
        }

        return base.ConvertFrom(context, culture, value);
    }
}
