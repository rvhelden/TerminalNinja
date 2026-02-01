using System.Text;
using TerminalNinja.Core.App;
using TerminalNinja.Core.Elements;
using TerminalNinja.Xaml;
using TerminalNinja.Xaml.Extensions;

namespace Sample;

public static class XamlSample
{
    public static void Run()
    {
        // Set console encoding to UTF-8 for proper Unicode character rendering
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║          TerminalNinja XAML Loading Demo                 ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");
        Console.WriteLine("This demo loads the UI from DemoLayout.xaml\n");
        Console.WriteLine("Features:");
        Console.WriteLine("  • Declarative UI definition in XAML");
        Console.WriteLine("  • Type converters (Size, Color, Border, etc.)");
        Console.WriteLine("  • Stack attached properties (SizeMode, FixedSize)");
        Console.WriteLine("  • x:Name support for element lookup");
        Console.WriteLine("  • Event wiring in C# code\n");
        Console.WriteLine("Controls:");
        Console.WriteLine("  TAB        - Next button");
        Console.WriteLine("  Mouse      - Click buttons");
        Console.WriteLine("  ESC        - Exit application\n");
        Console.WriteLine("Loading UI from XAML...\n");

        Thread.Sleep(1500);

        // Create application
        using var app = new Application(new ApplicationOptions
        {
            TargetFps = 60,
            EnableMouseTracking = true,
            EnableTabNavigation = true
        });

        // Load UI from XAML file
        var xamlPath = Path.Combine(AppContext.BaseDirectory, "DemoLayout.xaml");
        
        if (!File.Exists(xamlPath))
        {
            Console.WriteLine($"ERROR: Could not find {xamlPath}");
            Console.WriteLine("Make sure DemoLayout.xaml is copied to the output directory.");
            Thread.Sleep(3000);
            return;
        }

        var layout = TerminalXaml.LoadFromFile<Stack>(xamlPath);

        // Find elements by name (using x:Name from XAML)
        var newButton = layout.FindByName<Button>("newButton");
        var openButton = layout.FindByName<Button>("openButton");
        var saveButton = layout.FindByName<Button>("saveButton");

        // Application state
        var clickCount = 0;
        var lastAction = "Ready";

        // Helper to show a message (since Label.Text is init-only, we'll write to console on exit)
        void LogAction(string action)
        {
            clickCount++;
            lastAction = action;
        }

        // Wire up event handlers
        if (newButton != null)
        {
            newButton.Click += () => LogAction("New button clicked");
        }

        if (openButton != null)
        {
            openButton.Click += () => LogAction("Open button clicked");
        }

        if (saveButton != null)
        {
            saveButton.Click += () => LogAction("Save button clicked");
        }

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

        Console.WriteLine("UI loaded successfully! Starting application...\n");
        Thread.Sleep(500);

        // Run the application
        app.Run();

        // Cleanup
        Console.Clear();
        Console.WriteLine("\n╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║        TerminalNinja XAML Demo Completed                 ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");
        Console.WriteLine($"Total button clicks: {clickCount}");
        Console.WriteLine($"Last action: {lastAction}\n");
        Console.WriteLine("Key features demonstrated:");
        Console.WriteLine("  ✓ Loading UI from XAML file");
        Console.WriteLine("  ✓ Finding elements by x:Name");
        Console.WriteLine("  ✓ Wiring up event handlers in code");
        Console.WriteLine("  ✓ Stack attached properties (SizeMode, FixedSize)");
        Console.WriteLine("  ✓ Type converters (Color, Size, Border, etc.)");
        Console.WriteLine("  ✓ Interactive buttons with Tab navigation\n");
    }
}
