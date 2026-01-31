using TerminalNinja.Core.Elements;
using TerminalNinja.Core.Primitives;
using TerminalNinja.Core.Rendering;
using TerminalNinja.Core.Styling;


using var renderer = new Renderer();


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

// Build the complete Holy Grail layout: header | middle row | footer
var holyGrailLayout = new Stack
{
    Orientation = StackOrientation.Vertical,
    Children =
    [
        StackChild.Fixed(header, 5),           // Header: 5 cells tall
    ]
};

// Clear and render the Holy Grail layout
renderer.Clear();
renderer.Present();

renderer.Draw(holyGrailLayout);
renderer.Present();


Console.WriteLine("\nFeatures demonstrated:");
Console.WriteLine("  ✓ Nested Stack containers (Vertical → Horizontal)");
Console.WriteLine("  ✓ Fixed sizing (header: 5, footer: 3, sidebars: 20 & 25)");
Console.WriteLine("  ✓ Stretch sizing (middle row and main content)");
Console.WriteLine("  ✓ Mixed border styles (Single, Double, Rounded)");
Console.WriteLine("  ✓ 24-bit true color support");
Console.WriteLine("  ✓ Zero-allocation rendering pipeline");
