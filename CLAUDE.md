# CLAUDE.md - Developer Guide for TerminalNinja

This document provides essential information for AI coding agents working in the TerminalNinja codebase.

## Project Overview

- **Language**: C# 13 (latest)
- **Framework**: .NET 10.0 (Native AOT)
- **Platform**: Cross-platform (Windows, Linux, macOS)
- **Test Framework**: TUnit v1.20.0
- **IDE**: JetBrains Rider (optional)
- **Solution Structure**:
    - `TerminalNinja/` - Core library (terminal UI framework with XAML support)
    - `TerminalNinja.Wasm/` - Library for bringing TerminalNinja to WebAssembly
    - `TerminalNinja.Cli/` - Program for generating a single frame for the given xaml
    - `TerminalNinja.Generators/` - Source generators (ControlFactory, PropertyAccessor, XamlClass)
    - `TerminalNinja.Tests/` - Tests for the core `TerminalNinja` library
    - `TerminalNinja.Terminal.Tests/` - Tests for `TerminalNinja.Terminal`
    - `Sample/` - Sample console application demonstrating XAML usage (one screen per control)
    - `docs/` - Interactive documentation (GitHub Pages with playground using TerminalNinja.Wasm)

## AOT Constraints

Native AOT compilation is a core requirement. All code must be compatible:
- No `Activator.CreateInstance`, `System.Reflection.Emit`, expression trees, `Type.GetType(string)`
- Use source generators for property accessors, control factories, and XAML code-behind
- All types auto-discovered by generators — no manual registration needed
- The control-factory generator registers every non-abstract bindable type (including
  `ViewModelBase` subclasses) with a `static () => new T()` factory, which requires a
  parameterless constructor. Apply `[SkipFactoryRegistration]` (TerminalNinja.Aot) to view
  models with constructor dependencies that are never instantiated from XAML — property
  accessors for binding are still generated.

## Feature Development Process

When implementing new controls or features:

1. Implement in `TerminalNinja/Controls/` (or appropriate project)
2. All properties must be DependencyProperty-backed
3. Add theming support (see Theming Checklist below)
4. Add unit tests in the test project that matches the implementation project (e.g. core controls → `TerminalNinja.Tests/Unit/`, Skia code → `TerminalNinja.Skia.Tests/Unit/`)
5. Run all tests (see Build & Test Commands below)
6. Add sample screen in `Sample/Samples/{ControlName}/` + register in MainMenuViewModel and ShellViewModel
7. Add documentation page in `docs/samples/{controlname}.html` + entry in `docs/samples.js` + card in `docs/index.html`
8. Commit with conventional message: `feat:`, `fix:`, `docs:`, `refactor:`, `test:`

## Build & Test Commands

Tests are split across one project per production project. Pick the one that matches the code you're testing:

| Test project | Covers |
|---|---|
| `TerminalNinja.Tests` | Core `TerminalNinja` library (Controls, Buffers, XAML, etc.) |
| `TerminalNinja.Terminal.Tests` | `TerminalNinja.Terminal` |

```bash
# Build entire solution
dotnet build

# Run tests for a single test project (NOTE: use dotnet run, not dotnet test — .NET 10 + TUnit requires this)
dotnet run --project TerminalNinja.Tests/TerminalNinja.Tests.csproj
dotnet run --project TerminalNinja.Terminal.Tests/TerminalNinja.Terminal.Tests.csproj

# Run tests for a specific class
dotnet run --project TerminalNinja.Tests/TerminalNinja.Tests.csproj --treenode-filter "/*/*/CheckBoxTests/*"

# Run a specific test method
dotnet run --project TerminalNinja.Tests/TerminalNinja.Tests.csproj --treenode-filter "/*/*/*/MyTestName"

# Run sample app
dotnet run --project Sample/Sample.csproj
```

### TUnit Test Filtering

- Wildcard: `*` (e.g., `LoginTests*`)
- Equality: `=` (e.g., `[Category=Unit]`)
- AND: `&`, OR: `|` (with parentheses)
- Example: `/*/*/(ClassA)|(ClassB)/*`

