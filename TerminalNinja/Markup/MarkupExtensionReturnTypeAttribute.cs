// ReSharper disable once CheckNamespace
namespace System.Windows.Markup;

/// <summary>
/// Indicates the return type of a markup extension's ProvideValue method.
/// Shim for System.Windows.Markup.MarkupExtensionReturnTypeAttribute to avoid WPF dependency.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class MarkupExtensionReturnTypeAttribute(Type returnType) : Attribute
{
    public Type ReturnType { get; } = returnType;
}
