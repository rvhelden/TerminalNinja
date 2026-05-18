using TerminalNinja;
using TerminalNinja.Controls;
using TerminalNinja.Input;
using TerminalNinja.Skia;
using TerminalNinja.Xaml;

namespace TerminalNinja.Shell.Skia;

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
            // Tab moves focus through the focusable elements (REPL → EnvList → ScopeList).
            // The lists need keyboard focus to handle their own Up/Down/Enter, so this is
            // the user's path to switch between them.
            EnableTabNavigation = true,
        });

        app.ThemeName = "Dark";
        app.SetRoot(root);
        app.Initialized += () => app.FocusManager.SetFocus(viewModel.Repl);

        // Function-key shortcuts: panel toggles + exit. Handled at the application level so
        // they fire regardless of which control owns focus (otherwise the REPL would swallow
        // them as ordinary KeyChar input on some layouts).
        //
        // Tab is also intercepted here when the REPL has focus, before
        // Application.HandleKeyEvent's tab-navigation branch sees it — that way Tab can
        // open / accept the completion popup while the REPL is the active control, and
        // still acts as "move focus to env panel" once the popup is closed and the user
        // explicitly tabs out (Esc dismisses the popup → next Tab navigates).
        app.KeyDown += (key, args) =>
        {
            switch (key.Key)
            {
                case ConsoleKey.Tab when !key.HasModifiers && app.FocusManager.FocusedElement == viewModel.Repl:
                    // If the REPL has a completion candidate for the current input/cursor,
                    // open/accept the popup and consume Tab. Otherwise fall through so
                    // Application's tab-navigation moves focus to the next panel.
                    if (viewModel.Repl.TryHandleCompletionTab())
                    {
                        args.Handled = true;
                    }
                    break;
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
