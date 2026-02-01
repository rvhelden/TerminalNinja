using System.ComponentModel;
using System.Reflection;
using Portable.Xaml;
using Portable.Xaml.ComponentModel;
using Portable.Xaml.Schema;
using TerminalNinja.Core.Primitives;
using TerminalNinja.Core.Elements;
using TerminalNinja.Core.Styling;
using TerminalNinja.Xaml.TypeConverters;

namespace TerminalNinja.Xaml;

/// <summary>
/// Custom XAML schema context that provides type information and type converters
/// for TerminalNinja elements.
/// </summary>
public class TerminalXamlSchemaContext : XamlSchemaContext
{
    private static readonly object _lock = new();
    private static bool _typeConvertersRegistered = false;
    
    // Register type converters globally using TypeDescriptor (once)
    public TerminalXamlSchemaContext() : base()
    {
        lock (_lock)
        {
            if (!_typeConvertersRegistered)
            {
                TypeDescriptor.AddAttributes(typeof(Size), new TypeConverterAttribute(typeof(SizeTypeConverter)));
                TypeDescriptor.AddAttributes(typeof(Color), new TypeConverterAttribute(typeof(ColorTypeConverter)));
                TypeDescriptor.AddAttributes(typeof(Border), new TypeConverterAttribute(typeof(BorderTypeConverter)));
                TypeDescriptor.AddAttributes(typeof(Thickness), new TypeConverterAttribute(typeof(ThicknessTypeConverter)));
                TypeDescriptor.AddAttributes(typeof(Alignment), new TypeConverterAttribute(typeof(EnumTypeConverter<Alignment>)));
                TypeDescriptor.AddAttributes(typeof(TextAlignment), new TypeConverterAttribute(typeof(EnumTypeConverter<TextAlignment>)));
                TypeDescriptor.AddAttributes(typeof(TextWrapping), new TypeConverterAttribute(typeof(EnumTypeConverter<TextWrapping>)));
                TypeDescriptor.AddAttributes(typeof(TextTrimming), new TypeConverterAttribute(typeof(EnumTypeConverter<TextTrimming>)));
                TypeDescriptor.AddAttributes(typeof(ChildSizeMode), new TypeConverterAttribute(typeof(EnumTypeConverter<ChildSizeMode>)));
                TypeDescriptor.AddAttributes(typeof(StackOrientation), new TypeConverterAttribute(typeof(EnumTypeConverter<StackOrientation>)));
                
                _typeConvertersRegistered = true;
            }
        }
    }
    
    protected override ICustomAttributeProvider GetCustomAttributeProvider(Type type)
    {
        // For element types, wrap with our custom attribute provider
        if (typeof(IElement).IsAssignableFrom(type))
        {
            return new ElementAttributeProvider(type);
        }
        
        return base.GetCustomAttributeProvider(type);
    }

    /// <summary>
    /// Custom attribute provider that adds XAML-specific attributes to element types
    /// </summary>
    private class ElementAttributeProvider : ICustomAttributeProvider
    {
        private readonly Type _type;
        
        public ElementAttributeProvider(Type type)
        {
            _type = type;
        }
        
        public object[] GetCustomAttributes(bool inherit)
        {
            var attrs = new List<object>(_type.GetCustomAttributes(inherit));
            
            // Add RuntimeNamePropertyAttribute for all elements (they all have a Name property)
            attrs.Add(new Portable.Xaml.Markup.RuntimeNamePropertyAttribute(nameof(IElement.Name)));
            
            // Add ContentPropertyAttribute for specific types
            if (_type == typeof(Rectangle))
            {
                attrs.Add(new Portable.Xaml.Markup.ContentPropertyAttribute(nameof(Rectangle.Child)));
            }
            
            return attrs.ToArray();
        }
        
        public object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            var allAttrs = GetCustomAttributes(inherit);
            return allAttrs.Where(a => attributeType.IsInstanceOfType(a)).ToArray();
        }
        
        public bool IsDefined(Type attributeType, bool inherit)
        {
            return GetCustomAttributes(attributeType, inherit).Length > 0;
        }
    }
}
