# TerminalNinja.Xaml

XAML support for TerminalNinja terminal UI framework. Load terminal UIs from declarative XAML markup.

## Installation

Add a project reference to `TerminalNinja.Xaml.csproj`:

```xml
<ProjectReference Include="..\TerminalNinja.Xaml\TerminalNinja.Xaml.csproj" />
```

## Quick Start

### 1. Create a XAML file (e.g., `MainView.xaml`)

```xml
<Stack xmlns="clr-namespace:TerminalNinja.Core.Elements;assembly=TerminalNinja.Core"
       xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
       xmlns:e="clr-namespace:TerminalNinja.Core.Elements;assembly=TerminalNinja.Core"
       Orientation="Vertical">
    
    <Rectangle e:Stack.SizeMode="Fixed" e:Stack.FixedSize="5"
               BackgroundColor="#1E1E50"
               Border="Double">
        <Label x:Name="titleLabel"
               Text="Hello from XAML!"
               ForegroundColor="Cyan"
               HorizontalTextAlignment="Center"
               VerticalTextAlignment="Center" />
    </Rectangle>
    
    <Button x:Name="clickButton"
            e:Stack.SizeMode="Auto"
            Text="Click Me"
            Width="20"
            Height="3"
            FocusColor="Cyan"
            HoverColor="Yellow" />
    
    <Rectangle e:Stack.SizeMode="Stretch"
               BackgroundColor="#0F0F0F">
        <Label Text="Content area" />
    </Rectangle>
</Stack>
```

### 2. Load and use in C#

```csharp
using TerminalNinja.Xaml;
using TerminalNinja.Xaml.Extensions;

// Load from string
var xaml = File.ReadAllText("MainView.xaml");
var layout = TerminalXaml.Load<Stack>(xaml);

// Or load from file directly
var layout = TerminalXaml.LoadFromFile<Stack>("MainView.xaml");

// Find elements by name
var button = layout.FindByName<Button>("clickButton");
var label = layout.FindByName<Label>("titleLabel");

// Wire up events
if (button != null)
{
    button.Click += () => Console.WriteLine("Button clicked!");
}

// Use with Application
app.RootElement = layout;
```

## Supported Elements

All TerminalNinja core elements are supported:

- **Stack** - Horizontal/vertical layout container
- **Rectangle** - Bordered container with single child
- **Label** - Text display with wrapping and alignment
- **Button** - Interactive button with focus/hover states

## Type Converters

XAML strings are automatically converted to appropriate types:

### Size

```xml
<!-- Absolute pixels -->
Width="100"

<!-- Percentage of parent -->
Width="50%"

<!-- Fill remaining space -->
Width="*"
```

### Color

```xml
<!-- Named colors -->
ForegroundColor="Cyan"
BackgroundColor="White"

<!-- Hex RGB -->
BackgroundColor="#1E1E1E"

<!-- RGB values -->
ForegroundColor="255,128,0"
```

### Border

```xml
<!-- Named styles -->
Border="Single"
Border="Double"
Border="Rounded"
Border="Bold"
Border="Thick"

<!-- With custom color -->
<!-- Note: Color customization requires C# -->
```

### Thickness (Padding)

```xml
<!-- All sides -->
Padding="4"

<!-- Horizontal, Vertical -->
Padding="4,2"

<!-- Left, Top, Right, Bottom -->
Padding="1,2,3,4"
```

### Enums

All enums use their name as a string:

```xml
Orientation="Vertical"
Orientation="Horizontal"

HorizontalTextAlignment="Left"
HorizontalTextAlignment="Center"
HorizontalTextAlignment="Right"

TextWrapping="NoWrap"
TextWrapping="Wrap"

TextTrimming="None"
TextTrimming="CharacterEllipsis"
```

## Stack Attached Properties

Control how child elements are sized in a Stack:

```xml
<Stack xmlns="clr-namespace:TerminalNinja.Core.Elements;assembly=TerminalNinja.Core"
       xmlns:e="clr-namespace:TerminalNinja.Core.Elements;assembly=TerminalNinja.Core"
       Orientation="Vertical">
    
    <!-- Auto: Use element's natural size -->
    <Label Text="Auto-sized" e:Stack.SizeMode="Auto" />
    
    <!-- Fixed: Use specific size -->
    <Rectangle e:Stack.SizeMode="Fixed" e:Stack.FixedSize="10" />
    
    <!-- Stretch: Fill remaining space -->
    <Rectangle e:Stack.SizeMode="Stretch" />
</Stack>
```

**Note**: The namespace prefix (`e:` in this example) must reference `TerminalNinja.Core.Elements` where the Stack class is defined.

## x:Name Support

Use `x:Name` to identify elements for lookup:

