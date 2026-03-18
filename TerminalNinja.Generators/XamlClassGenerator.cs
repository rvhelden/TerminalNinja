using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Scriban;
using Scriban.Runtime;

namespace TerminalNinja.Generators;

/// <summary>
/// Incremental source generator that processes XAML files with x:Class attributes
/// and generates partial classes with InitializeComponent() methods and named element fields.
/// 
/// This is the TerminalNinja equivalent of WPF's XAML compilation step.
/// </summary>
[Generator]
public sealed class XamlClassGenerator : IIncrementalGenerator
{
    private const string XamlXNs = "http://schemas.microsoft.com/winfx/2006/xaml";
    private const string TerminalNinjaXamlNs = "http://schemas.terminalninja.dev/xaml";

    /// <summary>
    /// Maps the TerminalNinja XAML namespace to its CLR namespace prefixes.
    /// Must stay in sync with XamlLoader.DefaultClrNamespaces.
    /// </summary>
    private static readonly string[] DefaultClrNamespaces =
    {
        "TerminalNinja.Controls",
        "TerminalNinja.Primitives",
        "TerminalNinja.Styling",
        "TerminalNinja.Commands",
        "TerminalNinja.Resources",
        "TerminalNinja.App",
        "TerminalNinja.Xaml.Binding",
        "TerminalNinja.Xaml.Mvvm",
        "TerminalNinja.Xaml.Data"
    };

    /// <summary>
    /// Content control types whose Content can be transferred in InitializeComponent.
    /// </summary>
    private static readonly HashSet<string> ContentControlTypes = new()
    {
        "UserControl",
        "ContentControl",
        "Window"
    };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all additional text files that are XAML, including TerminalNinjaResourceName metadata
        var xamlFiles = context.AdditionalTextsProvider
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Where(static pair => pair.Left.Path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            .Select(static (pair, ct) =>
            {
                var file = pair.Left;
                var optionsProvider = pair.Right;
                var text = file.GetText(ct)?.ToString();
                var path = file.Path;

                // Read the TerminalNinjaResourceName metadata set by MSBuild targets
                string? resourceName = null;
                if (optionsProvider.GetOptions(file).TryGetValue(
                    "build_metadata.AdditionalFiles.TerminalNinjaResourceName", out var rn))
                {
                    resourceName = rn;
                }

                return new XamlFileInfo(path, text, resourceName);
            })
            .Where(static info => info.Content != null);

        var combined = context.CompilationProvider.Combine(xamlFiles.Collect());

        context.RegisterSourceOutput(combined, Execute);
    }

