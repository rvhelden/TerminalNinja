using TerminalNinja;
using TerminalNinja.Controls;
using TerminalNinja.Skia;
using TerminalNinja.Xaml;

namespace SkiaTerminal;

/// <summary>
/// Standalone demo: a TerminalView living inside a SkiaApplication window, attached to a
/// real PTY shell via <see cref="TerminalNinja.Terminal.ITerminalBackend"/>. The UI is
/// declared in <c>ShellLayout.xaml</c>; this entry point just wires the host to the layout.
/// </summary>
internal static class Program
{
    public static int Main()
    {
        using var viewModel = new ShellViewModel();
        var root = TerminalXaml.Load<Border>(XamlLayouts.ShellLayout, viewModel);

        using var app = new SkiaApplication(new SkiaApplicationOptions
        {
            Title = "TerminalNinja.Skia — Terminal",
            CellsWide = ShellViewModel.Cols + 20,
            CellsTall = ShellViewModel.Rows + 20,
            // FiraCode Nerd Font Mono = ligatures (!= => -> <= >=) AND the Nerd Font
            // icon glyphs nushell / starship / oh-my-posh / lsd emit. Note this is the
            // FAMILY name, not the face name — Skia's SKTypeface.FromFamilyName matches
            // family only, so "FiraCode Nerd Font Mono Reg" (a face) silently falls back
            // to a font without the icon glyphs, rendering them as tofu rectangles.
            FontFamily = "FiraCode Nerd Font Mono",
            // Let raw keys reach the shell; the window's close button still quits.
            EscapeQuits = false,
            EnableTabNavigation = false,
        });

        app.ThemeName = "Dark";
        app.SetRoot(root);
        app.Initialized += () => app.FocusManager.SetFocus(viewModel.Terminal);
        app.Run();

        return 0;
    }
}
