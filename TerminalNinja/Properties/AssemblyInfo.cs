using System.Windows.Markup;

// Map all CLR namespaces to a single XAML namespace URL
[assembly: XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Controls")]
[assembly: XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Primitives")]
[assembly: XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Styling")]
[assembly: XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Commands")]
[assembly: XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Resources")]
[assembly: XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.App")]
[assembly: XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Xaml.Markup")]
[assembly: XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Xaml.Binding")]
[assembly: XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Xaml.Mvvm")]

// Suggest default prefix when adding namespace
[assembly: XmlnsPrefix("http://schemas.terminalninja.dev/xaml", "tn")]

// --- TEMPORARY: Portable.Xaml attributes (required until XAML compiler replaces runtime parser) ---
// These will be removed in Phase 4g when the Portable.Xaml PackageReference is deleted.
[assembly: Portable.Xaml.Markup.XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Controls")]
[assembly: Portable.Xaml.Markup.XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Primitives")]
[assembly: Portable.Xaml.Markup.XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Styling")]
[assembly: Portable.Xaml.Markup.XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Commands")]
[assembly: Portable.Xaml.Markup.XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Resources")]
[assembly: Portable.Xaml.Markup.XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.App")]
[assembly: Portable.Xaml.Markup.XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Xaml.Markup")]
[assembly: Portable.Xaml.Markup.XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Xaml.Binding")]
[assembly: Portable.Xaml.Markup.XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Xaml.Mvvm")]
[assembly: Portable.Xaml.Markup.XmlnsPrefix("http://schemas.terminalninja.dev/xaml", "tn")]
