using TerminalNinja.Core.App;
using TerminalNinja.Core.Elements;
using TerminalNinja.Core.Primitives;
using TerminalNinja.Core.Styling;

// TerminalNinja Interactive Application Showcase
Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
Console.WriteLine("║   TerminalNinja - Interactive TUI Framework Showcase     ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");
Console.WriteLine("Features:");
Console.WriteLine("  • Keyboard Navigation (TAB/Shift+TAB)");
Console.WriteLine("  • Mouse Support (Click & Hover)");
Console.WriteLine("  • Focus Management");
Console.WriteLine("  • Event-Driven Architecture");
Console.WriteLine("  • Zero-Allocation Rendering\n");
Console.WriteLine("Controls:");
Console.WriteLine("  TAB        - Next button");
Console.WriteLine("  Shift+TAB  - Previous button");
Console.WriteLine("  Enter/Space- Activate focused button");
Console.WriteLine("  Mouse      - Click and hover over buttons");
Console.WriteLine("  ESC        - Exit application\n");
Console.WriteLine("Watch the buttons change color:");
Console.WriteLine("  WHITE  - Normal state");
Console.WriteLine("  CYAN   - Focused (via Tab or click)");
Console.WriteLine("  YELLOW - Hovered (mouse over)\n");
Console.WriteLine("Starting in 2 seconds...\n");

Thread.Sleep(2000);

// Create application
using var app = new Application(new ApplicationOptions
{
    TargetFps = 60,
    EnableMouseTracking = true,
    EnableTabNavigation = true
});

// Application state
var clickCount = 0;
var lastAction = "Waiting for user input...";
var currentFocus = "None";
var currentHover = "None";

// Create action buttons in a toolbar
var newButton = new Button
{
    Text = "New",
    Width = Size.Absolute(12),
    Height = Size.Absolute(3),
    TabIndex = 0,
    FocusColor = Color.Cyan,
    HoverColor = Color.Yellow,
    ForegroundColor = Color.White,
    BackgroundColor = new Color(20, 20, 20)
};

var openButton = new Button
{
    Text = "Open",
    Width = Size.Absolute(12),
    Height = Size.Absolute(3),
    TabIndex = 1,
    FocusColor = Color.Cyan,
    HoverColor = Color.Yellow,
    ForegroundColor = Color.White,
    BackgroundColor = new Color(20, 20, 20)
};

var saveButton = new Button
{
    Text = "Save",
    Width = Size.Absolute(12),
    Height = Size.Absolute(3),
    TabIndex = 2,
    FocusColor = Color.Cyan,
    HoverColor = Color.Yellow,
    ForegroundColor = Color.White,
    BackgroundColor = new Color(20, 20, 20)
};

var helpButton = new Button
{
    Text = "Help",
    Width = Size.Absolute(12),
    Height = Size.Absolute(3),
    TabIndex = 3,
    FocusColor = Color.Cyan,
    HoverColor = Color.Yellow,
    ForegroundColor = Color.White,
    BackgroundColor = new Color(20, 20, 20)
};

// Action buttons
var submitButton = new Button
{
    Text = "Submit",
    Width = Size.Absolute(16),
    Height = Size.Absolute(3),
    TabIndex = 4,
    FocusColor = new Color(0, 255, 0),
    HoverColor = new Color(100, 255, 100),
    ForegroundColor = Color.White,
    BackgroundColor = new Color(0, 80, 0)
};

var cancelButton = new Button
{
    Text = "Cancel",
    Width = Size.Absolute(16),
    Height = Size.Absolute(3),
    TabIndex = 5,
    FocusColor = new Color(255, 100, 0),
    HoverColor = new Color(255, 200, 100),
    ForegroundColor = Color.White,
    BackgroundColor = new Color(80, 40, 0)
};

var exitButton = new Button
{
    Text = "Exit",
    Width = Size.Absolute(16),
    Height = Size.Absolute(3),
    TabIndex = 6,
    FocusColor = new Color(255, 0, 0),
    HoverColor = new Color(255, 100, 100),
    ForegroundColor = Color.White,
    BackgroundColor = new Color(80, 0, 0)
};

// Wire up button events
newButton.Click += () =>
{
    clickCount++;
    lastAction = "New document created";
    UpdateLayout();
};

openButton.Click += () =>
{
    clickCount++;
    lastAction = "Open dialog displayed";
    UpdateLayout();
};

saveButton.Click += () =>
{
    clickCount++;
    lastAction = "Document saved successfully";
    UpdateLayout();
};

helpButton.Click += () =>
{
    clickCount++;
    lastAction = "Help documentation opened";
    UpdateLayout();
};

submitButton.Click += () =>
{
    clickCount++;
    lastAction = "Form submitted!";
    UpdateLayout();
};

cancelButton.Click += () =>
{
    clickCount++;
    lastAction = "Operation cancelled";
    UpdateLayout();
};

exitButton.Click += () =>
{
    lastAction = "Exiting application...";
    UpdateLayout();
    Thread.Sleep(500);
    app.Exit();
};

