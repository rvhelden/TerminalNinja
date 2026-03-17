namespace TerminalNinja.Markup;

/// <summary>
/// Maps a CLR namespace to a XAML XML namespace.
/// Shim for System.Windows.Markup.XmlnsDefinitionAttribute to avoid WPF dependency.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class XmlnsDefinitionAttribute : Attribute
{
    public XmlnsDefinitionAttribute(string xmlNamespace, string clrNamespace)
    {
        XmlNamespace = xmlNamespace;
        ClrNamespace = clrNamespace;
    }

    public string XmlNamespace { get; }
    public string ClrNamespace { get; }
}
