using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text;
using TerminalNinja.Controls;
using TerminalNinja.Rendering;
using TerminalNinja.Xaml;
using TerminalNinja.Xaml.Binding;

namespace TerminalNinja.Wasm;

/// <summary>
/// Browser-WASM entry point that exposes XAML rendering to JavaScript.
/// Call <see cref="RenderXaml"/> from JS after loading the dotnet WASM runtime.
/// </summary>
[SupportedOSPlatform("browser")]
public partial class WasmModule
{
    /// <summary>
    /// Renders a XAML string to ANSI escape sequences.
    /// </summary>
    /// <param name="xaml">The XAML markup to parse and render.</param>
    /// <param name="width">Viewport width in columns.</param>
    /// <param name="height">Viewport height in rows.</param>
    /// <returns>A UTF-8 string containing ANSI escape sequences ready for xterm.js.</returns>
    [JSExport]
    public static string RenderXaml(string xaml, int width, int height)
    {
        try
        {
            var bindingManager = new BindingManager();
            var window = TerminalXaml.Load<Window>(xaml, dataContext: null, bindingManager);

            using var memoryStream = new MemoryStream();
            using var renderer = Renderer.CreateOffscreen(memoryStream, width, height);

            renderer.Clear();
            renderer.Draw(window);
            renderer.Present();
            renderer.WriteReset();

            bindingManager.Dispose();

            return Encoding.UTF8.GetString(memoryStream.ToArray());
        }
        catch (Exception ex)
        {
            return $"\u001b[31mError: {ex.Message}\u001b[0m";
        }
    }
}
