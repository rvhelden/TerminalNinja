using TerminalNinja.App;
using TerminalNinja.Controls;
using TerminalNinja.Xaml;

namespace Sample;

public static class Program
{
    public static void Main()
    {
        using var app = new Application(new ApplicationOptions
        {
            TargetFps = 60,
            EnableMouseTracking = true,
            EnableTabNavigation = true
        });

        app.ThemeName = "GruvboxDark";

        using var shellViewModel = new ShellViewModel();
        shellViewModel.NavigateToMainMenu();

        var window = TerminalXaml.Load<Window>(XamlLayouts.ShellLayout, shellViewModel);
        window.Show();

        // Hot reload: watch XAML files and reload current sample on change
#if DEBUG
        var sampleProjectPath = FindProjectPath("Sample");
        if (sampleProjectPath != null)
        {
            app.EnableHotReload(
                sampleProjectPath,
                onReload: file => shellViewModel.StatusText = $"Hot reload: {Path.GetFileName(file)}",
                onError: (file, ex) => shellViewModel.StatusText = $"Reload error: {ex.Message}");
        }
#endif

        app.KeyDown += (keyEvent, args) =>
        {
            // Let the Application handle modal dismissal
            if (app.IsModal)
            {
                return;
            }

            switch (keyEvent.Key)
            {
                case ConsoleKey.Enter when shellViewModel.IsOnMainMenu:
                    shellViewModel.NavigateToSelectedSample();
                    args.Handled = true;
                    break;

                case ConsoleKey.Escape when !shellViewModel.IsOnMainMenu:
                    shellViewModel.NavigateToMainMenu();
                    args.Handled = true;
                    break;

                case ConsoleKey.Escape:
                    window.Close();
                    app.Exit();
                    args.Handled = true;
                    break;
            }
        };

        app.Run();
    }

#if DEBUG
    /// <summary>
    /// Walks up from the current directory to find the project folder (contains .csproj).
    /// </summary>
    private static string? FindProjectPath(string projectName)
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10 && dir != null; i++)
        {
            var candidate = Path.Combine(dir, projectName);
            if (Directory.Exists(candidate) && Directory.GetFiles(candidate, "*.csproj").Length > 0)
                return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }
#endif
}
