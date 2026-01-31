using TerminalNinja.Core.Elements;
using TerminalNinja.Core.Primitives;
using TerminalNinja.Core.Rendering;
using TerminalNinja.Core.Styling;

// TerminalNinja Sample - Holy Grail Layout Demo
Console.WriteLine("TerminalNinja Sample - Holy Grail Layout Demo");
Console.WriteLine("Demonstrating nested Stack containers for classic web layout\n");

using var renderer = new Renderer();

Console.WriteLine($"Terminal size: {renderer.Width}x{renderer.Height}");
Console.WriteLine($"Viewport: {renderer.Viewport}\n");

// Define colors for each section
var headerColor = new Color(30, 30, 80);      // Dark blue
var footerColor = new Color(40, 40, 40);       // Dark gray
var leftSidebarColor = new Color(20, 60, 20); // Dark green
var rightSidebarColor = new Color(60, 20, 20); // Dark red
var mainContentColor = new Color(15, 15, 15);  // Very dark gray

// Create header
var header = new Rectangle
{
    BackgroundColor = headerColor,
    ForegroundColor = Color.Cyan,
    Border = Border.Single(Color.Cyan)
};

// Create left sidebar
var leftSidebar = new Rectangle
{
    BackgroundColor = leftSidebarColor,
    ForegroundColor = Color.Green,
    Border = Border.Single(Color.Green)
};

// Create main content area
var mainContent = new Rectangle
{
    BackgroundColor = mainContentColor,
    ForegroundColor = Color.White,
    Border = Border.Rounded(Color.White)
};

// Create right sidebar
var rightSidebar = new Rectangle
{
    BackgroundColor = rightSidebarColor,
    ForegroundColor = Color.Red,
    Border = Border.Single(Color.Red)
};

// Create footer
var footer = new Rectangle
{
    BackgroundColor = footerColor,
    ForegroundColor = Color.Yellow,
    Border = Border.Double(Color.Yellow)
};

// Build middle row: left sidebar | main content | right sidebar
var middleRow = new Stack
{
    Orientation = StackOrientation.Horizontal,
    Children =
    [
        StackChild.Fixed(leftSidebar, 20),    // Left sidebar: 20 cells wide
        StackChild.Stretch(mainContent),       // Main content: fills remaining space
        StackChild.Fixed(rightSidebar, 25)     // Right sidebar: 25 cells wide
    ]
};

// Build the complete Holy Grail layout: header | middle row | footer
var holyGrailLayout = new Stack
{
    Orientation = StackOrientation.Vertical,
    Children =
    [
        StackChild.Fixed(header, 5),           // Header: 5 cells tall
        StackChild.Stretch(middleRow),         // Middle row: fills remaining space
        StackChild.Fixed(footer, 3)            // Footer: 3 cells tall
    ]
};

// Clear and render the Holy Grail layout
renderer.Clear();
renderer.Draw(holyGrailLayout);
renderer.Present();

Console.WriteLine("\nHoly Grail Layout rendered successfully!");
Console.WriteLine("\nLayout Structure:");
Console.WriteLine("  ┌─────────────────────────────────────────┐");
Console.WriteLine("  │              HEADER (5 rows)            │ ← Fixed height");
Console.WriteLine("  ├──────────┬──────────────────┬───────────┤");
Console.WriteLine("  │   LEFT   │                  │   RIGHT   │");
Console.WriteLine("  │ SIDEBAR  │  MAIN CONTENT    │ SIDEBAR   │ ← Stretch (fills remaining)");
Console.WriteLine("  │ (20 col) │   (stretch)      │ (25 col)  │");
Console.WriteLine("  ├──────────┴──────────────────┴───────────┤");
Console.WriteLine("  │             FOOTER (3 rows)             │ ← Fixed height");
Console.WriteLine("  └─────────────────────────────────────────┘");

Console.WriteLine("\nFeatures demonstrated:");
Console.WriteLine("  ✓ Nested Stack containers (Vertical → Horizontal)");
Console.WriteLine("  ✓ Fixed sizing (header: 5, footer: 3, sidebars: 20 & 25)");
Console.WriteLine("  ✓ Stretch sizing (middle row and main content)");
Console.WriteLine("  ✓ Mixed border styles (Single, Double, Rounded)");
Console.WriteLine("  ✓ 24-bit true color support");
Console.WriteLine("  ✓ Zero-allocation rendering pipeline");

// Wait a moment to see output, then exit gracefully
if (!Console.IsInputRedirected)
{
    Console.WriteLine("\nPress any key to exit...");
    Console.ReadKey(intercept: true);
}
else
{
    Thread.Sleep(2000);
}
