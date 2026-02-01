using System.Text;
using TerminalNinja.Core.App;
using TerminalNinja.Core.Elements;
using TerminalNinja.Xaml;
using TerminalNinja.Xaml.Binding;

namespace Sample;

public static class XamlSample
{
    public static void Run()
    {
        // Set console encoding to UTF-8 for proper Unicode character rendering
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║       TerminalNinja MVVM Data Binding Demo               ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");
        Console.WriteLine("This demo demonstrates full MVVM data binding!\n");
        Console.WriteLine("Features:");
        Console.WriteLine("  • Data binding with {Binding Path=...}");
        Console.WriteLine("  • INotifyPropertyChanged (ViewModelBase)");
        Console.WriteLine("  • ICommand pattern for button actions");
        Console.WriteLine("  • Automatic UI updates on property changes");
        Console.WriteLine("  • TwoWay binding support");
        Console.WriteLine("  • Nested property paths (User.Address.City)\n");
        Console.WriteLine("Controls:");
        Console.WriteLine("  TAB        - Next button");
        Console.WriteLine("  Mouse      - Click buttons");
        Console.WriteLine("  ESC        - Exit application\n");
        Console.WriteLine("Loading UI from XAML with data binding...\n");

        Thread.Sleep(1500);

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
        
        Console.Clear();
        Console.WriteLine("\n╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║      TerminalNinja MVVM Binding Demo Completed           ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");
        Console.WriteLine($"Total button clicks: {viewModel.ClickCount}");
        Console.WriteLine($"Final status: {viewModel.StatusText}\n");
        Console.WriteLine("Key features demonstrated:");
        Console.WriteLine("  ✓ Loading UI from XAML with data binding");
        Console.WriteLine("  ✓ {Binding Path=...} markup extension");
        Console.WriteLine("  ✓ ViewModelBase with INotifyPropertyChanged");
        Console.WriteLine("  ✓ ICommand pattern (NewCommand, OpenCommand, SaveCommand)");
        Console.WriteLine("  ✓ Automatic UI updates when ViewModel properties change");
        Console.WriteLine("  ✓ Full MVVM architecture support\n");
    }
}
