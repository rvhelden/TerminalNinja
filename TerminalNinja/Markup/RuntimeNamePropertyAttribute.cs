// ReSharper disable once CheckNamespace
namespace System.Windows.Markup;

/// <summary>
/// Indicates which property of a type maps to x:Name in XAML.
/// Shim for System.Windows.Markup.RuntimeNamePropertyAttribute to avoid WPF dependency.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RuntimeNamePropertyAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
