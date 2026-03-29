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
        
        app.ThemeName = "Dracula"; // Load the "Dark" theme from embedded XAML resources (e.g., Themes/Dark.xaml)

        // Create ViewModel
        var viewModel = new DemoViewModel();

        // Load UI from an embedded XAML resource using the generated XamlLayouts manifest.
        // This validates all transitive dependencies (e.g., ActivityLogControl.xaml)
        // and loads the root layout from the embedded resource.
        // Bindings are activated automatically via the DP expression system.
        var window = TerminalXaml.Load<Window>(XamlLayouts.DemoLayout, viewModel);

        // Use the WPF-style Window.Show() pattern
        // This sets app.RootControl = window internally
        window.Show();

        // Add ESC handler to exit
        app.KeyDown += (keyEvent, args) =>
        {
            if (keyEvent.Key == ConsoleKey.Escape)
            {
                window.Close();
                app.Exit();
                args.Handled = true;
            }
        };

        // Run the application
        app.Run();
    }
}