    private static void Execute(SourceProductionContext context,
        (Compilation Compilation, ImmutableArray<XamlFileInfo> XamlFiles) input)
    {
        var compilation = input.Compilation;
        var xamlFiles = input.XamlFiles;

        if (xamlFiles.IsDefaultOrEmpty)
            return;

        foreach (var xamlFile in xamlFiles)
        {
            try
            {
                ProcessXamlFile(context, compilation, xamlFile);
            }
            catch (Exception ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "TNXAML001",
                        "XAML Class Generation Failed",
                        "Failed to process XAML file '{0}': {1}",
                        "TerminalNinja.Generators",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    Location.None,
                    Path.GetFileName(xamlFile.FilePath),
                    ex.Message));
            }
        }
    }

    private static void ProcessXamlFile(SourceProductionContext context, Compilation compilation, XamlFileInfo xamlFile)
    {
        if (string.IsNullOrWhiteSpace(xamlFile.Content))
            return;

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xamlFile.Content);
        }
        catch (XmlException)
        {
            // Not valid XML — skip silently (may be a XAML fragment or resource dictionary)
            return;
        }

        var root = doc.Root;
        if (root == null)
            return;

        // Look for x:Class attribute on root element
        var xClassAttr = root.Attribute(XName.Get("Class", XamlXNs));
        if (xClassAttr == null)
            return; // No x:Class — this XAML doesn't need a generated partial class

        var fullClassName = xClassAttr.Value; // e.g., "Sample.ActivityLogControl"

        // Split into namespace and class name
        var lastDot = fullClassName.LastIndexOf('.');
        string? classNamespace = null;
        string className;
        if (lastDot >= 0)
        {
            classNamespace = fullClassName.Substring(0, lastDot);
            className = fullClassName.Substring(lastDot + 1);
        }
        else
        {
            className = fullClassName;
        }

        // Determine root element type
        var rootTypeName = ResolveElementTypeName(root);

        // Determine if root is a ContentControl-like type
        var isContentControl = IsContentControlType(root, compilation);

        // Collect all x:Name'd elements
        var namedElements = CollectNamedElements(root);

        // Build resource name — prefer MSBuild-provided metadata, fall back to convention
        var assemblyName = compilation.AssemblyName ?? "Generated";
        var resourceName = xamlFile.ResourceName;
        if (string.IsNullOrEmpty(resourceName))
        {
            // Fallback: {AssemblyName}.{ClassName}.xaml (legacy convention)
            resourceName = $"{assemblyName}.{className}.xaml";
        }

        // Generate source
        try
        {
            var template = TemplateLoader.Load("XamlClass.sbn");

            var namedElementModels = new List<object>();
            foreach (var ne in namedElements)
            {
                namedElementModels.Add(new
                {
                    name = ne.Name,
                    type_name = ne.TypeName
                });
            }

            var scriptObject = new ScriptObject();
            scriptObject.Add("namespace", classNamespace ?? assemblyName);
            scriptObject.Add("class_name", className);
            scriptObject.Add("root_type", rootTypeName);
            scriptObject.Add("is_content_control", isContentControl);
            scriptObject.Add("resource_name", resourceName);
            scriptObject.Add("named_elements", namedElementModels);

            var templateContext = new TemplateContext();
            templateContext.PushGlobal(scriptObject);

            var result = template.Render(templateContext);

            var hintName = $"{className}.g.cs";
            context.AddSource(hintName, result);
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "TNXAML002",
                    "XAML Template Rendering Failed",
                    "Failed to render XAML class template for '{0}': {1}",
                    "TerminalNinja.Generators",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                Location.None,
                fullClassName,
                ex.Message));
        }
    }

    /// <summary>
    /// Resolves the CLR type name for an XML element based on its namespace.
    /// </summary>
    private static string ResolveElementTypeName(XElement element)
    {
        var ns = element.Name.NamespaceName;
        var localName = element.Name.LocalName;

        if (ns == TerminalNinjaXamlNs || string.IsNullOrEmpty(ns))
        {
            // Use short name for well-known controls — they're imported via usings
            return localName;
        }

        // clr-namespace: support
        if (ns.StartsWith("clr-namespace:", StringComparison.Ordinal))
        {
            var clrNs = ns.Substring("clr-namespace:".Length);
            var semiIdx = clrNs.IndexOf(';');
            if (semiIdx >= 0)
                clrNs = clrNs.Substring(0, semiIdx);
            return clrNs + "." + localName;
        }

        return localName;
    }

    /// <summary>
    /// Checks if the root element type is a ContentControl (or subclass like UserControl/Window).
    /// Uses both well-known type names and compilation symbol resolution.
    /// </summary>
    private static bool IsContentControlType(XElement root, Compilation compilation)
    {
        var localName = root.Name.LocalName;

        // Fast path: known content control types
        if (ContentControlTypes.Contains(localName))
            return true;

        // Check if the type inherits from ContentControl by looking at compilation symbols
        var ns = root.Name.NamespaceName;
        if (ns == TerminalNinjaXamlNs || string.IsNullOrEmpty(ns))
        {
            foreach (var clrNs in DefaultClrNamespaces)
            {
                var fullName = clrNs + "." + localName;
                var symbol = compilation.GetTypeByMetadataName(fullName);
                if (symbol != null)
                {
                    return InheritsFrom(symbol, "ContentControl");
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether a type symbol inherits from a named base class.
    /// </summary>
    private static bool InheritsFrom(INamedTypeSymbol type, string baseClassName)
    {
        var current = type.BaseType;
        while (current != null)
        {
            if (current.Name == baseClassName)
                return true;
            current = current.BaseType;
        }
        return false;
    }

    /// <summary>
    /// Collects all elements with x:Name attributes from the XAML tree.
    /// Skips the root element (the partial class itself).
    /// </summary>
    private static List<NamedElementInfo> CollectNamedElements(XElement root)
    {
        var result = new List<NamedElementInfo>();

        foreach (var element in root.Descendants())
        {
            var localName = element.Name.LocalName;

            // Skip property elements (e.g., <UserControl.Resources>)
            if (localName.Contains('.'))
                continue;

            var nameAttr = element.Attribute(XName.Get("Name", XamlXNs));
            if (nameAttr == null)
                continue;

            var fieldName = nameAttr.Value;
            var typeName = ResolveElementTypeName(element);

            result.Add(new NamedElementInfo(fieldName, typeName));
        }

        return result;
    }
}

/// <summary>
/// Information about a XAML file discovered by the generator.
/// </summary>
internal sealed class XamlFileInfo : IEquatable<XamlFileInfo>
{
    public string FilePath { get; }
    public string? Content { get; }
    public string? ResourceName { get; }

    public XamlFileInfo(string filePath, string? content, string? resourceName = null)
    {
        FilePath = filePath;
        Content = content;
        ResourceName = resourceName;
    }

    public bool Equals(XamlFileInfo? other)
    {
        if (other is null) return false;
        return FilePath == other.FilePath && Content == other.Content && ResourceName == other.ResourceName;
    }

    public override bool Equals(object? obj) => Equals(obj as XamlFileInfo);
    public override int GetHashCode() => FilePath.GetHashCode();
}

/// <summary>
/// Information about a named element discovered in XAML.
/// </summary>
internal sealed class NamedElementInfo : IEquatable<NamedElementInfo>
{
    public string Name { get; }
    public string TypeName { get; }

    public NamedElementInfo(string name, string typeName)
    {
        Name = name;
        TypeName = typeName;
    }

    public bool Equals(NamedElementInfo? other)
    {
        if (other is null) return false;
        return Name == other.Name && TypeName == other.TypeName;
    }

    public override bool Equals(object? obj) => Equals(obj as NamedElementInfo);
    public override int GetHashCode() => Name.GetHashCode();
}
