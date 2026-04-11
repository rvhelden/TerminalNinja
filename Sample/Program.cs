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
}