```xml
<Label x:Name="statusLabel" Text="Ready" />
```

```csharp
var label = root.FindByName<Label>("statusLabel");
if (label != null)
{
    label.Text = "Updated!";
}
```

## Content Properties

Some elements have default content properties:

### Rectangle.Child

```xml
<!-- Explicit -->
<Rectangle>
    <Rectangle.Child>
        <Label Text="Inside" />
    </Rectangle.Child>
</Rectangle>

<!-- Implicit (preferred) -->
<Rectangle>
    <Label Text="Inside" />
</Rectangle>
```

### Stack.Children

```xml
<!-- Explicit -->
<Stack>
    <Stack.Children>
        <Label Text="First" />
        <Label Text="Second" />
    </Stack.Children>
</Stack>

<!-- Implicit (preferred) -->
<Stack>
    <Label Text="First" />
    <Label Text="Second" />
</Stack>
```

## XAML Namespace Declaration

Always declare the required namespaces at the root element:

```xml
<RootElement xmlns="clr-namespace:TerminalNinja.Core.Elements;assembly=TerminalNinja.Core"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:e="clr-namespace:TerminalNinja.Core.Elements;assembly=TerminalNinja.Core">
```

- **Default xmlns**: Makes element names shorter (no prefix needed)
- **xmlns:x**: Required for `x:Name` support
- **xmlns:e**: Required for Stack attached properties (or use any prefix you like)

## Example: Complete Application

**MainView.xaml:**

```xml
<Stack xmlns="clr-namespace:TerminalNinja.Core.Elements;assembly=TerminalNinja.Core"
       xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
       xmlns:e="clr-namespace:TerminalNinja.Core.Elements;assembly=TerminalNinja.Core"
       Orientation="Vertical">
    
    <Rectangle e:Stack.SizeMode="Fixed" e:Stack.FixedSize="3"
               BackgroundColor="#282828"
               Border="Single">
        <Label x:Name="titleLabel"
               Text="XAML Demo App"
               HorizontalTextAlignment="Center"
               VerticalTextAlignment="Center" />
    </Rectangle>
    
    <Rectangle e:Stack.SizeMode="Stretch">
        <Stack Orientation="Horizontal">
            <Button x:Name="btnNew"
                    e:Stack.SizeMode="Auto"
                    Text="New"
                    Width="12"
                    Height="3"
                    TabIndex="0" />
            
            <Button x:Name="btnOpen"
                    e:Stack.SizeMode="Auto"
                    Text="Open"
                    Width="12"
                    Height="3"
                    TabIndex="1" />
            
            <Rectangle e:Stack.SizeMode="Stretch" />
        </Stack>
    </Rectangle>
    
    <Rectangle e:Stack.SizeMode="Fixed" e:Stack.FixedSize="3"
               BackgroundColor="#1E1E1E">
        <Label x:Name="statusLabel"
               Text="Ready"
               HorizontalTextAlignment="Center" />
    </Rectangle>
</Stack>
```

**Program.cs:**

```csharp
using TerminalNinja.Core.App;
using TerminalNinja.Core.Elements;
using TerminalNinja.Xaml;
using TerminalNinja.Xaml.Extensions;

using var app = new Application();

// Load UI
var layout = TerminalXaml.LoadFromFile<Stack>("MainView.xaml");

// Find and wire up elements
var btnNew = layout.FindByName<Button>("btnNew");
var btnOpen = layout.FindByName<Button>("btnOpen");
var statusLabel = layout.FindByName<Label>("statusLabel");

if (btnNew != null)
    btnNew.Click += () => statusLabel!.Text = "New clicked!";

if (btnOpen != null)
    btnOpen.Click += () => statusLabel!.Text = "Open clicked!";

// Run
app.RootElement = layout;
app.Run();
```

## Limitations

Current limitations (may be addressed in future versions):

- No data binding support
- No markup extensions
- No resource dictionaries
- No styles or templates
- No custom namespaces or type converters outside the library
- Border color customization requires C# (not expressible in XAML string)

## Architecture

The XAML support is built on **Portable.Xaml** (v0.26.0) with custom type converters and a custom schema context that:

- Dynamically adds `RuntimeNamePropertyAttribute` for x:Name support
- Dynamically adds `ContentPropertyAttribute` for implicit children
- Registers type converters for Size, Color, Border, Thickness, and enums
- Keeps the core library (`TerminalNinja.Core`) independent of XAML/Portable.Xaml

## See Also

- [Sample/DemoLayout.xaml](../Sample/DemoLayout.xaml) - Example XAML layout file
- [TerminalNinja.Xaml.Tests](../TerminalNinja.Xaml.Tests/) - Comprehensive test suite with examples
