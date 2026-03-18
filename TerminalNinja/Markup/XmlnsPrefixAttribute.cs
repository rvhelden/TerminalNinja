// ReSharper disable once CheckNamespace
namespace System.Windows.Markup;

/// <summary>
/// Specifies the recommended prefix for a XAML XML namespace.
/// Shim for System.Windows.Markup.XmlnsPrefixAttribute to avoid WPF dependency.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class XmlnsPrefixAttribute(string xmlNamespace, string prefix) : Attribute
{
    public string XmlNamespace { get; } = xmlNamespace;
    public string Prefix { get; } = prefix;
}
