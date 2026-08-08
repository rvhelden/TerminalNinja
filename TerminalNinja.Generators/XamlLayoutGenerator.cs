using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Scriban;
using Scriban.Runtime;

namespace TerminalNinja.Generators;

/// <summary>
/// Incremental source generator that discovers all XAML files (via AdditionalTexts),
/// analyses their dependencies (custom control references and merged resource dictionaries),
/// and emits a static <c>XamlLayouts</c> class with an <c>IXamlLayout</c> field for each layout.
/// </summary>
[Generator]
public sealed class XamlLayoutGenerator : IIncrementalGenerator
{
    private const string XamlXNs = "http://schemas.microsoft.com/winfx/2006/xaml";
    private const string TerminalNinjaXamlNs = "http://schemas.terminalninja.dev/xaml";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Collect all XAML additional text files with their analyzer config options (for metadata)
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

                return new XamlLayoutFileInfo(path, text, resourceName);
            })
            .Where(static info => info.Content != null)
            .Collect();

        // The namespace for the generated XamlLayouts class (and the fallback resource-name
        // prefix) is the project's RootNamespace, not the assembly name — they differ when a
        // project sets a custom AssemblyName (e.g. a lowercase binary name), and generating
        // into a namespace named after the binary puts the manifests where no code lives.
        var rootNamespace = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) =>
                provider.GlobalOptions.TryGetValue("build_property.RootNamespace", out var ns)
                && !string.IsNullOrWhiteSpace(ns)
                    ? ns
                    : null);

        var combined = context.CompilationProvider.Combine(xamlFiles).Combine(rootNamespace);

        context.RegisterSourceOutput(combined, Execute);
    }

    private static void Execute(SourceProductionContext context,
        ((Compilation Compilation, ImmutableArray<XamlLayoutFileInfo> XamlFiles) Left, string? RootNamespace) input)
    {
        var compilation = input.Left.Compilation;
        var xamlFiles = input.Left.XamlFiles;

        if (xamlFiles.IsDefaultOrEmpty)
        {
            return;
        }

        var rootNamespace = input.RootNamespace ?? compilation.AssemblyName ?? "Generated";

        // Phase 1: Parse all XAML files and collect metadata
        var layouts = new List<XamlLayoutModel>();
        var xClassToFieldName = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var xamlFile in xamlFiles)
        {
            // A misspelled enum value is a load-time exception, not a build error — catch what we
            // can here so it surfaces while the XAML is being written instead of when it opens.
            XamlEnumAttributeChecker.Check(context, compilation, xamlFile);

            try
            {
                var model = ParseXamlFile(xamlFile, rootNamespace);
                if (model != null)
                {
                    layouts.Add(model);
                    if (model.XClass != null)
                    {
                        xClassToFieldName[model.XClass] = model.FieldName;
                    }
                }
            }
            catch (Exception ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "TNLAYOUT001",
                        "XAML Layout Analysis Failed",
                        "Failed to analyze XAML file '{0}': {1}",
                        "TerminalNinja.Generators",
                        DiagnosticSeverity.Warning,
                        isEnabledByDefault: true),
                    Location.None,
                    Path.GetFileName(xamlFile.FilePath),
                    ex.Message));
            }
        }

        if (layouts.Count == 0)
        {
            return;
        }

        // Phase 2: Resolve dependency references to field names
        // Build a map of element type full names → field names (for custom control refs)
        // The xClassToFieldName already maps "Sample.ActivityLogControl" → "ActivityLogControl"
        // We also need to map resource file names to field names for merged dictionaries
        var resourceNameToFieldName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var layout in layouts)
        {
            resourceNameToFieldName[layout.ResourceName] = layout.FieldName;
        }

        foreach (var layout in layouts)
        {
            var resolvedDeps = new List<string>();

            foreach (var dep in layout.RawDependencies)
            {
                switch (dep.Kind)
                {
                    case DependencyKind.CustomControl:
                        // dep.Value is a fully-qualified CLR type name (e.g., "Sample.ActivityLogControl")
                        if (xClassToFieldName.TryGetValue(dep.Value, out var fieldName))
                        {
                            if (!resolvedDeps.Contains(fieldName))
                            {
                                resolvedDeps.Add(fieldName);
                            }
                        }
                        break;

                    case DependencyKind.MergedDictionary:
                        // dep.Value is a resource name (e.g., "Sample.Themes.Default.xaml")
                        if (resourceNameToFieldName.TryGetValue(dep.Value, out var dictFieldName))
                        {
                            if (!resolvedDeps.Contains(dictFieldName))
                            {
                                resolvedDeps.Add(dictFieldName);
                            }
                        }
                        break;
                }
            }

            layout.ResolvedDependencyFieldNames = resolvedDeps;
        }

        // Phase 3: Generate source via template
        try
        {
            var template = TemplateLoader.Load("XamlLayouts.sbn");

            var layoutModels = layouts.Select(l => new
            {
                field_name = l.FieldName,
                resource_name = l.ResourceName,
                dependencies = l.ResolvedDependencyFieldNames.ToArray()
            }).ToArray();

            var scriptObject = new ScriptObject();
            scriptObject.Add("namespace", rootNamespace);
            scriptObject.Add("layouts", layoutModels);

            var templateContext = new TemplateContext();
            templateContext.PushGlobal(scriptObject);

            var result = template.Render(templateContext);

            context.AddSource("XamlLayouts.g.cs", result);
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "TNLAYOUT002",
                    "XAML Layout Generation Failed",
                    "Failed to generate XamlLayouts class: {0}",
                    "TerminalNinja.Generators",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                Location.None,
                ex.Message));
        }
    }

    /// <summary>
    /// Parses a single XAML file to extract its metadata: resource name, x:Class, field name,
    /// and raw dependency references.
    /// </summary>
    private static XamlLayoutModel? ParseXamlFile(XamlLayoutFileInfo xamlFile, string rootNamespace)
    {
        if (string.IsNullOrWhiteSpace(xamlFile.Content))
        {
            return null;
        }

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xamlFile.Content);
        }
        catch (XmlException)
        {
            return null;
        }

        var root = doc.Root;
        if (root == null)
        {
            return null;
        }

        // Determine resource name — prefer MSBuild-provided metadata, fall back to convention
        var resourceName = xamlFile.ResourceName;
        if (string.IsNullOrEmpty(resourceName))
        {
            // Fallback: use filename only
            var fileName = Path.GetFileName(xamlFile.FilePath);
            resourceName = $"{rootNamespace}.{fileName}";
        }

        // Determine x:Class (if present)
        var xClassAttr = root.Attribute(XName.Get("Class", XamlXNs));
        var xClass = xClassAttr?.Value;

        // Determine field name from x:Class or filename
        string fieldName;
        if (xClass != null)
        {
            // Use class name portion: "Sample.ActivityLogControl" → "ActivityLogControl"
            var lastDot = xClass.LastIndexOf('.');
            fieldName = lastDot >= 0 ? xClass.Substring(lastDot + 1) : xClass;
        }
        else
        {
            // Use filename without extension: "DemoLayout.xaml" → "DemoLayout"
            fieldName = Path.GetFileNameWithoutExtension(xamlFile.FilePath);
        }

        // Sanitize field name to be a valid C# identifier
        fieldName = SanitizeIdentifier(fieldName);

        // Collect raw dependencies
        var rawDependencies = new List<RawDependency>();

        // Build xmlns prefix → CLR namespace map
        var prefixToClrNamespace = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var attr in root.Attributes())
        {
            if (!attr.IsNamespaceDeclaration)
            {
                continue;
            }

            var prefix = attr.Name.LocalName;
            if (attr.Name.Namespace != XNamespace.Xmlns)
            {
                continue; // default xmlns — skip
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

        // Scan for custom control references (elements from clr-namespace: namespaces)
        var seenTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in root.DescendantsAndSelf())
        {
            var ns = element.Name.NamespaceName;
            var localName = element.Name.LocalName;

            // Skip property elements
            if (localName.Contains('.'))
            {
                continue;
            }

            // Check if this element comes from a clr-namespace
            if (ns.StartsWith("clr-namespace:", StringComparison.Ordinal))
            {
                var clrNs = ns.Substring("clr-namespace:".Length);
                var semiIdx = clrNs.IndexOf(';');
                if (semiIdx >= 0)
                {
                    clrNs = clrNs.Substring(0, semiIdx);
                }

                var fullTypeName = clrNs + "." + localName;
                if (seenTypes.Add(fullTypeName))
                {
                    rawDependencies.Add(new RawDependency(DependencyKind.CustomControl, fullTypeName));
                }
            }
        }

        // Scan for merged resource dictionaries
        // Look for <ResourceDictionary.MergedDictionaries> > <ResourceDictionary Source="..." />
        foreach (var element in root.DescendantsAndSelf())
        {
            if (element.Name.LocalName != "ResourceDictionary")
            {
                continue;
            }

            var sourceAttr = element.Attribute("Source");
            if (sourceAttr == null)
            {
                continue;
            }

            var source = sourceAttr.Value;
            if (string.IsNullOrEmpty(source))
            {
                continue;
            }

            // Convert relative path to resource name convention
            // e.g., "Themes/Default.xaml" → "{AssemblyName}.Themes.Default.xaml"
            var normalized = source.Replace('/', '.').Replace('\\', '.');
            var dictResourceName = $"{rootNamespace}.{normalized}";
            rawDependencies.Add(new RawDependency(DependencyKind.MergedDictionary, dictResourceName));
        }

        return new XamlLayoutModel(resourceName, fieldName, xClass, rawDependencies);
    }

    /// <summary>
    /// Sanitizes a string to be a valid C# identifier.
    /// Replaces invalid characters with underscores and ensures it doesn't start with a digit.
    /// </summary>
    private static string SanitizeIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "_";
        }

        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_')
            {
                chars[i] = '_';
            }
        }

        var result = new string(chars);
        if (char.IsDigit(result[0]))
        {
            result = "_" + result;
        }

        return result;
    }
}

