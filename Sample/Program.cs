using TerminalNinja.Core.Elements;
using TerminalNinja.Core.Primitives;
using TerminalNinja.Core.Rendering;
using TerminalNinja.Core.Styling;

// TerminalNinja Sample - Holy Grail Layout with Labels Demo
Console.WriteLine("TerminalNinja Sample - Holy Grail Layout with Labels Demo");
Console.WriteLine("Demonstrating nested Stack containers and text labels\n");

using var renderer = new Renderer();

Console.WriteLine($"Terminal size: {renderer.Width}x{renderer.Height}");
Console.WriteLine($"Viewport: {renderer.Viewport}\n");

// Define colors for each section
var headerColor = new Color(30, 30, 80);      // Dark blue
var footerColor = new Color(40, 40, 40);       // Dark gray
var leftSidebarColor = new Color(20, 60, 20); // Dark green
var rightSidebarColor = new Color(60, 20, 20); // Dark red
var mainContentColor = new Color(15, 15, 15);  // Very dark gray

// Create header with background and label
var header = new Rectangle
{
    BackgroundColor = headerColor,
    ForegroundColor = Color.Cyan,
    Border = Border.Single(Color.Cyan),
    Child = new Label
    {
        Text = "TerminalNinja Demo - Header",
        ForegroundColor = Color.Cyan,
        BackgroundColor = headerColor,
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center
    }
};

// Create left sidebar with background and label
var leftSidebar = new Rectangle
{
    BackgroundColor = leftSidebarColor,
    ForegroundColor = Color.Green,
    Border = Border.Single(Color.Green),
    Child = new Label
    {
        Text = "Menu",
        ForegroundColor = Color.Green,
        BackgroundColor = leftSidebarColor,
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Start,
        Padding = new Thickness(1, 1, 1, 0)
    }
};

// Create main content area with background and label
var mainContent = new Rectangle
{
    BackgroundColor = mainContentColor,
    ForegroundColor = Color.White,
    Border = Border.Rounded(Color.White),
    Child = new Label
    {
        Text = "Main Content Area - This is where your primary content goes. Try resizing the terminal!",
        ForegroundColor = Color.White,
        BackgroundColor = mainContentColor,
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
        Padding = new Thickness(2)
    }
};

// Create right sidebar with background and label
var rightSidebar = new Rectangle
{
    BackgroundColor = rightSidebarColor,
    ForegroundColor = Color.Red,
    Border = Border.Single(Color.Red),
    Child = new Label
    {
        Text = "Actions",
        ForegroundColor = Color.Red,
        BackgroundColor = rightSidebarColor,
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Start,
        Padding = new Thickness(1, 1, 1, 0)
    }
};

// Create footer with background and label
var footer = new Rectangle
{
    BackgroundColor = footerColor,
    ForegroundColor = Color.Yellow,
    Border = Border.Double(Color.Yellow),
    Child = new Label
    {
        Text = "Status: Ready | Press any key to exit",
        ForegroundColor = Color.Yellow,
        BackgroundColor = footerColor,
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center
    }
};

// Create toolbar demonstrating Rectangle + Stack + multiple elements pattern
// This shows the power of composition: Rectangle → Stack → [Label, Label, Label]
var toolbarColor = new Color(25, 25, 45);  // Dark blue-gray
var toolbar = new Rectangle
{
    BackgroundColor = toolbarColor,
    ForegroundColor = Color.Cyan,
    Border = Border.Single(Color.Cyan),
    Child = new Stack
    {
        Orientation = StackOrientation.Horizontal,
        Children =
        [
            // Left: Auto-sized buttons
            StackChild.Auto(new Label
            {
                Text = " [File] [Edit] [View] ",
                ForegroundColor = Color.White,
                BackgroundColor = toolbarColor
            }),
            
            // Center: Stretched title
            StackChild.Stretch(new Label
            {
                Text = "Rectangle + Stack Composition Demo",
                ForegroundColor = Color.Cyan,
                BackgroundColor = toolbarColor,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }),
            
            // Right: Auto-sized help/close buttons
            StackChild.Auto(new Label
            {
                Text = " [?] [X] ",
                ForegroundColor = Color.White,
                BackgroundColor = toolbarColor
            })
        ]
    }
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

// Build the complete Holy Grail layout: header | middle row | toolbar | footer
var holyGrailLayout = new Stack
{
    Orientation = StackOrientation.Vertical,
    Children =
    [
        StackChild.Fixed(header, 5),           // Header: 5 cells tall
        StackChild.Stretch(middleRow),         // Middle row: fills remaining space
        StackChild.Fixed(toolbar, 3),          // Toolbar: 3 cells tall (NEW!)
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
Console.WriteLine("  │    TOOLBAR: [L] | Title | [R]           │ ← Fixed height (3 rows)");
Console.WriteLine("  ├───────────────────────────────────────────┤");
Console.WriteLine("  │             FOOTER (3 rows)             │ ← Fixed height");
Console.WriteLine("  └─────────────────────────────────────────┘");

Console.WriteLine("\nFeatures demonstrated:");
Console.WriteLine("  ✓ Nested Stack containers (Vertical → Horizontal)");
Console.WriteLine("  ✓ Rectangle elements with child Label composition");
Console.WriteLine("  ✓ Rectangle + Stack + multiple elements (toolbar pattern)");
Console.WriteLine("  ✓ Label elements with text rendering");
Console.WriteLine("  ✓ Text alignment (horizontal and vertical)");
Console.WriteLine("  ✓ Text wrapping for long content");
Console.WriteLine("  ✓ Padding and spacing");
Console.WriteLine("  ✓ Auto-sized and Stretch children in Stack");
Console.WriteLine("  ✓ Fixed sizing (header: 5, toolbar: 3, footer: 3, sidebars: 20 & 25)");
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