## Architecture Overview

TerminalNinja is a WPF-like terminal UI framework with XAML support:

- **DependencySystem**: DependencyObject, DependencyProperty, PropertyMetadata, FrameworkPropertyMetadata, Expression
- **Controls**: Visual, UIElement, FrameworkElement, Control, Panel, StackPanel, Grid, DockPanel, UniformGrid, WrapPanel, ContentControl, ContentPresenter, Window, UserControl, ItemsControl, ItemsPresenter, Selector, ListBox, ListBoxItem, ComboBox, ComboBoxItem, ButtonBase, Button, CheckBox, RadioButton, TextBlock, TextBox, Border, ScrollViewer, TabControl, TabItem, TreeView, TreeViewItem, ListView, ListViewItem, ListViewColumn, FontIcon, ProgressBar, NumberPicker, DatePicker, TimePicker, DateTimePicker
    - `Controls.Primitives`: Popup, PopupRoot
    - `Controls.Charts`: ChartBase, BarChart, LineChart, TraceChart, FlameGraph + data models (ChartSeries, ChartDataPoint, TraceSpan, FlameNode), AxisScale, BarMode. Data models are plain `[BindableObject]` POCOs also listed in the generators' `AdditionalFactoryTypes` so they are XAML-instantiable.
- **Primitives**: Color, Size, Size2D, Rect, Thickness, GridLength, Alignment, SelectionMode, ScrollBarVisibility, TextAlignment, TextWrapping, TextTrimming
- **Buffers**: CellBuffer (with CopyRegionTo for ScrollViewer), DirtyRect, BrailleCanvas (2×4 sub-cell drawing for line charts)
- **Styling**: Style, Setter, BorderStyle, BorderChars
- **Resources**: ResourceDictionary (with MergedDictionaries)
- **Themes**: ThemeResourceKeys, Dark.xaml, Dracula.xaml, GruvboxDark.xaml + custom theme loading via `Application.LoadThemeFromFile/LoadThemeFromXaml`
- **XAML**: TerminalXaml, XamlLoader, Binding system (Binding, BindingExpression, PropertyPath, RelativeSource), IValueConverter, ViewModelBase, TypeConverters
- **Aot**: PropertyAccessorRegistry, ControlFactoryRegistry, TypeNameRegistry, TypeConverterRegistry, ContentPropertyRegistry
- **App**: Application (event loop, theme loading, overlay stack, focus management), Dispatcher (cross-thread work marshalled to the top of each frame via `Application.Dispatcher.Post`), DispatcherSynchronizationContext (installed by `Application.Run` so await continuations resume on the UI thread; ProcessTick-driven hosts call `Application.InstallSynchronizationContext()`)
- **Commands**: ICommand (custom, not System.Windows.Input), RelayCommand, AsyncRelayCommand (disables while running, completes via the current SynchronizationContext)
- **Input**: InputReader, KeyEvent, MouseEvent, FocusManager
- **Rendering/Ansi/Console/Platform**: Terminal rendering infrastructure, FrameCapture (renders a control tree into a CellBuffer and returns it as plain text or ANSI — the way to verify a layout headlessly from a script or test)

### Key Design Patterns

- **WPF-inspired**: Uses WPF terminology and patterns throughout
- **DependencyProperty system**: All control properties are DP-backed with change notification, metadata, callbacks, coercion
- **XAML-first**: Declarative UI with `{Binding}`, `{StaticResource}`, attached properties, styles
- **Source generators**: AOT-compatible — auto-discover controls, generate property accessors, control factories
- **ISelectableContainer**: Shared interface for ListBoxItem, ComboBoxItem, ListViewItem, TabItem

## Code Style Guidelines

- **Nullable reference types** enabled (`<Nullable>enable</Nullable>`)
- **File-scoped namespaces** (single line, no braces)
- **Implicit usings** — don't add explicit usings for System.*, System.Linq, etc.
- One public type per file, file name matches type name
- Private fields: `_camelCase`, parameters: `camelCase`, properties/methods: `PascalCase`
- Use `ArgumentNullException.ThrowIfNull()` for parameter validation

