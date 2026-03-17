# AGENTS.md - Developer Guide for TerminalNinja

This document provides essential information for AI coding agents working in the TerminalNinja codebase.

## Project Overview

- **Language**: C# 13 (latest)
- **Framework**: .NET 10.0-windows (requires Windows Desktop framework for IDE XAML support)
- **Platform**: Windows-only (due to System.Windows.Markup dependency for IDE IntelliSense)
- **Test Framework**: TUnit v1.12.93
- **IDE**: JetBrains Rider (optional)
- **Solution Structure**: 
  - `TerminalNinja/` - Core library (terminal UI framework with XAML support)
  - `TerminalNinja.Tests/` - Test project (492 tests, all passing)
  - `Sample/` - Sample console application demonstrating XAML usage

## Build & Test Commands

### Building the Project

```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build TerminalNinja/TerminalNinja.csproj

# Build in Release mode
dotnet build -c Release

# Clean and rebuild
dotnet clean && dotnet build
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run tests for specific project
dotnet test TerminalNinja.Tests/TerminalNinja.Tests.csproj

# Run tests with detailed output
dotnet test -v detailed

# List all discovered tests without running them
dotnet test --list-tests
```

#### TUnit test filtering
TUnit supports several operators for building complex filters:

Wildcard matching: Use * for pattern matching (e.g., LoginTests* matches LoginTests, LoginTestsSuite, etc.)
Equality: Use = for exact match (e.g., [Category=Unit])
Negation: Use != for excluding values (e.g., [Category!=Performance])
AND operator: Use & to combine conditions (e.g., [Category=Unit]&[Priority=High])
OR operator: Use | to match either condition within a single path segment - requires parentheses (e.g., /*/*/(Class1)|(Class2)/*)
For full information on the treenode filters, see Microsoft's documentation

So an example could be:

dotnet run --treenode-filter /*/*/LoginTests/* - To run all tests in the class LoginTests

or

dotnet run --treenode-filter /*/*/*/AcceptCookiesTest - To run all tests with the name AcceptCookiesTest

TUnit also supports filtering by your own properties. So you could do:

dotnet run --treenode-filter /*/*/*/*[MyFilterName=*SomeValue*]

And if your test had a property with the name "MyFilterName" and its value contained "SomeValue", then your test would be executed.


### Running the Sample Application

```bash
# Run the sample console app
dotnet run --project Sample/Sample.csproj
```

## Architecture Overview

TerminalNinja is a WPF-like terminal UI framework with XAML support:

- **Controls** (`TerminalNinja.Controls`): UI controls (StackPanel, Grid, ItemsControl, Window, Border, Button, TextBlock, ContentControl, ButtonBase)
- **DependencySystem** (`TerminalNinja.DependencySystem`): DependencyObject, DependencyProperty, PropertyMetadata
- **Primitives** (`TerminalNinja.Primitives`): Basic types (Color, Size, Rect, Thickness, etc.)
- **Buffers** (`TerminalNinja.Buffers`): Cell-based rendering buffers
- **Styling** (`TerminalNinja.Styling`): Style and Setter for control theming
- **Resources** (`TerminalNinja.Resources`): ResourceDictionary for shared resources
- **XAML** (`TerminalNinja.Xaml`): XAML loading, StaticResource, data binding support
- **App** (`TerminalNinja.App`): Application class with event loop
- **Rendering** (`TerminalNinja.Rendering`): ANSI terminal renderer
- **Input** (`TerminalNinja.Input`): Keyboard and mouse input handling

### Key Design Patterns

- **WPF-inspired**: Uses WPF terminology (Control, FrameworkElement, Content, etc.)
- **XAML-first**: Supports declarative UI with XAML markup
- **Attached properties**: StackPanel.SizeMode, Grid.Row/Column for layout control
- **Data binding**: `{Binding PropertyName}` support with INotifyPropertyChanged
- **Static resources**: `{StaticResource KeyName}` for reusable values
- **Styles**: Apply consistent theming with Style/Setter pattern

## Important Reference Sources

Portable.Xaml
e:\thirdparty\Portable.Xaml\

Spectre.Console
e:\thirdparty\spectre\

Wpf
e:\thirdparty\wpf\src\Microsoft.DotNet.Wpf\

## Code Style Guidelines

### General Principles

- Use **nullable reference types** - all projects have `<Nullable>enable</Nullable>`
- Use **implicit usings** - avoid redundant using statements for common namespaces
- Use **file-scoped namespaces** for cleaner code
- Follow standard .NET naming conventions
- Write async code with `async/await` by default

### Imports and Usings

**Implicit Global Usings (Auto-imported for all projects):**
- `System`, `System.Collections.Generic`, `System.IO`, `System.Linq`
- `System.Net.Http`, `System.Threading`, `System.Threading.Tasks`

**Test Project Global Usings (in GlobalUsings.cs):**
```csharp
global using TUnit.Core;
global using TUnit.Assertions;
global using TUnit.Assertions.Extensions;
global using TerminalNinja.Primitives;
global using TerminalNinja.Buffers;
global using TerminalNinja.Controls;  // Note: Controls not Elements
global using TerminalNinja.Styling;
global using TerminalNinja.Input;
global using TerminalNinja.Xaml;
global using TerminalNinja.Xaml.Extensions;
global using TerminalNinja.Resources;
global using TerminalNinja.Tests.Helpers;
```

**Guidelines:**
- Do NOT add explicit usings for types covered by implicit usings
- Place project-specific global usings in `GlobalUsings.cs`
- Order explicit usings: System namespaces first, then third-party, then project namespaces
- Remove unused usings (IDE will warn)

### Namespace and File Structure

```csharp
namespace TerminalNinja.ComponentName;

public class ClassName
{
    // Implementation
}
```

- Use **file-scoped namespaces** (single line, no braces)
- One public type per file
- File name must match the primary type name
- Namespace should match folder structure: `TerminalNinja.{FolderPath}`

### Naming Conventions

| Element            | Convention        | Example                               |
|--------------------|-------------------|---------------------------------------|
| Namespaces         | PascalCase        | `TerminalNinja.Controls`              |
| Classes/Interfaces | PascalCase        | `UIElement`, `FrameworkElement`, `StackPanel` |
| Methods            | PascalCase        | `Render`, `CalculateBounds`           |
| Properties         | PascalCase        | `Content`, `Width`, `IsEnabled`       |
| Fields (private)   | camelCase with _  | `_content`, `_children`               |
| Parameters         | camelCase         | `control`, `parentBounds`             |
| Local variables    | camelCase         | `result`, `bounds`                    |
| Constants          | PascalCase        | `MaxRetryCount`, `DefaultTimeout`     |
| Async methods      | Suffix with Async | `RenderAsync`, `ProcessAsync`         |

**Important Terminology:**
- Use **"control"** not "element" (aligns with WPF conventions)
- Use **"Content"** for child control properties (e.g., `ContentControl.Content`, `Window.Content`)
- `FrameworkElement` keeps its name (matches WPF exactly)

### Type and Null Safety

```csharp
// Always annotate nullability explicitly
public string? GetOptionalValue() => null;  // nullable return
public string GetRequiredValue() => "value";  // non-nullable return

// Use nullable value types when appropriate
public int? TryParse(string input) { ... }

// Validate parameters
public void ProcessCommand(string command)
{
    ArgumentNullException.ThrowIfNull(command);
    // Implementation
}
```

### Async/Await Patterns

```csharp
// Prefer async methods that return Task or Task<T>
public async Task<Result> ExecuteAsync(CancellationToken cancellationToken = default)
{
    var result = await SomeAsyncOperation(cancellationToken);
    return result;
}

// Use ConfigureAwait(false) in library code when safe
var data = await ReadDataAsync().ConfigureAwait(false);

// Pass CancellationToken as the last parameter
public async Task ProcessAsync(string input, CancellationToken cancellationToken)
```

### Error Handling

```csharp
// Use specific exception types
throw new ArgumentException($"Invalid command: {command}", nameof(command));
throw new InvalidOperationException("Service not initialized");

// Catch specific exceptions
try
{
    await ExecuteCommandAsync();
}
catch (CommandException ex)
{
    // Handle specific error
}
catch (Exception ex)
{
    // Log and rethrow or wrap
    throw new ApplicationException("Command execution failed", ex);
}
```

## Testing Guidelines

### Test Structure (TUnit Framework)

```csharp
namespace TerminalNinja.Tests.Unit.Controls;

public class StackPanelTests
{
    [Test]
    public async Task Render_AutoChild_UsesControlPreferredSize()
    {
        // Arrange
        var stackPanel = new StackPanel();
        var textBlock = new TextBlock { Text = "Test" };
        stackPanel.Children.Add(textBlock);
        
        // Act
        var buffer = new CellBuffer(20, 10);
        stackPanel.Render(buffer, new Rect(0, 0, 20, 10));
        
        // Assert
        await Assert.That(buffer.GetCell(0, 0).Char).IsEqualTo('T');
    }
    
    [Test]
    public async Task Children_NullControl_ThrowsException()
    {
        // Arrange
        var stackPanel = new StackPanel();
        
        // Act & Assert
        await Assert.That(() => stackPanel.Children.Add(null!))
            .ThrowsExactly<ArgumentNullException>();
    }
}
```

### Test Naming

- Pattern: `MethodName_Scenario_ExpectedBehavior`
- Examples:
  - `Render_AutoChild_UsesControlPreferredSize`
  - `Parse_EmptyString_ThrowsArgumentException`
  - `ProcessAsync_WithCancellation_StopsGracefully`

### TUnit Assertions

```csharp
// Boolean assertions
await Assert.That(value).IsTrue();
await Assert.That(value).IsFalse();

// Equality assertions
await Assert.That(actual).IsEqualTo(expected);
await Assert.That(actual).IsNotEqualTo(other);

// Null assertions
await Assert.That(value).IsNull();
await Assert.That(value).IsNotNull();

// Exception assertions
await Assert.That(action).ThrowsExactly<ExceptionType>();

// String assertions
await Assert.That(text).Contains("substring");
```

### Special Test Considerations

- All tests are **async** - use `async Task` and `await Assert.That(...)`
- Tests should reference file locations: `TerminalNinja/Controls/StackPanel.cs:123`

## Git Workflow

- **Branch naming**: `feature/description`, `bugfix/issue-name`, `refactor/component`
- **Commit messages**: Use conventional commits format
  - `feat: add Grid control with row/column support`
  - `fix: handle null content in Border rendering`
  - `refactor: rename Element to Control for WPF alignment`
  - `test: add tests for StaticResource resolution`
  - `docs: update AGENTS.md with current project state`

## Important Notes

- All tests are **async** - use `async Task` and `await Assert.That(...)`
- No linting configuration yet - follow standard .NET conventions
- Target framework is **.NET 10.0** - use latest C# features
- Keep code coverage high - add tests for new functionality
- Prefer composition over inheritance
- Keep methods small and focused (single responsibility)
- Document public APIs with XML comments

## Project Files to Never Modify

- `bin/`, `obj/` - Build output directories (gitignored)
- `*.user` files - User-specific IDE settings
- `/.vs/`, `/.idea/` - IDE-specific folders

## Adding New Files

When creating new source files in `TerminalNinja/`:
1. Place in appropriate folder matching the namespace
2. Use file-scoped namespaces
3. Ensure nullable reference types are handled correctly
4. Add corresponding test file in `TerminalNinja.Tests/` with matching structure
5. Follow naming convention: `ComponentName.cs` for implementation, `ComponentNameTests.cs` for tests

When creating new controls:
1. Place in `TerminalNinja/Controls/` folder
2. Choose the appropriate base class:
   - `Control` — for interactive controls with Background/Foreground/Padding (Focusable=true by default)
   - `FrameworkElement` — for non-interactive visual elements (e.g., TextBlock, Border)
   - `ContentControl` — for controls with a single `Content` child
   - `Panel` — for layout containers with a `Children` collection
   - `ItemsControl` — for data-bound collections
   - `ButtonBase` — for clickable controls
3. Input handling: Override `OnKeyEvent`/`OnMouseEvent` (inherited from UIElement) for keyboard/mouse input
4. Add `[ContentProperty]` and `[RuntimeNameProperty]` attributes if applicable
5. Add `[TypeConverter]` attribute if custom XAML parsing is needed
6. Update tests in `TerminalNinja.Tests/Unit/Controls/`

## XAML Support

### Type Converters

All types that can be used in XAML should have a `[TypeConverter]` attribute:

```csharp
[TypeConverter(typeof(ColorTypeConverter))]
public readonly record struct Color { ... }
```

Existing converters:
- `ColorTypeConverter` - Parses colors like "Red", "#FF0000", "rgb(255,0,0)"
- `SizeTypeConverter` - Parses sizes like "Auto", "Stretch", "10"
- `ThicknessTypeConverter` - Parses thickness like "5", "5,10", "5,10,5,10"
- `BorderTypeConverter` - Parses border styles like "Single", "Double", "Rounded" (for `BorderStyle` struct)
- `GridLengthTypeConverter` - Parses grid lengths like "Auto", "*", "2*", "100"

### XAML Namespace

All CLR namespaces are mapped to a single XAML namespace in `Properties/AssemblyInfo.cs`:

```csharp
using Portable.Xaml.Markup;
using SWM = System.Windows.Markup;

// --- Portable.Xaml attributes (for runtime) ---
[assembly: XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Controls")]
[assembly: XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Primitives")]
// etc.

// --- System.Windows.Markup attributes (for IDE IntelliSense) ---
[assembly: SWM.XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Controls")]
[assembly: SWM.XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Primitives")]
// etc.
```

**Why duplicate attributes?**
- `Portable.Xaml.Markup.XmlnsDefinition` - Used by Portable.Xaml at runtime
- `System.Windows.Markup.XmlnsDefinition` - Recognized by Rider/Visual Studio for XAML IntelliSense
- Both are required for full IDE + runtime support

### Loading XAML

```csharp
// Load from string
var window = TerminalXaml.Parse<Window>(xamlString);

// Load from file
var window = TerminalXaml.LoadFromFile<Window>("DemoLayout.xaml");

// Show window
window.Show();  // Sets Application.Current.RootControl = window
```

## Recent Changes (Feb-Mar 2026)

### DependencyProperty Conversion (Mar 2026)

All ~43 CLR/auto properties across the entire control hierarchy have been converted to DependencyProperty-backed properties, matching WPF's property system. Attached properties (StackPanel.SizeMode/FixedSize, Grid.Row/Column/RowSpan/ColumnSpan) now use `DependencyProperty.RegisterAttached` instead of the old `AttachedPropertyStore`.

**Pattern for regular DPs:**
```csharp
public static readonly DependencyProperty XxxProperty =
    DependencyProperty.Register(nameof(Xxx), typeof(T), typeof(OwnerClass),
        new FrameworkPropertyMetadata(defaultValue, affectsRender: true));

public T Xxx
{
    get => (T)GetValue(XxxProperty)!;
    set => SetValue(XxxProperty, value);
}
```

**Pattern for attached DPs:**
```csharp
public static readonly DependencyProperty XxxProperty =
    DependencyProperty.RegisterAttached("Xxx", typeof(T), typeof(OwnerClass),
        new PropertyMetadata(defaultValue));

public static T GetXxx(DependencyObject d) => (T)d.GetValue(XxxProperty)!;
public static void SetXxx(DependencyObject d, T value) => d.SetValue(XxxProperty, value);
```

**Properties converted per class:**

| Class | DependencyProperties |
|-------|---------------------|
| UIElement | Visibility, IsEnabled, Focusable, IsFocused, IsMouseOver |
| FrameworkElement | HorizontalAlignment, VerticalAlignment, Name, DataContext, Style |
| Control | Background, Foreground, Padding, BorderStyle, TabIndex, Template |
| Panel | Background |
| StackPanel | Orientation, CrossAxisAlignment + attached SizeMode, FixedSize |
| Grid | Attached Row, Column, RowSpan, ColumnSpan |
| ContentControl | Content (with PropertyChangedCallback for Parent management) |
| ButtonBase | Command (with PropertyChangedCallback for CanExecuteChanged), CommandParameter |
| Button | Text, FocusColor, HoverColor, Width, Height |
| TextBlock | Text, Foreground, Background, Width, Height, HorizontalTextAlignment, VerticalTextAlignment, TextWrapping, TextTrimming, Padding |
| Border | Background, Foreground, BorderStyle, Width, Height, Child (with PropertyChangedCallback) |
| Window | Title, Width, Height |
| ItemsControl | ItemsSource (with PropertyChangedCallback), ItemTemplate, ItemsPanel |

**Metadata usage:**
- Visual properties use `FrameworkPropertyMetadata(default, affectsRender: true)` — triggers `InvalidateVisual()` on change
- Non-visual properties use `PropertyMetadata(default)` or `FrameworkPropertyMetadata(default, affectsRender: false)`
- Properties with side effects (Content, Child, Command, ItemsSource) use `PropertyChangedCallback`
- Nullable defaults use `(object?)null` to avoid CS8625

**Deleted artifacts:**
- `UIElement.SetProperty<T>()` helper — no longer needed since all properties use `DependencyObject.SetValue()`
- `AttachedPropertyStore` / `AttachedPropertyKey` — replaced by `DependencyProperty.RegisterAttached`

**No changes needed to infrastructure:**
- PropertyAccessorGenerator — DP CLR wrappers look identical to normal properties
- Style application — uses PropertyAccessorRegistry → CLR setter → `SetValue()` — works unchanged
- Bindings — `DependencyObject.SetValue()` raises INPC, so bindings work unchanged
- XAML loader — uses PropertyAccessorRegistry — works unchanged

### WPF-Aligned Class Hierarchy Refactoring (Feb-Mar 2026)

Major refactoring to align the entire class hierarchy with WPF's inheritance tree:

**New Class Hierarchy:**
```
DependencyObject (DependencySystem/)
  └── Visual (abstract) — visual tree parent/child, GetChildrenWithBounds
        └── UIElement (abstract) — Visibility, IsEnabled, Focusable, IsFocused, IsMouseOver, input events (OnKeyEvent/OnMouseEvent), HitTest, InvalidateVisual
              └── FrameworkElement (abstract) — resources, styles, DataContext, Name, Width/Height, HorizontalAlignment/VerticalAlignment, Margin, GetLogicalChildren()
                    ├── Control (abstract) — Background, Foreground, Padding, BorderStyle, TabIndex, Template (stub); Focusable=true by default
                    │     ├── ContentControl [ContentProperty("Content")] — Content, HasContent
                    │     │     ├── Window — Title, Show/Close
                    │     │     └── UserControl — Focusable=false
                    │     ├── ItemsControl — Items, ItemsSource, ItemTemplate, ItemsPanel
                    │     └── ButtonBase (abstract) — Command, Click
                    │           └── Button (sealed) — focus/hover rendering
                    ├── Panel (abstract) [ContentProperty("Children")] — Children (IList<UIElement>), Background
                    │     ├── StackPanel — Orientation, attached SizeMode/FixedSize
                    │     └── Grid (sealed) — RowDefinitions/ColumnDefinitions, attached Row/Column/RowSpan/ColumnSpan
                    ├── TextBlock — Text, Foreground, Background, wrapping, trimming
                    └── Border — BorderStyle, Background, Foreground, Child (UIElement?)
```

**Key Changes:**
- `IControl` interface removed entirely — replaced by class hierarchy
- `ControlBase` removed — replaced by `Visual` + `UIElement`
- `IFocusable` interface removed — members moved to `UIElement` (Focusable, IsFocused, IsMouseOver, input events)
- `Rectangle` renamed to `Border` (matches WPF naming)
- `Label` renamed to `TextBlock` (WPF's Label is a ContentControl; our Label behaves like TextBlock)
- `ForegroundColor`/`BackgroundColor` renamed to `Foreground`/`Background` everywhere
- X/Y properties removed from controls — positioning is parent layout's job
- `HorizontalAlignment`/`VerticalAlignment` added to `FrameworkElement`
- `FocusManager` now uses `UIElement` directly (FocusedElement/HoveredElement)
- `GetLogicalChildren()` virtual method added to FrameworkElement for DataContext propagation

### Panel/StackPanel/ItemsControl Refactoring (Feb 1, 2026)

Major architectural refactoring to align with WPF patterns by introducing `Panel` base class and removing the old `Stack` system:

**New Classes Created:**
- `Panel` (abstract) - Base class for all layout containers with automatic `Children` collection management
- `StackPanel` - WPF-aligned stack layout with `Orientation` property (Horizontal/Vertical)
- `ItemsControl` - Data-bound collection display with `ItemsSource`, `ItemTemplate`, and `ItemsPanel` properties
- `DataTemplate` - Template system for visualizing data items
- `Orientation` enum - `Horizontal` and `Vertical` values

**Deleted Classes:**
- `Stack` - Replaced by `StackPanel`
- `StackChild` - No longer needed (children added directly to `Children` collection)
- `StackOrientation` - Replaced by `Orientation` enum
- `StackChildTypeConverter` - No longer needed
- `ContentPropertyAttribute` - Now provided by System.Xaml in Windows Desktop framework

**Grid Updated:**
- Changed base class from `FrameworkElement` to `Panel`
- Now inherits `Children` collection from `Panel`
- Removed duplicate `_children` field

**Key Pattern Changes:**

Old Stack pattern:
```csharp
var stack = new Stack {
    Orientation = StackOrientation.Vertical,
    Children = [
        StackChild.Fixed(new Label { Text = "A" }, 5),
        StackChild.Stretch(new Label { Text = "B" })
    ]
};
```

New StackPanel pattern:
```csharp
var textBlock1 = new TextBlock { Text = "A" };
var textBlock2 = new TextBlock { Text = "B" };
StackPanel.SetSizeMode(textBlock1, ChildSizeMode.Fixed);
StackPanel.SetFixedSize(textBlock1, 5);
StackPanel.SetSizeMode(textBlock2, ChildSizeMode.Stretch);

var stackPanel = new StackPanel {
    Orientation = Orientation.Vertical,
    Children = { textBlock1, textBlock2 }
};
```

XAML changes:
- `<Stack>` → `<StackPanel>`
- `Stack.SizeMode` → `StackPanel.SizeMode`
- `Stack.FixedSize` → `StackPanel.FixedSize`

**Test Coverage:**
- Added `PanelTests.cs` (32 tests) - Tests Panel base class and ObservableControlCollection
- Added `StackPanelTests.cs` (34 tests) - Tests StackPanel layout, attached properties, and rendering
- Removed old Stack tests (StackTests, StackChildTests, etc.)
- **Total Tests**: 492 (up from 431)

### Windows Desktop Framework for IDE Support (Feb 1, 2026)

Added `Microsoft.WindowsDesktop.App` framework reference to enable IDE XAML IntelliSense:
- Changed TargetFramework from `net10.0` to `net10.0-windows` (Windows-only)
- Added duplicate `System.Windows.Markup.XmlnsDefinition` attributes alongside `Portable.Xaml.Markup` versions
- Rider/Visual Studio now recognize the XAML namespace and provide IntelliSense
- Runtime behavior unchanged - Portable.Xaml still used for XAML parsing

**Why Windows-only?**
- IDE XAML language services require `System.Windows.Markup` attributes
- These are only available in the Windows Desktop framework
- Portable.Xaml alone doesn't provide IDE support

### Element → Control Rename

The entire codebase was renamed from "Element" terminology to "Control" terminology to align with WPF conventions:

- **Namespaces**: `TerminalNinja.Elements` → `TerminalNinja.Controls`
- **Types**: `IElement` → (later removed entirely), `ElementBase` → (later replaced by Visual/UIElement)
- **Properties**: `Application.RootElement` → `Application.RootControl`
- **Variables**: All `element` parameters/variables renamed to `control`
- **Exception**: `FrameworkElement` keeps its name (matches WPF)

### TypeConverter Refactoring

Moved from runtime registration to declarative attributes:
- All types now have `[TypeConverter]` attributes directly on them
- Removed `TypeDescriptor.AddAttributes()` calls from `TerminalXamlSchemaContext`
- Simplified schema context to just inherit from `XamlSchemaContext`
- Deleted custom `EnumTypeConverter<T>` (using built-in `EnumConverter` now)

### XAML Features

- **StaticResource**: `{StaticResource KeyName}` markup extension with resource lookup
- **Data Binding**: `{Binding PropertyName}` with INotifyPropertyChanged support
- **Attached Properties**: StackPanel.SizeMode, StackPanel.FixedSize, Grid.Row, Grid.Column, etc.
- **Styles**: Style/Setter pattern with TargetType validation
- **Window pattern**: Window.Show() / Window.Close() with Application.Current.RootControl

## Current Test Stats

- **Total Tests**: 492
- **Status**: All passing
- **Test Coverage**: Unit tests for Controls, Styling, Resources, XAML loading, and rendering
