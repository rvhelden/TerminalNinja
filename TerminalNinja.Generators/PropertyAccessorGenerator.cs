using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Scriban;
using Scriban.Runtime;

namespace TerminalNinja.Generators;

/// <summary>
/// Incremental source generator that produces property accessor registrations
/// for all types implementing IControl, inheriting ViewModelBase, implementing
/// INotifyPropertyChanged, marked with [BindableObject], or referenced via
/// DataTemplate.DataType in XAML files.
/// </summary>
[Generator]
public sealed class PropertyAccessorGenerator : IIncrementalGenerator
{
    private const string TerminalNinjaXamlNs = "http://schemas.terminalninja.dev/xaml";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Collect all class declarations that match IsTargetType or IsBindableType
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => GetTargetType(ctx))
            .Where(static t => t is not null)
            .Select(static (t, _) => t!);

        // Collect DataType type names from XAML AdditionalTexts
        var dataTypeNames = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, ct) => file.GetText(ct)?.ToString())
            .Where(static text => text != null)
            .SelectMany(static (text, _) => ExtractDataTypeNames(text!))
            .Collect();

        // Combine everything
        var combined = context.CompilationProvider
            .Combine(classDeclarations.Collect())
            .Combine(dataTypeNames);

        context.RegisterSourceOutput(combined, Execute);
    }

    private static INamedTypeSymbol? GetTargetType(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol symbol)
        {
            return null;
        }

        return GeneratorHelper.IsTargetType(symbol) || GeneratorHelper.IsBindableType(symbol)
            ? symbol
            : null;
    }

    /// <summary>
    /// Extracts fully-qualified CLR type names from DataType attributes in a XAML document.
    /// Resolves namespace prefixes (e.g., "sample:LogEntry") using xmlns declarations.
    /// </summary>
    private static ImmutableArray<string> ExtractDataTypeNames(string xamlContent)
    {
        var builder = ImmutableArray.CreateBuilder<string>();

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xamlContent);
        }
        catch (XmlException)
        {
            return builder.ToImmutable();
        }

        if (doc.Root == null)
        {
            return builder.ToImmutable();
        }

        // Build prefix → CLR namespace map from root xmlns declarations
        var prefixToClrNamespace = new Dictionary<string, string>();
        foreach (var attr in doc.Root.Attributes())
        {
            if (!attr.IsNamespaceDeclaration)
            {
                continue;
            }

            var prefix = attr.Name.LocalName;
            if (attr.Name.Namespace == XNamespace.Xmlns)
            {
                prefix = attr.Name.LocalName; // xmlns:sample="..."
            }
            else if (attr.Name.LocalName == "xmlns")
            {
                continue; // default xmlns — not a prefix we resolve DataType with
            }

            var value = attr.Value;
            if (value.StartsWith("clr-namespace:", StringComparison.Ordinal))
            {
                var clrNs = value.Substring("clr-namespace:".Length);
                var semiIdx = clrNs.IndexOf(';');
                if (semiIdx >= 0)
                {
                    clrNs = clrNs.Substring(0, semiIdx);
                }

                prefixToClrNamespace[prefix] = clrNs;
            }
        }

        // Scan all elements for DataType attributes
        foreach (var element in doc.Root.DescendantsAndSelf())
        {
            var dataTypeAttr = element.Attribute("DataType");
            if (dataTypeAttr == null)
            {
                continue;
            }

            var value = dataTypeAttr.Value; // e.g., "sample:LogEntry"
            var colonIdx = value.IndexOf(':');
            if (colonIdx < 0)
            {
                continue; // No prefix — would need default namespace resolution, skip
            }

            var prefix = value.Substring(0, colonIdx);
            var typeName = value.Substring(colonIdx + 1);

            if (prefixToClrNamespace.TryGetValue(prefix, out var clrNs))
            {
                builder.Add(clrNs + "." + typeName); // e.g., "Sample.LogEntry"
            }
        }

        return builder.ToImmutable();
    }

    private static void Execute(
        SourceProductionContext context,
        ((Compilation Compilation, ImmutableArray<INamedTypeSymbol> Types) Left,
         ImmutableArray<string> DataTypeNames) input)
    {
        var compilation = input.Left.Compilation;
        var types = input.Left.Types;
        var dataTypeNames = input.DataTypeNames;

        // Deduplicate types from class declarations
        var seen = new HashSet<string>();
        var uniqueTypes = new List<INamedTypeSymbol>();

        if (!types.IsDefaultOrEmpty)
        {
            foreach (var type in types)
            {
                var key = type.ToDisplayString();
                if (seen.Add(key))
                {
                    uniqueTypes.Add(type);
                }
            }
        }

        // Resolve DataType type names from XAML and add them.
        // Also collect their fully-qualified names so we can register them
        // in TypeNameRegistry (they aren't controls/ViewModels, so the
        // ControlFactoryGenerator won't register them).
        var dataTypeRegistrations = new List<string>();

        // Collect debug info to embed as comments in generated source
        var debugLines = new List<string>();
        debugLines.Add($"// DEBUG: Assembly={compilation.AssemblyName}, DataTypeNames.Length={dataTypeNames.Length}, IsDefault={dataTypeNames.IsDefault}");
        if (!dataTypeNames.IsDefaultOrEmpty)
        {
            foreach (var n in dataTypeNames)
            {
                debugLines.Add($"//   DataType: {n}");
            }
        }

        if (!dataTypeNames.IsDefaultOrEmpty)
        {
            foreach (var fullTypeName in dataTypeNames)
            {
                var symbol = compilation.GetTypeByMetadataName(fullTypeName);

                if (symbol == null || symbol.IsImplicitlyDeclared ||
                    symbol.TypeKind != TypeKind.Class)
                {
                    continue;
                }

                // Always register in TypeNameRegistry (even if already in uniqueTypes)
                var fqn = GeneratorHelper.GetFullyQualifiedTypeName(symbol);
                dataTypeRegistrations.Add(fqn);

                if (seen.Add(fullTypeName))
                {
                    uniqueTypes.Add(symbol);
                }
            }
        }

        // Build type models for the template
        var typeModels = new List<object>();
        foreach (var type in uniqueTypes)
        {
            var model = GeneratorHelper.CreateTypeModel(type);
            if (model.Properties.Length == 0)
            {
                continue;
            }

            typeModels.Add(new
            {
                full_name = model.FullName,
                name = model.Name,
                is_abstract = model.IsAbstract,
                properties = model.Properties.Select(p => new
                {
                    name = p.Name,
                    type = p.Type,
                    can_read = p.CanRead,
                    can_write = p.CanWrite
                }).ToArray()
            });
        }

        if (typeModels.Count == 0)
        {
            return;
        }

        // Determine namespace from the assembly
        var ns = compilation.AssemblyName ?? "Generated";

        try
        {
            var template = TemplateLoader.Load("PropertyAccessors.sbn");

            var scriptObject = new ScriptObject();
            scriptObject.Add("namespace", ns);
            scriptObject.Add("types", typeModels);
            scriptObject.Add("type_name_registrations", dataTypeRegistrations);

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
