// ReSharper disable once CheckNamespace
namespace System.Windows.Markup;

/// <summary>
/// Maps a CLR namespace to a XAML XML namespace.
/// Shim for System.Windows.Markup.XmlnsDefinitionAttribute to avoid WPF dependency.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class XmlnsDefinitionAttribute(string xmlNamespace, string clrNamespace) : Attribute
{
    public string XmlNamespace { get; } = xmlNamespace;
    public string ClrNamespace { get; } = clrNamespace;
}
