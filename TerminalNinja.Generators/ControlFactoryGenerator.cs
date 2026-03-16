using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Scriban;
using Scriban.Runtime;

namespace TerminalNinja.Generators;

/// <summary>
/// Incremental source generator that produces factory registrations
/// for all non-abstract types that implement IControl or other instantiable types
/// used in XAML (Style, Setter, DataTemplate, RowDefinition, ColumnDefinition, etc.).
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

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Collect all class declarations that are candidate factory types
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => GetFactoryType(ctx))
            .Where(static t => t is not null)
            .Select(static (t, _) => t!);

        var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

        context.RegisterSourceOutput(compilationAndClasses, Execute);
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

        return null;
    }

    private static void Execute(
        SourceProductionContext context,
        (Compilation Compilation, ImmutableArray<INamedTypeSymbol> Types) input)
    {
        var (compilation, types) = input;

        if (types.IsDefaultOrEmpty)
            return;

        // Deduplicate and filter to non-abstract
        var seen = new HashSet<string>();
        var uniqueTypes = new List<INamedTypeSymbol>();
        foreach (var type in types)
        {
            if (type.IsAbstract || type.IsStatic)
                continue;
            var key = type.ToDisplayString();
            if (seen.Add(key))
                uniqueTypes.Add(type);
        }

        if (uniqueTypes.Count == 0)
            return;

        var typeModels = uniqueTypes.Select(t => new
        {
            full_name = GeneratorHelper.GetFullyQualifiedTypeName(t),
            name = t.Name
        }).ToArray();

        var ns = compilation.AssemblyName ?? "Generated";

        try
        {
            var template = TemplateLoader.Load("ControlFactory.sbn");

            var scriptObject = new ScriptObject();
            scriptObject.Add("namespace", ns);
            scriptObject.Add("types", typeModels);

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
}
