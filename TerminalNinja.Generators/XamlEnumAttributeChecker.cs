using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace TerminalNinja.Generators;

/// <summary>
/// Build-time check for enum-valued XAML attributes.
/// <para>
/// A wrong enum spelling in XAML is otherwise only found when the screen is opened, because the
/// value is converted at load time. This resolves the element's type from the compilation, finds
/// the property, and — when the property type is an enum — verifies the literal is a member or one
/// of the accepted WPF aliases. It reports a warning, never an error: type resolution here is
/// deliberately conservative and anything it cannot resolve is skipped, so it must never be able
/// to fail a build that would have run.
/// </para>
/// </summary>
internal static class XamlEnumAttributeChecker
{
    private const string XamlXNs = "http://schemas.microsoft.com/winfx/2006/xaml";

    internal static readonly DiagnosticDescriptor InvalidEnumValue = new(
        "TNXAML001",
        "Invalid enum value in XAML",
        "'{0}' is not a valid value for {1}.{2} (type '{3}'). Valid values are: {4}",
        "TerminalNinja.Xaml",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "XAML attribute values are converted when the layout loads, so a misspelled enum value would otherwise throw at runtime.");

    /// <summary>
    /// The WPF spellings accepted at runtime. This duplicates
    /// <c>TerminalNinja.Xaml.XamlEnumValues</c> because a source generator cannot reference the
    /// runtime assembly — keep the two in step.
    /// </summary>
    private static readonly Dictionary<string, Dictionary<string, string>> AliasesByEnumFullName =
        new(StringComparer.Ordinal)
        {
            ["TerminalNinja.Primitives.TextTrimming"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["CharacterEllipsis"] = "Ellipsis",
                ["WordEllipsis"] = "Ellipsis",
                ["Clip"] = "None",
            },
            ["TerminalNinja.Primitives.TextAlignment"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Left"] = "Start",
                ["Right"] = "End",
            },
            ["TerminalNinja.Primitives.Alignment"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Left"] = "Start",
                ["Top"] = "Start",
                ["Right"] = "End",
                ["Bottom"] = "End",
            },
            ["TerminalNinja.Primitives.TextWrapping"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["WrapWithOverflow"] = "Wrap",
            },
        };

