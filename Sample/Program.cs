using System.Text;

namespace Sample;

public static class Program
{
    public static void Main()
    {
        // Set console encoding to UTF-8
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        
        Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║              TerminalNinja Sample Selector                ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");
        Console.WriteLine("Choose a demo to run:\n");
        Console.WriteLine("  [1] Core Sample - Full interactive demo (built with C# code)");
        Console.WriteLine("  [2] XAML Sample - UI loaded from XAML file\n");
        Console.WriteLine("  [ESC] Exit\n");
        Console.Write("Enter your choice (1 or 2): ");
        
        while (true)
        {
            var key = Console.ReadKey(true);
            
            if (key.Key == ConsoleKey.Escape)
            {
                Console.WriteLine("\nExiting...\n");
                return;
            }
            
            if (key.KeyChar == '1')
            {
                Console.WriteLine("1 - Core Sample\n");
                Thread.Sleep(500);
                Console.Clear();
                CoreSample.Run();
                break;
            }
            
            if (key.KeyChar == '2')
            {
                Console.WriteLine("2 - XAML Sample\n");
                Thread.Sleep(500);
                Console.Clear();
                XamlSample.Run();
                break;
            }
        }
    }
}