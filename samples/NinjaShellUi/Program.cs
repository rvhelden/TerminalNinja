using TerminalNinja;
using TerminalNinja.Controls;
using TerminalNinja.Input;
using TerminalNinja.Skia;
using TerminalNinja.Xaml;

namespace NinjaShellUi;

/// <summary>
/// Skia-hosted UI for NinjaShell. Three toggleable side panels (files / env / scope) framed
/// around a custom <see cref="ReplView"/> that evaluates user input through
/// <c>NinjaEvaluator</c> in-process. F1 / F2 / F3 toggle panels; F10 exits.
/// </summary>
internal static class Program
{
    public static int Main()
    {
        var viewModel = new ShellViewModel();
        var root = TerminalXaml.Load<Border>(XamlLayouts.ShellLayout, viewModel);

        using var app = new SkiaApplication(new SkiaApplicationOptions
        {
            Title = "NinjaShell UI",
            CellsWide = 140,
            CellsTall = 36,
            // Same font as SkiaTerminal — ligatures + Nerd Font icons render correctly.
            FontFamily = "FiraCode Nerd Font Mono",
            EscapeQuits = false,
            EnableTabNavigation = false,
        });

        app.ThemeName = "Dark";
        app.SetRoot(root);
        app.Initialized += () => app.FocusManager.SetFocus(viewModel.Repl);

        // Function-key shortcuts: panel toggles + exit. Handled at the application level so
        // they fire regardless of which control owns focus (otherwise the REPL would swallow
        // them as ordinary KeyChar input on some layouts).
        app.KeyDown += (key, args) =>
        {
            switch (key.Key)
            {
                case ConsoleKey.F1:
                    viewModel.ToggleFilesPanel();
                    args.Handled = true;
                    break;
                case ConsoleKey.F2:
                    viewModel.ToggleEnvPanel();
                    args.Handled = true;
                    break;
                case ConsoleKey.F3:
                    viewModel.ToggleScopePanel();
                    args.Handled = true;
                    break;
                case ConsoleKey.F10:
                    app.Stop();
                    args.Handled = true;
                    break;
            }
        };

        app.Run();
        return 0;
    }
}
