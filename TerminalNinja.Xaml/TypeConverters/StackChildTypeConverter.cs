using System.ComponentModel;
using System.Globalization;
using TerminalNinja.Core.Elements;
using TerminalNinja.Core.Primitives;

namespace TerminalNinja.Xaml.TypeConverters;

/// <summary>
/// Type converter that converts IElement to StackChild by reading attached properties.
/// </summary>
public class StackChildTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return typeof(IElement).IsAssignableFrom(sourceType) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is IElement element)
        {
            var sizeMode = Stack.GetSizeMode(element);
            var fixedSize = Stack.GetFixedSize(element);
            
            return new StackChild
            {
                Element = element,
                SizeMode = sizeMode,
                FixedSize = fixedSize
            };
        }

        return base.ConvertFrom(context, culture, value);
    }
}
