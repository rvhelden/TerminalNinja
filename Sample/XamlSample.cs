using System.Reflection;
using TerminalNinja.Aot;
using TerminalNinja.App;
using TerminalNinja.Controls;
using TerminalNinja.Xaml;
using TerminalNinja.Xaml.Binding;

namespace Sample;

public static class XamlSample
{
    public static void Run()
    {
        // Register data types that appear in XAML DataType attributes but
        // aren't auto-discovered by the source generator (plain POCOs).
        TypeNameRegistry.Register(typeof(LogEntry));
        // Create application with options
        using var app = new Application(new ApplicationOptions
        {
            TargetFps = 60,
            EnableMouseTracking = true,
            EnableTabNavigation = true
        });

        // Create ViewModel
        var viewModel = new DemoViewModel();

        // Load UI from embedded XAML resource
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "Sample.DemoLayout.xaml";
        
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            Console.WriteLine($"ERROR: Could not find embedded resource '{resourceName}'");
            Console.WriteLine("Available resources:");
            foreach (var name in assembly.GetManifestResourceNames())
            {
                Console.WriteLine($"  - {name}");
            }
            Thread.Sleep(3000);
            return;
        }
        
        // Load XAML with binding support from embedded resource stream
        var bindingManager = new BindingManager();
        var window = TerminalXaml.LoadFromStream<Window>(stream, viewModel, bindingManager);

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

        Console.WriteLine("Window loaded from embedded resource with StaticResource support! Starting application...\n");
        Console.WriteLine("Click the buttons to see automatic UI updates via binding!\n");
        Console.WriteLine("Colors are now defined in Window.Resources and referenced via {StaticResource}!\n");
        Thread.Sleep(1000);

        // Run the application
        app.Run();

        // Cleanup
        bindingManager.Dispose();
    }
}
