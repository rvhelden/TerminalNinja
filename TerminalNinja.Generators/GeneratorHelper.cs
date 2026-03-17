using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace TerminalNinja.Generators;

/// <summary>
/// Data model representing a type and its properties, used by Scriban templates.
/// </summary>
internal sealed class TypeModel
{
    public string FullName { get; }
    public string Name { get; }
    public bool IsAbstract { get; }
    public ImmutableArray<PropertyModel> Properties { get; }

    public TypeModel(string fullName, string name, bool isAbstract, ImmutableArray<PropertyModel> properties)
    {
        FullName = fullName;
        Name = name;
        IsAbstract = isAbstract;
        Properties = properties;
    }
}

/// <summary>
/// Data model representing a single property on a type.
/// </summary>
internal sealed class PropertyModel
{
    public string Name { get; }
    public string Type { get; }
    public bool CanWrite { get; }

    public PropertyModel(string name, string type, bool canWrite)
    {
        Name = name;
        Type = type;
        CanWrite = canWrite;
    }
}

/// <summary>
/// Shared utilities for finding relevant types during source generation.
/// </summary>
internal static class GeneratorHelper
{
    /// <summary>
    /// Interface and base class names the generator looks for.
    /// </summary>
    private static readonly string[] TargetInterfaceNames = { };
    private static readonly string[] TargetBaseClassNames = { "Visual", "UIElement", "FrameworkElement", "Control", "ViewModelBase" };
    private const string NotifyPropertyChangedName = "INotifyPropertyChanged";
    private const string BindableObjectAttributeName = "BindableObjectAttribute";

    /// <summary>
    /// Additional type names (beyond base class inheritors) that need property
    /// accessors generated because they are used in XAML and their properties
    /// are set by the XamlLoader via PropertyAccessorRegistry.
    /// </summary>
    private static readonly HashSet<string> AdditionalAccessorTypes = new()
    {
        "DataTemplate",
        "Style",
        "Setter",
        "RowDefinition",
        "ColumnDefinition"
    };

    /// <summary>
    /// Checks if a type should have property accessors generated for it.
    /// A type qualifies if it:
    /// - Inherits from one of the control base classes
    /// - Inherits from ViewModelBase
    /// - Implements INotifyPropertyChanged
    /// - Is one of the additional XAML-instantiable types (DataTemplate, Style, etc.)
    /// </summary>
    public static bool IsTargetType(INamedTypeSymbol type)
    {
        // Skip compiler-generated types
        if (type.IsImplicitlyDeclared)
            return false;

        // Must be a class (not interface, struct, enum, delegate)
        if (type.TypeKind != TypeKind.Class)
            return false;

        // Skip types that aren't accessible from generated top-level code
        // (e.g., private nested test classes)
        if (!IsAccessibleFromGeneratedCode(type))
            return false;

        // Check if it inherits from target base classes
        if (InheritsFrom(type, TargetBaseClassNames))
            return true;

        // Check if it implements INotifyPropertyChanged (catches other bindable types)
        if (ImplementsInterface(type, new[] { NotifyPropertyChangedName }))
            return true;

        // Check if it's one of the additional XAML types that need accessors
        if (AdditionalAccessorTypes.Contains(type.Name))
            return true;

        return false;
    }

    /// <summary>
    /// Checks if a type is a plain data class (POCO) marked with [BindableObject]
    /// that should have property accessors generated for AOT-safe data binding.
    /// This is separate from <see cref="IsTargetType"/> so that only the
    /// PropertyAccessorGenerator uses it — the ControlFactoryGenerator does not
    /// need to register POCOs as XAML-instantiable controls.
    /// </summary>
    public static bool IsBindableType(INamedTypeSymbol type)
    {
        // Skip compiler-generated types
        if (type.IsImplicitlyDeclared)
            return false;

        // Must be a class (not interface, struct, enum, delegate)
        if (type.TypeKind != TypeKind.Class)
            return false;

        // Skip types that aren't accessible from generated top-level code
        if (!IsAccessibleFromGeneratedCode(type))
            return false;

        // Already covered by IsTargetType — skip to avoid duplicate registration
        if (IsTargetType(type))
            return false;

        // Check for [BindableObject] attribute
        return HasAttribute(type, BindableObjectAttributeName);
    }

