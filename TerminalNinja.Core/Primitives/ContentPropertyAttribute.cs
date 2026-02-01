// ReSharper disable once CheckNamespace
namespace System.Windows.Markup;

/// <summary>
/// Identifies the default content property for a type when used in XAML.
/// This is a marker attribute for XAML parsers.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class ContentPropertyAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the ContentPropertyAttribute class.
    /// </summary>
    public ContentPropertyAttribute()
    {
        Name = string.Empty;
    }
    
    /// <summary>
    /// Initializes a new instance of the ContentPropertyAttribute class with the specified property name.
    /// </summary>
    /// <param name="name">The name of the content property.</param>
    public ContentPropertyAttribute(string name)
    {
        Name = name;
    }
    
    /// <summary>
    /// Gets the name of the content property.
    /// </summary>
    public string Name { get; }
}
