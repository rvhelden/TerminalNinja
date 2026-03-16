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
/// Incremental source generator that produces property accessor registrations
/// for all types implementing IControl, inheriting ViewModelBase, or implementing INotifyPropertyChanged.
/// </summary>
[Generator]
public sealed class PropertyAccessorGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Collect all class declarations
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => GetTargetType(ctx))
            .Where(static t => t is not null)
            .Select(static (t, _) => t!);

        // Combine with compilation to resolve full type info
        var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

        context.RegisterSourceOutput(compilationAndClasses, Execute);
    }

    private static INamedTypeSymbol? GetTargetType(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol symbol)
            return null;

        return GeneratorHelper.IsTargetType(symbol) ? symbol : null;
    }

    private static void Execute(
        SourceProductionContext context,
        (Compilation Compilation, ImmutableArray<INamedTypeSymbol> Types) input)
    {
        var (compilation, types) = input;

        if (types.IsDefaultOrEmpty)
            return;

        // Deduplicate types (same type can appear from multiple partial declarations)
        var seen = new HashSet<string>();
        var uniqueTypes = new List<INamedTypeSymbol>();
        foreach (var type in types)
        {
            var key = type.ToDisplayString();
            if (seen.Add(key))
                uniqueTypes.Add(type);
        }

        // Build type models for the template
        var typeModels = new List<object>();
        foreach (var type in uniqueTypes)
        {
            var model = GeneratorHelper.CreateTypeModel(type);
            if (model.Properties.Length == 0)
                continue;

            typeModels.Add(new
            {
                full_name = model.FullName,
                name = model.Name,
                is_abstract = model.IsAbstract,
                properties = model.Properties.Select(p => new
                {
                    name = p.Name,
                    type = p.Type,
                    can_write = p.CanWrite
                }).ToArray()
            });
        }

        if (typeModels.Count == 0)
            return;

        // Determine namespace from the assembly
        var ns = compilation.AssemblyName ?? "Generated";

        try
        {
            var template = TemplateLoader.Load("PropertyAccessors.sbn");

            var scriptObject = new ScriptObject();
            scriptObject.Add("namespace", ns);
            scriptObject.Add("types", typeModels);

            var templateContext = new TemplateContext();
            templateContext.PushGlobal(scriptObject);

            var result = template.Render(templateContext);

            context.AddSource("GeneratedPropertyAccessors.g.cs", result);
        }
        catch (Exception ex)
        {
            // Report as a diagnostic instead of crashing the build
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "TNGEN001",
                    "Property Accessor Generation Failed",
                    "Failed to generate property accessors: {0}",
                    "TerminalNinja.Generators",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                Location.None,
                ex.Message));
        }
    }
}
