// ReSharper disable once CheckNamespace
namespace System.Windows.Markup;

/// <summary>
/// Indicates which property of a type is the XAML content property.
/// Shim for System.Windows.Markup.ContentPropertyAttribute to avoid WPF dependency.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class ContentPropertyAttribute : Attribute
{
    public ContentPropertyAttribute(string name) => Name = name;

    public string Name { get; }
}
