using SkiaSharp;
using TerminalNinja;
using TerminalNinja.Controls;
using TerminalNinja.Input;
using TerminalNinja.Shell.Skia.Branding;
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
    public static int Main(string[] args)
    {
        // Out-of-band entry point used to bake the static icon PNG that the VS Code extension
        // ships in its VSIX. Bypasses the SDL / GL stack so it runs in CI / headless environments
        // — same SkiaSharp rendering routine the running app uses, just dumped to disk.
        // Usage: dotnet run --project TerminalNinja.Shell.Skia -- --write-icon <path> [size]
        if (args.Length >= 2 && args[0] == "--write-icon")
        {
            var size = args.Length >= 3 && int.TryParse(args[2], out var parsed) ? parsed : 256;
            IconRenderer.WritePng(args[1], size);
            // Fully qualified: a using of TerminalNinja brings in a TerminalNinja.Console
            // namespace that shadows System.Console at the unqualified name.
            System.Console.WriteLine($"Wrote {size}×{size} icon to {args[1]}");
            return 0;
        }

        var viewModel = new ShellViewModel();
        var root = TerminalXaml.Load<Border>(XamlLayouts.ShellLayout, viewModel);

        using var app = new SkiaApplication(new SkiaApplicationOptions
        {
            Title = "NinjaShell UI",
            CellsWide = 140,
            CellsTall = 36,
            // FiraCode Nerd Font Mono ships embedded in this assembly (Fonts/) so ligatures
            // and Nerd Font glyphs render the same on every machine, with no system-wide
            // font install required. SkiaApplication takes ownership and disposes on exit.
            Typeface = LoadEmbeddedTypeface("FiraCodeNerdFontMono-Regular.ttf"),
            // 256px source — window managers resample down to 16/24/32/48 for taskbar +
            // chrome contexts. SkiaApplication consumes (and disposes) the bitmap during
            // window init; SDL3 copies the pixels internally before we let it go.
            WindowIcon = IconRenderer.Render(256),
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
                case ConsoleKey.C when key.Ctrl && !key.Shift && !key.Alt && app.FocusManager.FocusedElement == viewModel.Repl:
                    // Application.HandleKeyEvent treats plain Ctrl+C as "exit unless a TextBox
                    // has focus" — ReplView isn't a TextBox, so without this intercept the
                    // event never reaches the REPL and its selection-copy/clear path never
                    // runs. Forward to the REPL manually and consume so the app doesn't exit.
                    viewModel.Repl.OnKeyEvent(key);
                    args.Handled = true;
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

    private static SKTypeface? LoadEmbeddedTypeface(string resourceName)
    {
        // Embedded as a flat manifest name via <LogicalName> in the csproj, so this lookup
        // is the file name verbatim. SKTypeface.FromStream copies the bytes internally —
        // safe to dispose the stream right after.
        using var stream = typeof(Program).Assembly.GetManifestResourceStream(resourceName);
        return stream is null ? null : SKTypeface.FromStream(stream);
    }
}
