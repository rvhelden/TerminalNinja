// ReSharper disable once CheckNamespace
namespace System.Windows.Markup;

/// <summary>
/// Indicates that a property can be initialized by a positional constructor argument in XAML.
/// Shim for System.Windows.Markup.ConstructorArgumentAttribute to avoid WPF dependency.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ConstructorArgumentAttribute : Attribute
{
    public ConstructorArgumentAttribute(string argumentName) => ArgumentName = argumentName;

    public string ArgumentName { get; }
}
