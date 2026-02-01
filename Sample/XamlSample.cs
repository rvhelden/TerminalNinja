using TerminalNinja.App;
using TerminalNinja.Elements;
using TerminalNinja.Xaml;
using TerminalNinja.Xaml.Binding;

namespace Sample;

public static class XamlSample
{
    public static void Run()
    {
        // Create application
        using var app = new Application(new ApplicationOptions
        {
            TargetFps = 60,
            EnableMouseTracking = true,
            EnableTabNavigation = true
        });

        // Create ViewModel
        var viewModel = new DemoViewModel();

        // Load UI from XAML file WITH DATA BINDING
        var xamlPath = Path.Combine(AppContext.BaseDirectory, "DemoLayout.xaml");
        
        if (!File.Exists(xamlPath))
        {
            Console.WriteLine($"ERROR: Could not find {xamlPath}");
            Console.WriteLine("Make sure DemoLayout.xaml is copied to the output directory.");
            Thread.Sleep(3000);
            return;
        }

        // Load XAML with binding support - pass viewModel as dataContext
        var bindingManager = new BindingManager();
        var layout = TerminalXaml.LoadFromFile<Stack>(xamlPath, viewModel, bindingManager);

        // Set the loaded layout as root
        app.RootElement = layout;

        // Add ESC handler to exit
        app.KeyDown += (keyEvent, args) =>
        {
            if (keyEvent.Key == ConsoleKey.Escape)
            {
                app.Exit();
                args.Handled = true;
            }
        };

        Console.WriteLine("UI loaded successfully with data binding! Starting application...\n");
        Console.WriteLine("Click the buttons to see automatic UI updates via binding!\n");
        Thread.Sleep(500);

        // Run the application
        app.Run();

        // Cleanup
        bindingManager.Dispose();
    }
}
