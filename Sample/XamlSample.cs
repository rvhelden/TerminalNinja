using TerminalNinja.App;
using TerminalNinja.Elements;
using TerminalNinja.Xaml;
using TerminalNinja.Xaml.Binding;

namespace Sample;

public static class XamlSample
{
    public static void Run()
    {
        // Create application with options
        using var app = new Application(new ApplicationOptions
        {
            TargetFps = 60,
            EnableMouseTracking = true,
            EnableTabNavigation = true
        });

        // Create ViewModel
        var viewModel = new DemoViewModel();

        // Load UI from XAML file with Window as root
        var xamlPath = Path.Combine(AppContext.BaseDirectory, "DemoLayout.xaml");
        
        if (!File.Exists(xamlPath))
        {
            Console.WriteLine($"ERROR: Could not find {xamlPath}");
            Console.WriteLine("Make sure DemoLayout.xaml is copied to the output directory.");
            Thread.Sleep(3000);
            return;
        }

        // Load XAML with binding support - now returns Window instead of Stack
        var bindingManager = new BindingManager();
        var window = TerminalXaml.LoadFromFile<Window>(xamlPath, viewModel, bindingManager);

        // Use the WPF-style Window.Show() pattern
        // This sets app.RootElement = window internally
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

        Console.WriteLine("Window loaded with StaticResource support! Starting application...\n");
        Console.WriteLine("Click the buttons to see automatic UI updates via binding!\n");
        Console.WriteLine("Colors are now defined in Window.Resources and referenced via {StaticResource}!\n");
        Thread.Sleep(1000);

        // Run the application
        app.Run();

        // Cleanup
        bindingManager.Dispose();
    }
}
