using Portable.Xaml.Markup;
using SWM = System.Windows.Markup;

// --- Portable.Xaml attributes (for runtime) ---
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

// --- System.Windows.Markup attributes (for IDE IntelliSense) ---
// These duplicate attributes enable XAML IntelliSense in Rider/Visual Studio
[assembly: SWM.XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Controls")]
[assembly: SWM.XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Primitives")]
[assembly: SWM.XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Styling")]
[assembly: SWM.XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Commands")]
[assembly: SWM.XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Resources")]
[assembly: SWM.XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.App")]
[assembly: SWM.XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Xaml.Markup")]
[assembly: SWM.XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Xaml.Binding")]
[assembly: SWM.XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Xaml.Mvvm")]

// Suggest default prefix when adding namespace
[assembly: SWM.XmlnsPrefix("http://schemas.terminalninja.dev/xaml", "tn")]
