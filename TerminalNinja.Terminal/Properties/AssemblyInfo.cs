using System.Runtime.CompilerServices;
using System.Windows.Markup;

// LibraryImport-based P/Invoke (ConPtyNative, UnixPtyNative when added) requires the
// assembly to opt out of runtime marshalling so the source-generated stubs can handle
// blittable-only types. Affects every P/Invoke in this assembly; all signatures here use
// int for Win32 BOOL and pass strings via the Utf16/Utf8 marshallers built into LibraryImport.
[assembly: DisableRuntimeMarshalling]

// Expose TerminalView and friends through the default TerminalNinja XAML namespace so they
// can be used without a custom prefix.
[assembly: XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Terminal")]