// ────────────────────────────────────────────────────────────────
//  Internal model types
// ────────────────────────────────────────────────────────────────

internal sealed class XamlLayoutFileInfo : IEquatable<XamlLayoutFileInfo>
{
    public string FilePath { get; }
    public string? Content { get; }
    public string? ResourceName { get; }

    public XamlLayoutFileInfo(string filePath, string? content, string? resourceName)
    {
        FilePath = filePath;
        Content = content;
        ResourceName = resourceName;
    }

    public bool Equals(XamlLayoutFileInfo? other)
    {
        if (other is null)
        {
            return false;
        }

        return FilePath == other.FilePath && Content == other.Content && ResourceName == other.ResourceName;
    }

    public override bool Equals(object? obj) => Equals(obj as XamlLayoutFileInfo);
    public override int GetHashCode() => FilePath.GetHashCode();
}

internal enum DependencyKind
{
    CustomControl,
    MergedDictionary
}

internal sealed class RawDependency
{
    public DependencyKind Kind { get; }
    public string Value { get; }

    public RawDependency(DependencyKind kind, string value)
    {
        Kind = kind;
        Value = value;
    }
}

internal sealed class XamlLayoutModel
{
    public string ResourceName { get; }
    public string FieldName { get; }
    public string? XClass { get; }
    public List<RawDependency> RawDependencies { get; }
    public List<string> ResolvedDependencyFieldNames { get; set; } = new();

    public XamlLayoutModel(string resourceName, string fieldName, string? xClass, List<RawDependency> rawDependencies)
    {
        ResourceName = resourceName;
        FieldName = fieldName;
        XClass = xClass;
        RawDependencies = rawDependencies;
    }
}
