using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Scriban;
using Scriban.Runtime;

namespace TerminalNinja.Generators;

/// <summary>
/// Incremental source generator that produces factory registrations
/// for all non-abstract types that implement IControl or other instantiable types
/// used in XAML (Style, Setter, DataTemplate, RowDefinition, ColumnDefinition, etc.).
/// Also generates registrations for:
/// - TypeNameRegistry (type name → Type mapping for XAML type resolution)
/// - ContentPropertyRegistry (from [ContentProperty] attributes)
/// - AttachedPropertySetterRegistry (from static SetXxx methods)
/// - TypeConverterRegistry (from [TypeConverter] attributes)
/// </summary>
[Generator]
public sealed class ControlFactoryGenerator : IIncrementalGenerator
{
    /// <summary>
    /// Additional type names (beyond IControl implementors) that need factories
    /// because they are instantiated from XAML.
    /// </summary>
    private static readonly HashSet<string> AdditionalFactoryTypes = new()
    {
        "DataTemplate",
        "RowDefinition",
        "ColumnDefinition",
        "Style",
        "Setter"
    };

    /// <summary>
    /// Additional interface names (beyond IControl) whose implementors need factories
    /// because they can be instantiated from XAML as resources.
    /// </summary>
    private static readonly string[] AdditionalFactoryInterfaces = { "IValueConverter" };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Pipeline 1: Collect class declarations for factory types
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => GetFactoryType(ctx))
            .Where(static t => t is not null)
            .Select(static (t, _) => t!);

        // Pipeline 2: Collect ALL type declarations (class, struct, enum) for [TypeConverter] discovery
        var typeConverterDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax
                                             || node is StructDeclarationSyntax
                                             || node is EnumDeclarationSyntax
                                             || node is RecordDeclarationSyntax,
                transform: static (ctx, _) => GetTypeConverterInfo(ctx))
            .Where(static t => t is not null)
            .Select(static (t, _) => t!);

        var combined = context.CompilationProvider
            .Combine(classDeclarations.Collect())
            .Combine(typeConverterDeclarations.Collect());

        context.RegisterSourceOutput(combined, Execute);
    }

    private static INamedTypeSymbol? GetFactoryType(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol symbol)
            return null;

        // Must be non-abstract and non-static to be instantiable
        if (symbol.IsAbstract || symbol.IsStatic)
            return null;

        // Check if it implements IControl
        if (GeneratorHelper.IsTargetType(symbol))
            return symbol;

        // Check if it's one of the additional types needed for XAML
        if (AdditionalFactoryTypes.Contains(symbol.Name))
            return symbol;

        // Check if it implements one of the additional factory interfaces (e.g., IValueConverter)
        foreach (var iface in symbol.AllInterfaces)
        {
            foreach (var name in AdditionalFactoryInterfaces)
            {
                if (iface.Name == name)
                    return symbol;
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if a type declaration has a [TypeConverter] attribute and returns info about it.
    /// </summary>
    private static TypeConverterModel? GetTypeConverterInfo(GeneratorSyntaxContext context)
    {
        INamedTypeSymbol? symbol = null;

        switch (context.Node)
        {
            case ClassDeclarationSyntax:
            case StructDeclarationSyntax:
            case RecordDeclarationSyntax:
            case EnumDeclarationSyntax:
                symbol = context.SemanticModel.GetDeclaredSymbol(context.Node) as INamedTypeSymbol;
                break;
        }

        if (symbol == null)
            return null;

        // Skip types that aren't accessible
        if (symbol.DeclaredAccessibility != Accessibility.Public &&
            symbol.DeclaredAccessibility != Accessibility.Internal)
            return null;

        // Look for [TypeConverter(typeof(SomeConverter))] attribute
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name != "TypeConverterAttribute")
                continue;

            // Get the converter type from the constructor argument
            if (attr.ConstructorArguments.Length == 1 &&
                attr.ConstructorArguments[0].Value is INamedTypeSymbol converterType)
            {
                var targetFullName = GeneratorHelper.GetFullyQualifiedTypeName(symbol);
                var converterFullName = GeneratorHelper.GetFullyQualifiedTypeName(converterType);

                // Skip EnumConverter — those are handled automatically at runtime
                if (converterType.Name == "EnumConverter")
                    return null;

                return new TypeConverterModel(targetFullName, converterFullName);
            }
        }

        return null;
    }

    private static void Execute(
        SourceProductionContext context,
        ((Compilation Compilation, ImmutableArray<INamedTypeSymbol> Types) Left,
         ImmutableArray<TypeConverterModel> TypeConverters) input)
    {
        var compilation = input.Left.Compilation;
        var types = input.Left.Types;
        var typeConverters = input.TypeConverters;

        // --- Factory types ---
        var seen = new HashSet<string>();
        var factoryTypeModels = new List<object>();
        if (!types.IsDefaultOrEmpty)
        {
            foreach (var type in types)
            {
                if (type.IsAbstract || type.IsStatic)
                    continue;
                var key = type.ToDisplayString();
                if (seen.Add(key))
                {
                    factoryTypeModels.Add(new
                    {
                        full_name = GeneratorHelper.GetFullyQualifiedTypeName(type)
                    });
                }
            }
        }

        // --- Content properties ---
        // Scan ALL classes in the compilation (including abstract ones) for [ContentProperty]
        var contentPropertyModels = new List<object>();
        var contentSeen = new HashSet<string>();
        foreach (var type in GetAllClassSymbols(compilation))
        {
            var contentPropName = GetContentPropertyAttributeValue(type);
            if (contentPropName == null)
                continue;

            var fullName = GeneratorHelper.GetFullyQualifiedTypeName(type);
            if (contentSeen.Add(fullName))
            {
                contentPropertyModels.Add(new
                {
                    full_name = fullName,
                    property_name = contentPropName
                });
            }
        }

        // --- Attached properties ---
        // Scan all classes for public static void SetXxx(object, T) methods
        var attachedPropertyModels = new List<object>();
        var attachedSeen = new HashSet<string>();
        foreach (var type in GetAllClassSymbols(compilation))
        {
            foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
            {
                if (!method.IsStatic || method.DeclaredAccessibility != Accessibility.Public)
                    continue;
                if (!method.Name.StartsWith("Set", StringComparison.Ordinal) || method.Name.Length <= 3)
                    continue;
                if (method.Parameters.Length != 2)
                    continue;
                if (!method.ReturnsVoid)
                    continue;

                // First parameter must be 'object' (the target)
                if (method.Parameters[0].Type.SpecialType != SpecialType.System_Object)
                    continue;

                var propertyName = method.Name.Substring(3); // "SetRow" → "Row"
                var parameterType = method.Parameters[1].Type;
                var ownerFullName = GeneratorHelper.GetFullyQualifiedTypeName(type);
                var parameterFullName = GeneratorHelper.GetFullyQualifiedTypeName(parameterType);
                var dedupeKey = ownerFullName + "." + propertyName;

                if (attachedSeen.Add(dedupeKey))
                {
                    attachedPropertyModels.Add(new
                    {
                        owner_full_name = ownerFullName,
                        property_name = propertyName,
                        parameter_full_name = parameterFullName
                    });
                }
            }
        }

        // --- TypeConverters ---
        var typeConverterModels = new List<object>();
        var converterSeen = new HashSet<string>();
        if (!typeConverters.IsDefaultOrEmpty)
        {
            foreach (var tc in typeConverters)
            {
                if (converterSeen.Add(tc.TargetFullName))
                {
                    typeConverterModels.Add(new
                    {
                        target_full_name = tc.TargetFullName,
                        converter_full_name = tc.ConverterFullName
                    });
                }
            }
        }

        // Skip if nothing to generate
        if (factoryTypeModels.Count == 0 && contentPropertyModels.Count == 0 &&
            attachedPropertyModels.Count == 0 && typeConverterModels.Count == 0)
            return;

        var ns = compilation.AssemblyName ?? "Generated";

        try
        {
            var template = TemplateLoader.Load("ControlFactory.sbn");

            var scriptObject = new ScriptObject();
            scriptObject.Add("namespace", ns);
            scriptObject.Add("types", factoryTypeModels);
            scriptObject.Add("content_properties", contentPropertyModels);
            scriptObject.Add("attached_properties", attachedPropertyModels);
            scriptObject.Add("type_converters", typeConverterModels);

            var templateContext = new TemplateContext();
            templateContext.PushGlobal(scriptObject);

            var result = template.Render(templateContext);

            context.AddSource("GeneratedControlFactories.g.cs", result);
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "TNGEN002",
                    "Control Factory Generation Failed",
                    "Failed to generate control factories: {0}",
                    "TerminalNinja.Generators",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                Location.None,
                ex.Message));
        }
    }

    /// <summary>
    /// Gets the value of a [ContentProperty("...")] attribute on a type, if present.
    /// Checks both System.Windows.Markup.ContentPropertyAttribute and
    /// Portable.Xaml.Markup.ContentPropertyAttribute.
    /// </summary>
    private static string? GetContentPropertyAttributeValue(INamedTypeSymbol type)
    {
        foreach (var attr in type.GetAttributes())
        {
            if (attr.AttributeClass?.Name != "ContentPropertyAttribute")
                continue;

            if (attr.ConstructorArguments.Length == 1 &&
                attr.ConstructorArguments[0].Value is string propertyName)
            {
                return propertyName;
            }
        }
        return null;
    }

    /// <summary>
    /// Enumerates all named type symbols in the compilation (across all syntax trees).
    /// Returns distinct types.
    /// </summary>
    private static IEnumerable<INamedTypeSymbol> GetAllClassSymbols(Compilation compilation)
    {
        var seen = new HashSet<string>();

        foreach (var tree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();

            foreach (var classDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(classDecl) is INamedTypeSymbol symbol)
                {
                    var key = symbol.ToDisplayString();
                    if (seen.Add(key))
                        yield return symbol;
                }
            }
        }
    }
}

/// <summary>
/// Model for a type → TypeConverter mapping discovered from [TypeConverter] attributes.
/// Implements <see cref="IEquatable{T}"/> for incremental generator caching.
/// </summary>
internal sealed class TypeConverterModel : IEquatable<TypeConverterModel>
{
    public string TargetFullName { get; }
    public string ConverterFullName { get; }

    public TypeConverterModel(string targetFullName, string converterFullName)
    {
        TargetFullName = targetFullName;
        ConverterFullName = converterFullName;
    }

    public bool Equals(TypeConverterModel? other)
    {
        if (other is null) return false;
        return TargetFullName == other.TargetFullName && ConverterFullName == other.ConverterFullName;
    }

    public override bool Equals(object? obj) => Equals(obj as TypeConverterModel);
    public override int GetHashCode() => TargetFullName.GetHashCode() ^ ConverterFullName.GetHashCode();
}
