using System.Text;
using TerminalNinja.Controls;
using TerminalNinja.Rendering;
using TerminalNinja.Xaml;
using TerminalNinja.Xaml.Binding;

namespace TerminalNinja.Cli;

/// <summary>
/// CLI tool that renders a XAML layout to ANSI escape sequences and writes them to stdout.
/// Designed for snapshot generation, piping, and future WASM compilation.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        var options = ParseArgs(args);
        if (options is null)
        {
            PrintUsage();
            return 1;
        }

        if (!File.Exists(options.XamlFile))
        {
            Console.Error.WriteLine($"Error: XAML file not found: {options.XamlFile}");
            return 1;
        }

        Console.OutputEncoding = Encoding.UTF8;

        var xaml = File.ReadAllText(options.XamlFile);

        // Load XAML with optional binding support
        var bindingManager = new BindingManager();
        var window = TerminalXaml.Load<Window>(xaml, dataContext: null, bindingManager);

        // Create an offscreen renderer that writes to stdout
        var stdout = Console.OpenStandardOutput();
        using var renderer = Renderer.CreateOffscreen(stdout, options.Width, options.Height);

        // Render the requested number of frames
        for (var frame = 0; frame < options.Frames; frame++)
        {
            renderer.Clear();
            renderer.Draw(window);
            renderer.Present();
        }

        // Reset terminal attributes at the end
        renderer.WriteReset();

        bindingManager.Dispose();

        return 0;
    }

    private static SnapshotOptions? ParseArgs(string[] args)
    {
        if (args.Length == 0)
            return null;

        var options = new SnapshotOptions();
        var i = 0;

        // First positional arg is the XAML file
        if (i < args.Length && !args[i].StartsWith('-'))
        {
            options.XamlFile = args[i++];
        }
        else
        {
            return null;
        }

        while (i < args.Length)
        {
            switch (args[i])
            {
                case "--width" or "-w" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out var w) || w <= 0)
                    {
                        Console.Error.WriteLine($"Error: Invalid width: {args[i]}");
                        return null;
                    }
                    options.Width = w;
                    break;

                case "--height" or "-h" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out var h) || h <= 0)
                    {
                        Console.Error.WriteLine($"Error: Invalid height: {args[i]}");
                        return null;
                    }
                    options.Height = h;
                    break;

                case "--frames" or "-f" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out var f) || f <= 0)
                    {
                        Console.Error.WriteLine($"Error: Invalid frames: {args[i]}");
                        return null;
                    }
                    options.Frames = f;
                    break;

                case "--help":
                    return null;

                default:
                    Console.Error.WriteLine($"Error: Unknown argument: {args[i]}");
                    return null;
            }
            i++;
        }

        return options;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("""
            Usage: terminalninja-snapshot <file.xaml> [options]

            Renders a XAML layout to ANSI escape sequences on stdout.

            Options:
              --width, -w <cols>     Viewport width in columns (default: 80)
              --height, -h <rows>    Viewport height in rows (default: 24)
              --frames, -f <count>   Number of frames to render (default: 1)
              --help                 Show this help message

            Examples:
              terminalninja-snapshot layout.xaml
              terminalninja-snapshot layout.xaml --width 120 --height 40
              terminalninja-snapshot layout.xaml -w 80 -h 24 -f 3 > snapshot.ansi
            """);
    }
}

internal sealed class SnapshotOptions
{
    public string XamlFile { get; set; } = "";
    public int Width { get; set; } = 80;
    public int Height { get; set; } = 24;
    public int Frames { get; set; } = 1;
}
