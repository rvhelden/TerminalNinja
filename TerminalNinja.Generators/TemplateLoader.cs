using System;
using System.IO;
using System.Reflection;
using Scriban;

namespace TerminalNinja.Generators;

/// <summary>
/// Loads Scriban templates from embedded resources.
/// </summary>
internal static class TemplateLoader
{
    /// <summary>
    /// Loads and parses a Scriban template from an embedded resource.
    /// </summary>
    /// <param name="templateName">Template file name without path (e.g., "PropertyAccessors.sbn")</param>
    public static Template Load(string templateName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"TerminalNinja.Generators.Templates.{templateName}";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            var available = string.Join(", ", assembly.GetManifestResourceNames());
            throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found. Available resources: {available}");
        }

        using var reader = new StreamReader(stream);
        var templateText = reader.ReadToEnd();

        var template = Template.Parse(templateText, templateName);
        if (template.HasErrors)
        {
            var errors = string.Join("\n", template.Messages);
            throw new InvalidOperationException(
                $"Failed to parse Scriban template '{templateName}': {errors}");
        }

        return template;
    }
}
