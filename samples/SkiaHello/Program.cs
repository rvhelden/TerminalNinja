using TerminalNinja.Controls;
using TerminalNinja.Primitives;
using TerminalNinja.Skia;

namespace SkiaHello;

internal static class Program
{
    public static int Main()
    {
        // A trivial control tree: a coloured border with a text block inside. Renders entirely
        // through the SkiaCellSink path — no XAML loader, no theming, no Application.Current.
        var root = new Border
        {
            Background = new Color(0x1E, 0x1E, 0x2E),
            BorderBrush = new Color(0xCD, 0xD6, 0xF4),
            Padding = new Thickness(2, 1, 2, 1),
            Child = new TextBlock
            {
                Text = "TerminalNinja.Skia — hello",
                Foreground = new Color(0xA6, 0xE3, 0xA1),
                Background = new Color(0x1E, 0x1E, 0x2E),
            },
        };

        using var app = new SkiaApplication(new SkiaApplicationOptions
        {
            Title = "TerminalNinja.Skia — Step 6",
            CellsWide = 60,
            CellsTall = 10,
        });
        app.SetRoot(root);
        app.Run();
        return 0;
    }
}
