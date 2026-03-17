namespace TerminalNinja.Markup;

/// <summary>
/// Indicates which property of a type maps to x:Name in XAML.
/// Shim for System.Windows.Markup.RuntimeNamePropertyAttribute to avoid WPF dependency.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class RuntimeNamePropertyAttribute : Attribute
{
    public RuntimeNamePropertyAttribute(string name) => Name = name;

    public string Name { get; }
}
