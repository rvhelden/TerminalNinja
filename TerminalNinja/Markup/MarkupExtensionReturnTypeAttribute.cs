// ReSharper disable once CheckNamespace
namespace System.Windows.Markup;

/// <summary>
/// Indicates the return type of a markup extension's ProvideValue method.
/// Shim for System.Windows.Markup.MarkupExtensionReturnTypeAttribute to avoid WPF dependency.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class MarkupExtensionReturnTypeAttribute : Attribute
{
    public MarkupExtensionReturnTypeAttribute(Type returnType) => ReturnType = returnType;

    public Type ReturnType { get; }
}
