# AGENTS.md - Developer Guide for TerminalNinja

This document provides essential information for AI coding agents working in the TerminalNinja codebase.

## Project Overview

- **Language**: C# 13 (latest)
- **Framework**: .NET 10.0
- **Platform**: Cross-platform (Windows, Linux, macOS)
- **Test Framework**: TUnit v1.20.0
- **IDE**: JetBrains Rider (optional)
- **Solution Structure**:
    - `TerminalNinja/` - Core library (terminal UI framework with XAML support)
    - `TerminalNinja.Wasm/` - Library for bringing TerminalNinja to WebAssembly
    - `TerminalNinja.Cli/` - Program for generating a single frame for the given xaml
    - `TerminalNinja.Generators/` - Source generators (ControlFactory, PropertyAccessor, XamlClass)
    - `TerminalNinja.Tests/` - Test project (955 tests, all passing)
    - `Sample/` - Sample console application demonstrating XAML usage
    - `docs/` - Interactive documentation used for github pages containing and interactive playground using TerminalNinja.Wasm

## Feature development process
1. Identify the feature to be implemented (e.g., new control, XAML feature, rendering optimization)
2. Create a new branch for the feature (e.g., `feature/new-control-name`)
3. Implement the feature in the appropriate project (e.g., `TerminalNinja/Controls/` for new controls)
4. Add unit tests in `TerminalNinja.Tests/Unit/` corresponding to the new feature
5. Run all tests to ensure they pass
6. Add XAML samples in `Sample/` if applicable
7. Update documentation in `docs/` if applicable
8. Commit changes with a clear message (e.g., `feat: add new control for

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

- **DependencySystem** (`TerminalNinja.DependencySystem`): DependencyObject, DependencyProperty, PropertyMetadata, FrameworkPropertyMetadata
- **Controls** (`TerminalNinja.Controls`): UI controls — Visual, UIElement, FrameworkElement, Control, Panel, StackPanel, Grid, ContentControl, ContentPresenter, Window, UserControl, ItemsControl, ItemsPresenter, Selector, ListBox, ListBoxItem, ButtonBase, Button, TextBlock, Border, FontIcon, ProgressBar
    - `Controls.Primitives` — Popup, PopupRoot
- **Primitives** (`TerminalNinja.Primitives`): Basic types (Color, Size, Size2D, Rect, Thickness, GridLength, Alignment, SelectionMode, TextAlignment, TextWrapping, TextTrimming)
- **Buffers** (`TerminalNinja.Buffers`): Cell-based rendering buffers (CellBuffer, DirtyRect)
- **Styling** (`TerminalNinja.Styling`): Style, Setter, BorderStyle, BorderChars
- **Resources** (`TerminalNinja.Resources`): ResourceDictionary for shared resources
- **Commands** (`TerminalNinja.Commands`): ICommand, RelayCommand
- **Documents** (`TerminalNinja.Documents`): Inline text elements for rich text — Inline (abstract), Run, Span, InlineCollection, InlineRun
- **Markup** (`TerminalNinja.Markup`): XAML markup infrastructure — ContentPropertyAttribute, RuntimeNamePropertyAttribute, MarkupExtension, StaticResourceExtension, XmlnsDefinitionAttribute, XmlnsPrefixAttribute
- **Themes** (`TerminalNinja.Themes`): Built-in theme system — ThemeResourceKeys, Dark.xaml, Dracula.xaml, GruvboxDark.xaml (embedded resources)
- **XAML** (`TerminalNinja.Xaml`): XAML loading (TerminalXaml, XamlLoader)
    - `Xaml.Binding` — Binding, BindingBase, BindingExpression, BindingExpressionBase, BindingMode, BindingOperations, PropertyPath, PropertyPathObserver, PropertyPathSegment, RelativeSource, RelativeSourceMode
    - `Xaml.Data` — IValueConverter, DateTimeConverter
    - `Xaml.Mvvm` — ViewModelBase
    - `Xaml.Extensions` — ControlExtensions
    - `Xaml.TypeConverters` — ColorTypeConverter, SizeTypeConverter, ThicknessTypeConverter, BorderTypeConverter, GridLengthTypeConverter, TextDecorationsTypeConverter
- **Aot** (`TerminalNinja.Aot`): AOT-compatible registries — PropertyAccessorRegistry, ControlFactoryRegistry, TypeNameRegistry, TypeConverterRegistry, ContentPropertyRegistry, AttachedPropertySetterRegistry
- **App** (`TerminalNinja.App`): Application class with event loop
- **Rendering** (`TerminalNinja.Rendering`): ANSI terminal renderer
- **Ansi** (`TerminalNinja.Ansi`): AnsiCodes, AnsiStyle, AnsiWriter
- **Console** (`TerminalNinja.Console`): Terminal abstraction (ITerminal, SystemTerminal, Terminal, TerminalGuard)
- **Input** (`TerminalNinja.Input`): Keyboard and mouse input handling (InputReader, KeyEventArgs, MouseAction, MouseButton)
- **Platform** (`TerminalNinja.Platform`): Platform-specific code (Windows/, Unix/)

### Key Design Patterns

- **WPF-inspired**: Uses WPF terminology (Control, FrameworkElement, Content, etc.)
- **DependencyProperty system**: All control properties are DependencyProperty-backed, supporting change notification, metadata, and callbacks
- **XAML-first**: Supports declarative UI with XAML markup
- **Attached properties**: StackPanel.SizeMode, Grid.Row/Column for layout control (using `DependencyProperty.RegisterAttached`)
- **Data binding**: `{Binding PropertyName}` support with INotifyPropertyChanged, PropertyPath, RelativeSource, IValueConverter
- **Static resources**: `{StaticResource KeyName}` for reusable values
- **Styles**: Apply consistent theming with Style/Setter pattern
- **Source generators**: AOT-compatible code generation for property accessors, control factories, and XAML code-behind

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

**Core Library Global Usings (in GlobalUsings.cs):**
```csharp
global using TerminalNinja.DependencySystem;
```

**Test Project Global Usings (in GlobalUsings.cs):**
```csharp
global using TUnit.Core;
global using TerminalNinja.DependencySystem;
global using TUnit.Assertions;
global using TUnit.Assertions.Extensions;
global using TerminalNinja.Primitives;
global using TerminalNinja.Buffers;
global using TerminalNinja.Controls;
global using TerminalNinja.Controls.Primitives;
global using TerminalNinja.Documents;
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
- Test project uses NSubstitute v5.3.0 for mocking
- Primitive controls live in `TerminalNinja.Controls.Primitives` (e.g., Popup, ButtonBase)

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
    - `Selector` — for items controls with selection semantics (SelectedIndex, SelectedItem)
    - `ButtonBase` — for clickable controls
3. All properties must be DependencyProperty-backed (see DependencyProperty Conversion section)
4. Input handling: Override `OnKeyEvent`/`OnMouseEvent` (inherited from UIElement) for keyboard/mouse input
5. Add `[ContentProperty]` and `[RuntimeNameProperty]` attributes if applicable
6. Add `[TypeConverter]` attribute if custom XAML parsing is needed
7. Update tests in `TerminalNinja.Tests/Unit/Controls/`

## XAML Support

### Type Converters

All types that can be used in XAML should have a `[TypeConverter]` attribute:

```csharp
[TypeConverter(typeof(ColorTypeConverter))]
public readonly record struct Color { ... }
```

Existing converters (in `TerminalNinja.Xaml.TypeConverters`):
- `ColorTypeConverter` - Parses colors like "Red", "#FF0000", "rgb(255,0,0)"
- `SizeTypeConverter` - Parses sizes like "Auto", "Stretch", "10"
- `ThicknessTypeConverter` - Parses thickness like "5", "5,10", "5,10,5,10"
- `BorderTypeConverter` - Parses border styles like "Single", "Double", "Rounded" (for `BorderStyle` struct)
- `GridLengthTypeConverter` - Parses grid lengths like "Auto", "*", "2*", "100"

### XAML Namespace

All CLR namespaces are mapped to a single XAML namespace in `Properties/AssemblyInfo.cs`:

```csharp
using System.Windows.Markup;

[assembly: XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Controls")]
[assembly: XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Controls.Primitives")]
[assembly: XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Documents")]
[assembly: XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Primitives")]
[assembly: XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Styling")]
[assembly: XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Commands")]
[assembly: XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Resources")]
[assembly: XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.App")]
[assembly: XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Xaml.Binding")]
[assembly: XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Xaml.Markup")]
[assembly: XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "System.Windows.Markup")]
[assembly: XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Xaml.Mvvm")]
[assembly: XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "TerminalNinja.Xaml.Data")]
[assembly: XmlnsDefinition("http://schemas.terminalninja.dev/xaml", "System")]

[assembly: XmlnsPrefix("http://schemas.terminalninja.dev/xaml", "tn")]
```

These `System.Windows.Markup.XmlnsDefinition` attributes are recognized by both Rider/Visual Studio for XAML IntelliSense and by Portable.Xaml at runtime (Portable.Xaml maps `System.Windows.Markup` attributes internally).

### Loading XAML

```csharp
// Load from string
var window = TerminalXaml.Parse<Window>(xamlString);

// Load from file
var window = TerminalXaml.LoadFromFile<Window>("DemoLayout.xaml");

// Show window
window.Show();  // Sets Application.Current.RootControl = window
```

## Recent Changes (Feb-Apr 2026)

### Theming System (Apr 2026)

Added a built-in theme system with switchable XAML resource dictionaries:

- `ThemeResourceKeys` — well-known string constants for theme color resources (e.g., `ThemeBackgroundColor`, `ThemeForegroundColor`, `ThemeAccentColor`)
- Built-in themes as embedded resources: `Dark.xaml`, `Dracula.xaml`, `GruvboxDark.xaml`
- Themes are XAML ResourceDictionaries loaded via `Application.ThemeName`
- Controls reference theme colors via `{StaticResource ThemeBackgroundColor}` etc.

### FontIcon Control (Apr 2026)

- `FontIcon` (sealed) — extends FrameworkElement for displaying Nerd Font icons
- DependencyProperties: Glyph, FontSize

### ProgressBar Control (Apr 2026)

- `ProgressBar` (sealed) — extends FrameworkElement, implements IDisposable
- DependencyProperties: Value, Minimum, Maximum, IsIndeterminate

### Popup Control (Apr 2026)

- `Popup` — extends FrameworkElement (in Controls.Primitives namespace)
- `PopupRoot` (internal) — supporting class for Popup rendering

### Documents / Inline Text (Apr 2026)

Added rich inline text support within TextBlock:

- `Inline` (abstract) — base class for inline text elements, extends FrameworkElement
- `Run` (sealed) — text content inline
- `Span` (sealed) — container for nested inlines
- `InlineCollection` — collection type for managing inlines
- `InlineRun` — internal run representation

### Markup Infrastructure (Apr 2026)

Moved XAML markup attributes and extensions to dedicated `TerminalNinja.Markup` namespace:

- `ContentPropertyAttribute`, `RuntimeNamePropertyAttribute`
- `MarkupExtension`, `StaticResourceExtension`
- `XmlnsDefinitionAttribute`, `XmlnsPrefixAttribute`
- `ConstructorArgumentAttribute`, `MarkupExtensionReturnTypeAttribute`

### Binding Infrastructure Refactoring (Apr 2026)

Refactored binding system to more closely resemble WPF:

- `BindingExtension` / `BindingManager` / `ElementBinding` removed
- Replaced by: `Binding`, `BindingBase`, `BindingExpression`, `BindingExpressionBase`, `BindingOperations`
- Added: `PropertyPathObserver`, `PropertyPathSegment`, `RelativeSourceMode`

### Selector / ListBox / ListBoxItem (Mar 2026)

Added WPF-aligned selection controls:

- `Selector` (abstract) — extends ItemsControl with SelectedIndex, SelectedItem, SelectionMode, SelectionChanged event
- `ListBox` — extends Selector with keyboard navigation, item container generation (ListBoxItem), SelectedBackground/SelectedForeground
- `ListBoxItem` — extends ContentControl with IsSelected, SelectedBackground/SelectedForeground, mouse click selection
- `ContentPresenter` — renders ContentControl content, supports DataTemplate
- `ItemsPresenter` — renders ItemsControl's ItemsPanel, walks parent chain to find owner
- `SelectionMode` enum added to Primitives
- `SelectionChangedEventArgs` for selection change events

### DependencyProperty Conversion (Mar 2026)

All CLR/auto properties across the entire control hierarchy have been converted to DependencyProperty-backed properties, matching WPF's property system. Attached properties (StackPanel.SizeMode/FixedSize, Grid.Row/Column/RowSpan/ColumnSpan) now use `DependencyProperty.RegisterAttached`.

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
| ContentControl | Content, ContentTemplate (with PropertyChangedCallbacks) |
| ContentPresenter | Content, ContentTemplate (with PropertyChangedCallbacks) |
| ButtonBase | Command (with PropertyChangedCallback for CanExecuteChanged), CommandParameter |
| Button | Text, FocusColor, HoverColor, Width, Height |
| TextBlock | Text, Foreground, Background, Width, Height, HorizontalTextAlignment, VerticalTextAlignment, TextWrapping, TextTrimming, Padding |
| Border | Background, Foreground, BorderStyle, Width, Height, Child (with PropertyChangedCallback) |
| Window | Title, Width, Height |
| ItemsControl | ItemsSource (with PropertyChangedCallback), ItemTemplate, ItemsPanel |
| Selector | SelectedIndex, SelectedItem, SelectionMode (with PropertyChangedCallbacks) |
| ListBox | SelectedBackground, SelectedForeground |
| ListBoxItem | IsSelected, SelectedBackground, SelectedForeground |
| FontIcon | Glyph, FontSize |
| ProgressBar | Value, Minimum, Maximum, IsIndeterminate |
| Popup | IsOpen, Child, PlacementTarget |

**Metadata usage:**
- Visual properties use `FrameworkPropertyMetadata(default, affectsRender: true)` — triggers `InvalidateVisual()` on change
- Non-visual properties use `PropertyMetadata(default)` or `FrameworkPropertyMetadata(default, affectsRender: false)`
- Properties with side effects (Content, Child, Command, ItemsSource) use `PropertyChangedCallback`
- Nullable defaults use `(object?)null` to avoid CS8625

**No changes needed to infrastructure:**
- PropertyAccessorGenerator — DP CLR wrappers look identical to normal properties
- Style application — uses PropertyAccessorRegistry → CLR setter → `SetValue()` — works unchanged
- Bindings — `DependencyObject.SetValue()` raises INPC, so bindings work unchanged
- XAML loader — uses PropertyAccessorRegistry — works unchanged

### WPF-Aligned Class Hierarchy Refactoring (Feb-Mar 2026)

Major refactoring to align the entire class hierarchy with WPF's inheritance tree:

**Class Hierarchy:**
```
DependencyObject (DependencySystem/)
  └── Visual (abstract) — visual tree parent/child, GetChildrenWithBounds
        └── UIElement (abstract) — Visibility, IsEnabled, Focusable, IsFocused, IsMouseOver, input events (OnKeyEvent/OnMouseEvent), HitTest, InvalidateVisual
              └── FrameworkElement (abstract) — resources, styles, DataContext, Name, Width/Height, HorizontalAlignment/VerticalAlignment, Margin, GetLogicalChildren()
                    ├── Control (abstract) — Background, Foreground, Padding, BorderStyle, TabIndex, Template (stub); Focusable=true by default
                    │     ├── ContentControl [ContentProperty("Content")] — Content, ContentTemplate, HasContent
                    │     │     ├── Window — Title, Show/Close
                    │     │     ├── UserControl — Focusable=false
                    │     │     └── ListBoxItem — IsSelected, SelectedBackground/SelectedForeground
                    │     ├── ItemsControl [ContentProperty("Items")] — Items, ItemsSource, ItemTemplate, ItemsPanel
                    │     │     └── Selector (abstract) — SelectedIndex, SelectedItem, SelectionMode, SelectionChanged
                    │     │           └── ListBox — keyboard navigation, item container generation
                    │     └── ButtonBase (abstract, in Controls.Primitives) — Command, CommandParameter, Click
                    │           └── Button (sealed) — Text, FocusColor, HoverColor, focus/hover rendering
                    ├── Panel (abstract) [ContentProperty("Children")] — Children (IList<UIElement>), Background
                    │     ├── StackPanel — Orientation, CrossAxisAlignment, attached SizeMode/FixedSize
                    │     └── Grid (sealed) — RowDefinitions/ColumnDefinitions, attached Row/Column/RowSpan/ColumnSpan
                    ├── ContentPresenter — Content, ContentTemplate (renders ContentControl's content)
                    ├── ItemsPresenter — finds owning ItemsControl, delegates to ItemsPanel
                    ├── TextBlock (sealed) — Text, Foreground, Background, wrapping, trimming, alignment
                    ├── Border (sealed) — BorderStyle, Background, Foreground, Child (UIElement?)
                    ├── FontIcon (sealed) — Glyph, FontSize for Nerd Font icon display
                    ├── ProgressBar (sealed) — Value, Minimum, Maximum, IsIndeterminate; implements IDisposable
                    ├── Popup (in Controls.Primitives) — IsOpen, Child, PlacementTarget
                    └── Inline (abstract, in Documents/) — base for inline text elements
                          ├── Run (sealed) — Text content inline
                          └── Span (sealed) — container for nested inlines
```

### XAML Features

- **StaticResource**: `{StaticResource KeyName}` markup extension with resource lookup
- **Data Binding**: `{Binding PropertyName}` with INotifyPropertyChanged support
    - `BindingMode` — OneWay, TwoWay, OneTime
    - `PropertyPath` — dot-separated property paths for nested bindings
    - `RelativeSource` — bind to ancestor controls (`{RelativeSource FindAncestor, AncestorType=...}`)
    - `ElementBinding` — bind to named elements
    - `IValueConverter` — transform values between source and target
- **Attached Properties**: StackPanel.SizeMode, StackPanel.FixedSize, Grid.Row, Grid.Column, etc.
- **Styles**: Style/Setter pattern with TargetType validation
- **Window pattern**: Window.Show() / Window.Close() with Application.Current.RootControl