void UpdateLayout()
{
    // Update focus and hover state strings
    var focusedButton = app.FocusManager.FocusedElement;
    currentFocus = focusedButton switch
    {
        _ when focusedButton == newButton => "New",
        _ when focusedButton == openButton => "Open",
        _ when focusedButton == saveButton => "Save",
        _ when focusedButton == helpButton => "Help",
        _ when focusedButton == submitButton => "Submit",
        _ when focusedButton == cancelButton => "Cancel",
        _ when focusedButton == exitButton => "Exit",
        _ => "None"
    };
    
    var hoveredButton = app.FocusManager.HoveredElement;
    currentHover = hoveredButton switch
    {
        _ when hoveredButton == newButton => "New",
        _ when hoveredButton == openButton => "Open",
        _ when hoveredButton == saveButton => "Save",
        _ when hoveredButton == helpButton => "Help",
        _ when hoveredButton == submitButton => "Submit",
        _ when hoveredButton == cancelButton => "Cancel",
        _ when hoveredButton == exitButton => "Exit",
        _ => "None"
    };
    
    // Header
    var header = new Rectangle
    {
        Height = Size.Absolute(5),
        BackgroundColor = new Color(30, 30, 80),
        ForegroundColor = Color.Cyan,
        Border = Border.Double(Color.Cyan),
        Child = new Label
        {
            Text = "TerminalNinja Interactive Showcase",
            ForegroundColor = Color.Cyan,
            BackgroundColor = new Color(30, 30, 80),
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        }
    };
    
    // Toolbar with action buttons
    var toolbar = new Rectangle
    {
        Height = Size.Absolute(5),
        BackgroundColor = new Color(25, 25, 45),
        ForegroundColor = Color.White,
        Border = Border.Single(Color.Cyan),
        Child = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children =
            [
                StackChild.Auto(new Rectangle
                {
                    Width = Size.Absolute(2),
                    BackgroundColor = new Color(25, 25, 45)
                }),
                StackChild.Auto(newButton),
                StackChild.Auto(openButton),
                StackChild.Auto(saveButton),
                StackChild.Auto(helpButton),
                StackChild.Stretch(new Rectangle
                {
                    BackgroundColor = new Color(25, 25, 45)
                })
            ]
        }
    };
    
    // Content area with instructions
    var contentArea = new Rectangle
    {
        BackgroundColor = new Color(15, 15, 15),
        ForegroundColor = Color.White,
        Border = Border.Rounded(Color.White),
        Child = new Label
        {
            Text = "Welcome to TerminalNinja!\n\nThis interactive demo showcases:\n• Tab Navigation - Press TAB to move between buttons\n• Mouse Support - Click buttons or hover to see effects\n• Focus States - Cyan border indicates focus\n• Hover States - Yellow border indicates hover\n• Color Theming - Different button styles\n\nTry interacting with the buttons above and below!",
            ForegroundColor = Color.White,
            BackgroundColor = new Color(15, 15, 15),
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Padding = new Thickness(4, 2, 4, 2)
        }
    };
    
    // Action buttons row
    var actionRow = new Rectangle
    {
        Height = Size.Absolute(5),
        BackgroundColor = new Color(20, 20, 30),
        ForegroundColor = Color.White,
        Border = Border.Single(Color.White),
        Child = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children =
            [
                StackChild.Stretch(new Rectangle { BackgroundColor = new Color(20, 20, 30) }),
                StackChild.Auto(submitButton),
                StackChild.Auto(new Rectangle
                {
                    Width = Size.Absolute(2),
                    BackgroundColor = new Color(20, 20, 30)
                }),
                StackChild.Auto(cancelButton),
                StackChild.Auto(new Rectangle
                {
                    Width = Size.Absolute(2),
                    BackgroundColor = new Color(20, 20, 30)
                }),
                StackChild.Auto(exitButton),
                StackChild.Stretch(new Rectangle { BackgroundColor = new Color(20, 20, 30) })
            ]
        }
    };
    
    // Status bar
    var statusBar = new Rectangle
    {
        Height = Size.Absolute(3),
        BackgroundColor = new Color(40, 40, 40),
        ForegroundColor = Color.Yellow,
        Border = Border.Single(Color.Yellow),
        Child = new Label
        {
            Text = $"Clicks: {clickCount} | Action: {lastAction} | Focus: {currentFocus} | Hover: {currentHover}",
            ForegroundColor = Color.Yellow,
            BackgroundColor = new Color(40, 40, 40),
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        }
    };
    
    // Build complete layout
    var layout = new Stack
    {
        Orientation = StackOrientation.Vertical,
        Children =
        [
            StackChild.Fixed(header, 5),
            StackChild.Fixed(toolbar, 5),
            StackChild.Stretch(contentArea),
            StackChild.Fixed(actionRow, 5),
            StackChild.Fixed(statusBar, 3)
        ]
    };
    
    app.RootElement = layout;
}

// Set initial layout
UpdateLayout();

// Run the application event loop
app.Run();

// Cleanup and goodbye message
Console.Clear();
Console.WriteLine("\n╔═══════════════════════════════════════════════════════════╗");
Console.WriteLine("║            Thanks for trying TerminalNinja!               ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");
Console.WriteLine($"Session Statistics:");
Console.WriteLine($"  • Total button clicks: {clickCount}");
Console.WriteLine($"  • Last action: {lastAction}");
Console.WriteLine($"\nTerminalNinja Features Demonstrated:");
Console.WriteLine($"  ✓ Interactive buttons with click events");
Console.WriteLine($"  ✓ Keyboard navigation (Tab/Shift+Tab)");
Console.WriteLine($"  ✓ Mouse support (click and hover)");
Console.WriteLine($"  ✓ Focus management with visual feedback");
Console.WriteLine($"  ✓ Event-driven architecture");
Console.WriteLine($"  ✓ Nested layouts (Stack + Rectangle)");
Console.WriteLine($"  ✓ Zero-allocation rendering");
Console.WriteLine($"  ✓ 24-bit true color support\n");
