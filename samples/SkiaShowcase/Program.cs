using TerminalNinja.Controls;
using TerminalNinja.Primitives;
using TerminalNinja.Skia;

namespace SkiaShowcase;

/// <summary>
/// A richer end-to-end sample for TerminalNinja.Skia: a Dark-themed window with a
/// TextBlock title, a TextBox, a CheckBox, and two Buttons. Tab cycles focus through
/// the interactive controls; Escape (or window close) quits.
/// </summary>
/// <remarks>
/// This exercises the chain SkiaApplication → inner Application → Application.Current →
/// FrameworkElement.ApplicationResourceLookup → built-in Dark theme resources. If any
/// link in that chain were broken the title's themed colors or the focus indicator
/// would render wrong.
/// </remarks>
internal static class Program
{
    public static int Main()
    {
        var status = new TextBlock
        {
            Text = "Idle. Tab to navigate, Enter on a button, Escape to quit.",
            Foreground = new Color(0xA6, 0xE3, 0xA1),
            Background = new Color(0x1E, 0x1E, 0x2E),
        };

        var name = new TextBox
        {
            Text = "world",
            Background = new Color(0x31, 0x32, 0x44),
            Foreground = new Color(0xCD, 0xD6, 0xF4),
        };

        var subscribe = new CheckBox
        {
            Content = "send me updates",
            Foreground = new Color(0xCD, 0xD6, 0xF4),
            Background = new Color(0x1E, 0x1E, 0x2E),
        };

        var greet = new Button
        {
            Content = "Greet",
            Foreground = new Color(0x1E, 0x1E, 0x2E),
            Background = new Color(0xA6, 0xE3, 0xA1),
        };
        greet.Click += () =>
        {
            var marker = (subscribe.IsChecked == true) ? "*" : string.Empty;
            status.Text = $"Hello, {name.Text}!{marker}";
        };

        var quit = new Button
        {
            Content = "Quit",
            Foreground = new Color(0x1E, 0x1E, 0x2E),
            Background = new Color(0xF3, 0x8B, 0xA8),
        };

        var root = new Border
        {
            Background = new Color(0x1E, 0x1E, 0x2E),
            BorderBrush = new Color(0xCD, 0xD6, 0xF4),
            Padding = new Thickness(2, 1, 2, 1),
            Child = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Children =
                {
                    new TextBlock
                    {
                        Text = "TerminalNinja.Skia showcase",
                        Foreground = new Color(0xF9, 0xE2, 0xAF),
                        Background = new Color(0x1E, 0x1E, 0x2E),
                        TextDecorations = TextDecorations.Bold,
                    },
                    status,
                    new TextBlock
                    {
                        Text = "Name:",
                        Foreground = new Color(0xCD, 0xD6, 0xF4),
                        Background = new Color(0x1E, 0x1E, 0x2E),
                    },
                    name,
                    subscribe,
                    greet,
                    quit,
                },
            },
        };

        using var app = new SkiaApplication(new SkiaApplicationOptions
        {
            Title = "TerminalNinja.Skia — Showcase",
            CellsWide = 60,
            CellsTall = 14,
        });

        // Wire Quit after construction so we can capture the app reference.
        quit.Click += () => app.Stop();

        app.ThemeName = "Dark";
        app.SetRoot(root);
        app.Run();
        return 0;
    }
}