### DependencyProperty Patterns

**Regular DP:**
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

**Attached DP:**
```csharp
public static readonly DependencyProperty XxxProperty =
    DependencyProperty.RegisterAttached("Xxx", typeof(T), typeof(OwnerClass),
        new PropertyMetadata(defaultValue));

public static T GetXxx(DependencyObject d) => (T)d.GetValue(XxxProperty)!;
public static void SetXxx(DependencyObject d, T value) => d.SetValue(XxxProperty, value);
```

**Metadata usage:**
- Visual properties: `FrameworkPropertyMetadata(default, affectsRender: true)`
- Non-visual: `PropertyMetadata(default)`
- Side effects: add `propertyChangedCallback`
- Value clamping: add `coerceValueCallback`
- Nullable defaults: `(object?)null`

### Critical: SetValue vs SetValueInternal

When a control modifies its own DP internally (e.g., TextBox editing its Text property):
- **`SetValue(dp, value)`** — clears any active binding expression (breaks two-way bindings)
- **`SetValueInternal(dp, value)`** — preserves the binding expression (keeps two-way bindings working)

Always use `SetValueInternal` for internal DP writes in controls that support data binding.

## Testing Guidelines

- All tests are **async**: `async Task` with `await Assert.That(...)`
- Test naming: `MethodName_Scenario_ExpectedBehavior`
- Test project uses NSubstitute v5.3.0 for mocking
- Use `new CellBuffer(width, height)` + `control.Render(buffer, new Rect(...))` for rendering tests
- Use `TerminalXaml.Load<T>(xaml)` for XAML parsing tests
- Use `new Application(new ApplicationOptions { Headless = true })` for Application-dependent tests

### TUnit Assertions

```csharp
await Assert.That(value).IsTrue();
await Assert.That(actual).IsEqualTo(expected);
await Assert.That(value).IsNull();
await Assert.That(action).ThrowsExactly<ExceptionType>();
```

## Adding New Controls

1. Place in `TerminalNinja/Controls/`
2. Choose base class:
    - `Control` — interactive controls (Focusable=true, Background/Foreground/Padding)
    - `FrameworkElement` — non-interactive visual elements (TextBlock, Border)
    - `ContentControl` — single Content child
    - `Panel` — layout container with Children collection
    - `ItemsControl` — data-bound collections
    - `Selector` — items with selection (SelectedIndex/SelectedItem)
    - `ButtonBase` — clickable controls (Command, Click)
3. Add `[ContentProperty]` and `[RuntimeNameProperty("Name")]` attributes
4. All properties as DependencyProperties
5. Override `GetPreferredSize`, `CalculateBounds`, `Render`, `OnKeyEvent`/`OnMouseEvent`

### Focus visuals

A terminal has no adorner layer: nothing can be drawn outside a control's own bounds, and every
cell inside them is already carrying content. **A control must not show focus by recolouring its
own cells.** `DataGrid`, `ListBox`, `ListView` and `TreeView` used to draw a "focus border" along
their bounds' edges, which in practice tinted the header row and the first and last character of
every row — it reads as a mnemonic, not as a frame.

Focus visuals belong on the element whose cells are chrome rather than content:

- Wrap the control in a `Border`. `Border.ContainsFocus` (the equivalent of WPF's
  `IsKeyboardFocusWithin`) switches the frame to `FocusBorderBrush`, themed from
  `ThemeAccentColor`, and `Border.ShowFocusBorder="False"` opts a decorative frame out.
- A control that draws its *own* frame — `Button`, `TextBox`, `ComboBox`, the pickers — keeps
  using its `FocusColor` on that frame. Those cells are chrome the control owns.

### Theming Checklist

When adding a new control that needs theme support:

1. Add color key constants to `TerminalNinja/Themes/ThemeResourceKeys.cs`
2. Add `<Color x:Key="...">` resources to ALL 3 theme files:
    - `TerminalNinja/Themes/Dark.xaml`
    - `TerminalNinja/Themes/Dracula.xaml`
    - `TerminalNinja/Themes/GruvboxDark.xaml`
3. Add `<Style TargetType="YourControl">` implicit style to ALL 3 theme files
4. Update theme test count in `TerminalNinja.Tests/Xaml/ThemeTests.cs`

### Sample & Docs Checklist

When adding a new control:

1. Create `Sample/Samples/{ControlName}/{ControlName}Screen.xaml` (+ ViewModel if needed)
2. Add entry to `Sample/Samples/MainMenu/MainMenuViewModel.cs` Samples list
3. Add navigation case to `Sample/ShellViewModel.cs` NavigateToSample switch
4. Create `docs/samples/{controlname}.html` with: Overview, Properties table, Examples, Keyboard Shortcuts, Key Concepts
5. Add entry to `docs/samples.js` SAMPLES array (no Width/Height on Window elements)
6. Add `<a class="sample-card">` to `docs/index.html` sample grid

## XAML Support

### XAML Namespace

All CLR namespaces mapped to `http://schemas.terminalninja.dev/xaml` via `XmlnsDefinition` in `Properties/AssemblyInfo.cs`. New controls in `TerminalNinja.Controls` are available in XAML automatically.

### Loading XAML

```csharp
// From a generated layout manifest (the usual path — one IXamlLayout field per .xaml file,
// generated into the project's RootNamespace as {RootNamespace}.XamlLayouts):
var window = TerminalXaml.Load<Window>(XamlLayouts.ShellLayout, viewModel);

// From a raw XAML string or stream:
var window = TerminalXaml.Load<Window>(xamlString);
var window = TerminalXaml.LoadFromStream<Window>(stream, dataContext);
window.Show();
```

There is no `TerminalXaml.Parse<T>` or `TerminalXaml.LoadFromFile<T>`. Only `.xaml` files are
globbed by `TerminalNinja.targets` (not `.axaml`); embedded resource logical names follow
`{RootNamespace}.{Path.To.File}.xaml`.

### Type Converters

Existing converters (in `TerminalNinja.Xaml.TypeConverters`):
- `ColorTypeConverter` — "Red", "#FF0000", "rgb(255,0,0)"
- `SizeTypeConverter` — "Auto", "Stretch", "10"
- `ThicknessTypeConverter` — "5", "5,10", "5,10,5,10"
- `BorderTypeConverter` — "Single", "Double", "Rounded"
- `GridLengthTypeConverter` — "Auto", "*", "2*", "100"

## Git Workflow

This repo uses [Versionize](https://github.com/versionize/versionize) to compute semver bumps and generate the changelog, so every commit and PR title MUST follow Conventional Commits.

- **Format**: `<type>(<optional-scope>): <imperative lowercase summary>`
- **Branch naming**: `feature/<slug>`, `fix/<slug>`
- **Types and version impact**:
    - `feat` → minor bump
    - `fix` → patch bump
    - `perf` → patch bump
    - `refactor`, `docs`, `test`, `build`, `ci`, `chore`, `style` → no bump (still in changelog depending on Versionize config)
- **Breaking changes**: append `!` after the type/scope AND add a `BREAKING CHANGE:` footer. Example: `feat(databinding)!: ...` with `BREAKING CHANGE: BindingMode.Default removed.`
- **Scopes** (optional but preferred): `button`, `checkbox`, `databinding`, `theming`, `xaml`, `aot`, `generators`, `layout`, `focus`, `sample`, `packaging`, etc. — usually the control or subsystem name.
- **PR titles**: same shape as commits — the merged/squashed commit is what Versionize sees.
- **`chore(release):` commits** are generated by `versionize run` during releases — do not write these by hand.

## Project Files to Never Modify

- `bin/`, `obj/` — build output (gitignored)
- `*.user`, `/.vs/`, `/.idea/` — IDE settings
