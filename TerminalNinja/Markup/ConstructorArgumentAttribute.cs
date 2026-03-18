// ReSharper disable once CheckNamespace
namespace System.Windows.Markup;

/// <summary>
/// Indicates that a property can be initialized by a positional constructor argument in XAML.
/// Shim for System.Windows.Markup.ConstructorArgumentAttribute to avoid WPF dependency.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ConstructorArgumentAttribute(string argumentName) : Attribute
{
    public string ArgumentName { get; } = argumentName;
}