    /// <summary>
    /// Checks if a type has an attribute with the given name.
    /// </summary>
    private static bool HasAttribute(INamedTypeSymbol type, string attributeName)
    {
        foreach (var attr in type.GetAttributes())
        {
            if (attr.AttributeClass?.Name == attributeName)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Gets all public instance properties that should have accessors generated.
    /// Excludes indexers and properties with inaccessible getters.
    /// Only returns properties declared on this type (not inherited ones).
    /// </summary>
    public static ImmutableArray<PropertyModel> GetProperties(INamedTypeSymbol type)
    {
        var builder = ImmutableArray.CreateBuilder<PropertyModel>();

        foreach (var member in type.GetMembers())
        {
            if (member is not IPropertySymbol property)
                continue;

            // Skip static, indexers, and non-public properties
            if (property.IsStatic)
                continue;
            if (property.IsIndexer)
                continue;
            if (property.DeclaredAccessibility != Accessibility.Public &&
                property.DeclaredAccessibility != Accessibility.Internal)
                continue;

            // Must have a getter
            if (property.GetMethod == null)
                continue;
            if (property.GetMethod.DeclaredAccessibility != Accessibility.Public &&
                property.GetMethod.DeclaredAccessibility != Accessibility.Internal)
                continue;

            // Determine if writable (init-only setters are NOT writable at runtime)
            bool canWrite = property.SetMethod != null &&
                           !property.SetMethod.IsInitOnly &&
                           (property.SetMethod.DeclaredAccessibility == Accessibility.Public ||
                            property.SetMethod.DeclaredAccessibility == Accessibility.Internal);

            var propertyType = GetFullyQualifiedTypeName(property.Type);

            builder.Add(new PropertyModel(property.Name, propertyType, canWrite));
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Creates a TypeModel for the given named type symbol.
    /// </summary>
    public static TypeModel CreateTypeModel(INamedTypeSymbol type)
    {
        var fullName = GetFullyQualifiedTypeName(type);
        var properties = GetProperties(type);
        return new TypeModel(fullName, type.Name, type.IsAbstract, properties);
    }

    /// <summary>
    /// Gets the fully qualified type name suitable for use in generated C# code.
    /// </summary>
    public static string GetFullyQualifiedTypeName(ITypeSymbol type)
    {
        // Use the display format that includes the global:: prefix for unambiguity
        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    private static bool ImplementsInterface(INamedTypeSymbol type, string[] interfaceNames)
    {
        foreach (var iface in type.AllInterfaces)
        {
            foreach (var name in interfaceNames)
            {
                if (iface.Name == name)
                    return true;
            }
        }
        return false;
    }

    private static bool InheritsFrom(INamedTypeSymbol type, string[] baseClassNames)
    {
        var current = type.BaseType;
        while (current != null)
        {
            foreach (var name in baseClassNames)
            {
                if (current.Name == name)
                    return true;
            }
            current = current.BaseType;
        }
        return false;
    }

    /// <summary>
    /// Checks if a type is accessible from top-level generated code in the same assembly.
    /// A type is accessible if it (and all containing types) have at least internal accessibility.
    /// </summary>
    private static bool IsAccessibleFromGeneratedCode(INamedTypeSymbol type)
    {
        var current = type;
        while (current != null)
        {
            if (current.DeclaredAccessibility != Accessibility.Public &&
                current.DeclaredAccessibility != Accessibility.Internal)
            {
                return false;
            }
            current = current.ContainingType;
        }
        return true;
    }
}