    /// <summary>
    /// Checks every attribute in <paramref name="xamlFile"/> and reports one diagnostic per
    /// unrecognised enum value.
    /// </summary>
    public static void Check(SourceProductionContext context, Compilation compilation, XamlLayoutFileInfo xamlFile)
    {
        if (string.IsNullOrWhiteSpace(xamlFile.Content))
        {
            return;
        }

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xamlFile.Content, LoadOptions.SetLineInfo);
        }
        catch (Exception)
        {
            return; // Malformed XAML is somebody else's diagnostic.
        }

        var root = doc.Root;
        if (root == null)
        {
            return;
        }

        var namespaces = CollectXmlnsClrNamespaces(compilation);
        if (namespaces.Count == 0)
        {
            return;
        }

        var typeCache = new Dictionary<string, INamedTypeSymbol?>(StringComparer.Ordinal);
        var lineStarts = BuildLineStarts(xamlFile.Content!);

        foreach (var element in root.DescendantsAndSelf())
        {
            var localName = element.Name.LocalName;

            // Property elements (<TextBlock.Foreground>) carry no attributes worth checking.
            if (localName.IndexOf('.') >= 0)
            {
                continue;
            }

            var elementType = ResolveType(compilation, namespaces, typeCache, localName);
            if (elementType == null)
            {
                continue;
            }

            foreach (var attribute in element.Attributes())
            {
                if (attribute.IsNamespaceDeclaration
                    || attribute.Name.NamespaceName == XamlXNs
                    || attribute.Name.LocalName.IndexOf('.') >= 0) // attached property — owner differs
                {
                    continue;
                }

                var value = attribute.Value.Trim();
                if (value.Length == 0
                    || value[0] == '{'          // markup extension, resolved at runtime
                    || value.IndexOf(',') >= 0) // flags list, or a converter's own syntax
                {
                    continue;
                }

                var property = FindProperty(elementType, attribute.Name.LocalName);
                if (property?.Type is not INamedTypeSymbol propertyType || propertyType.TypeKind != TypeKind.Enum)
                {
                    continue;
                }

                if (IsAcceptedValue(propertyType, value))
                {
                    continue;
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidEnumValue,
                    CreateLocation(xamlFile.FilePath, lineStarts, attribute),
                    value,
                    elementType.Name,
                    attribute.Name.LocalName,
                    propertyType.Name,
                    DescribeAcceptedValues(propertyType)));
            }
        }
    }

    private static bool IsAcceptedValue(INamedTypeSymbol enumType, string value)
    {
        // Numeric literals have always been legal in XAML; leave them alone.
        if (long.TryParse(value, out _))
        {
            return true;
        }

        foreach (var member in EnumMemberNames(enumType))
        {
            if (string.Equals(member, value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return AliasesByEnumFullName.TryGetValue(FullName(enumType), out var aliases)
               && aliases.ContainsKey(value);
    }

    private static string DescribeAcceptedValues(INamedTypeSymbol enumType)
    {
        var names = string.Join(", ", EnumMemberNames(enumType));

        if (AliasesByEnumFullName.TryGetValue(FullName(enumType), out var aliases) && aliases.Count > 0)
        {
            names += " (WPF spellings also accepted: "
                     + string.Join(", ", aliases.Select(a => a.Key + " = " + a.Value))
                     + ")";
        }

        return names;
    }

    private static IEnumerable<string> EnumMemberNames(INamedTypeSymbol enumType) =>
        enumType.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => f.HasConstantValue)
            .Select(f => f.Name);

    private static string FullName(INamedTypeSymbol type) =>
        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);

    /// <summary>
    /// Reads the <c>[XmlnsDefinition]</c> attributes off every referenced assembly (and the
    /// compilation itself) to learn which CLR namespaces the default XAML namespace covers.
    /// </summary>
    private static List<string> CollectXmlnsClrNamespaces(Compilation compilation)
    {
        var result = new List<string>();

        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols.Concat([compilation.Assembly]))
        {
            foreach (var attribute in assembly.GetAttributes())
            {
                if (attribute.AttributeClass?.Name != "XmlnsDefinitionAttribute"
                    || attribute.ConstructorArguments.Length != 2)
                {
                    continue;
                }

                if (attribute.ConstructorArguments[1].Value is string clrNamespace && !result.Contains(clrNamespace))
                {
                    result.Add(clrNamespace);
                }
            }
        }

        return result;
    }

    private static INamedTypeSymbol? ResolveType(
        Compilation compilation,
        List<string> namespaces,
        Dictionary<string, INamedTypeSymbol?> cache,
        string localName)
    {
        if (cache.TryGetValue(localName, out var cached))
        {
            return cached;
        }

        INamedTypeSymbol? found = null;
        foreach (var ns in namespaces)
        {
            var candidate = compilation.GetTypeByMetadataName(ns + "." + localName);
            if (candidate == null)
            {
                continue;
            }

            if (found != null)
            {
                // Ambiguous across namespaces — the loader picks by other means, so stay quiet.
                found = null;
                break;
            }

            found = candidate;
        }

        cache[localName] = found;
        return found;
    }

    private static IPropertySymbol? FindProperty(INamedTypeSymbol type, string propertyName)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            var property = current.GetMembers(propertyName).OfType<IPropertySymbol>().FirstOrDefault();
            if (property != null)
            {
                return property;
            }
        }

        return null;
    }

    // ── Locations ───────────────────────────────────────────────────
    // XAML files are AdditionalTexts, so a diagnostic has to be built by hand from the line info
    // XDocument recorded; without it the warning has no file to point at and is nearly useless.

    private static int[] BuildLineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                starts.Add(i + 1);
            }
        }

        return starts.ToArray();
    }

    private static Location CreateLocation(string filePath, int[] lineStarts, XAttribute attribute)
    {
        if (attribute is not System.Xml.IXmlLineInfo lineInfo || !lineInfo.HasLineInfo())
        {
            return Location.None;
        }

        var line = lineInfo.LineNumber - 1;
        var column = lineInfo.LinePosition - 1;
        if (line < 0 || line >= lineStarts.Length || column < 0)
        {
            return Location.None;
        }

        var start = lineStarts[line] + column;
        var length = attribute.Name.LocalName.Length + attribute.Value.Length + 3; // Name="Value"

        return Location.Create(
            filePath,
            new TextSpan(start, length),
            new LinePositionSpan(
                new LinePosition(line, column),
                new LinePosition(line, column + length)));
    }
}
