using TerminalNinja.Core.Elements;
using TerminalNinja.Core.Primitives;
using TerminalNinja.Core.Rendering;
using TerminalNinja.Core.Styling;

// TerminalNinja Sample - High-Performance TUI Framework Demo
Console.WriteLine("TerminalNinja Sample - Rendering demo...");

using var renderer = new Renderer();

// Create a centered title box
var titleBox = new Rectangle
{
    X = Size.Percent(20),
    Y = Size.Absolute(2),
    Width = Size.Percent(60),
    Height = Size.Absolute(5),
    HorizontalAlignment = Alignment.Start,
    VerticalAlignment = Alignment.Start,
    Border = Border.Single(Color.Cyan),
    BackgroundColor = new Color(20, 20, 40),
    ForegroundColor = Color.Cyan
};

// Create a main content box
var contentBox = new Rectangle
{
    X = Size.Percent(10),
    Y = Size.Absolute(9),
    Width = Size.Percent(80),
    Height = Size.Absolute(15),
    HorizontalAlignment = Alignment.Start,
    VerticalAlignment = Alignment.Start,
    Border = Border.Rounded(Color.Green),
    BackgroundColor = new Color(10, 30, 10),
    ForegroundColor = Color.Green
};

// Create a footer box at the bottom
var footerBox = new Rectangle
{
    X = Size.Percent(5),
    Y = Size.Percent(85),
    Width = Size.Percent(90),
    Height = Size.Absolute(3),
    HorizontalAlignment = Alignment.Start,
    VerticalAlignment = Alignment.Start,
    Border = Border.Double(Color.Yellow),
    BackgroundColor = new Color(40, 40, 20),
    ForegroundColor = Color.Yellow
};

// Create small accent boxes
var accentBox1 = new Rectangle
{
    X = Size.Percent(5),
    Y = Size.Percent(50),
    Width = Size.Absolute(20),
    Height = Size.Absolute(8),
    HorizontalAlignment = Alignment.Start,
    VerticalAlignment = Alignment.Start,
    Border = Border.Single(Color.Magenta),
    BackgroundColor = new Color(40, 10, 40),
    ForegroundColor = Color.Magenta
};

var accentBox2 = new Rectangle
{
    X = Size.Percent(75),
    Y = Size.Percent(50),
    Width = Size.Absolute(20),
    Height = Size.Absolute(8),
    HorizontalAlignment = Alignment.Start,
    VerticalAlignment = Alignment.Start,
    Border = Border.Single(Color.Red),
    BackgroundColor = new Color(40, 10, 10),
    ForegroundColor = Color.Red
};

// Clear and draw all elements
renderer.Clear();
renderer.Draw(titleBox);
renderer.Draw(contentBox);
renderer.Draw(footerBox);
renderer.Draw(accentBox1);
renderer.Draw(accentBox2);

// Present to screen with zero-allocation diffing
renderer.Present();

Console.WriteLine("\nRendering complete! The TUI framework is working.");
Console.WriteLine("Features demonstrated:");
Console.WriteLine("  - Zero-allocation cell-level diffing");
Console.WriteLine("  - 24-bit true color support");
Console.WriteLine("  - Multiple border styles (single, double, rounded)");
Console.WriteLine("  - Absolute and relative positioning/sizing");
Console.WriteLine("  - C# 13 \\e escape sequences");

// Wait a moment to see output, then exit gracefully
if (!Console.IsInputRedirected)
{
    Console.WriteLine("\nPress any key to exit...");
    Console.ReadKey(intercept: true);
}
else
{
    Thread.Sleep(1000);
}