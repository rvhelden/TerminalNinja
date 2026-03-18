// ReSharper disable once CheckNamespace
namespace System.Windows.Markup;

/// <summary>
/// Indicates which property of a type is the XAML content property.
/// Shim for System.Windows.Markup.ContentPropertyAttribute to avoid WPF dependency.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ContentPropertyAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
