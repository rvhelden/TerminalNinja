// ReSharper disable once CheckNamespace
namespace System.Windows.Markup;

/// <summary>
/// Specifies the recommended prefix for a XAML XML namespace.
/// Shim for System.Windows.Markup.XmlnsPrefixAttribute to avoid WPF dependency.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class XmlnsPrefixAttribute : Attribute
{
    public XmlnsPrefixAttribute(string xmlNamespace, string prefix)
    {
        XmlNamespace = xmlNamespace;
        Prefix = prefix;
    }

    public string XmlNamespace { get; }
    public string Prefix { get; }
}
