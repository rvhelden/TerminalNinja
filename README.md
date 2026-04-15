# TerminalNinja

[![NuGet](https://img.shields.io/nuget/v/TerminalNinja.svg)](https://www.nuget.org/packages/TerminalNinja)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

A WPF-inspired terminal UI framework for .NET 10, built native AOT-first.

**[Documentation](https://rvhelden.github.io/TerminalNinja/)** | **[XAML Playground](https://rvhelden.github.io/TerminalNinja/playground.html)**

## Features

- **XAML-first UI** — declarative layouts with the TerminalNinja XML namespace
- **Dependency property system** — WPF-aligned DPs with metadata, callbacks, and binding support
- **Data binding** — `{Binding}`, `RelativeSource`, `IValueConverter`, OneWay/TwoWay/OneTime modes
- **MVVM pattern** — `ViewModelBase`, `RelayCommand`, `INotifyPropertyChanged`
- **Theming** — 3 built-in themes (Dark, Dracula, Gruvbox Dark) with implicit styles
- **30+ controls** — Grid, ListBox, ComboBox, TabControl, TreeView, DataGrid, DatePicker, and more
- **Modal dialogs** — `ShowDialogAsync()` with overlay stack and dimmed background
- **Keyboard and mouse input** — focus management, tab navigation, hit testing
- **Native AOT** — ~20ms startup, 18MB memory footprint. Source generators for property accessors, control factories, and XAML code-behind
- **Zero-allocation rendering** — cell-level diffing with packed 8-byte cell structures
- **Cross-platform** — Windows (VT100) and Unix terminal support
- **WASM playground** — [try XAML in the browser](https://rvhelden.github.io/TerminalNinja/playground.html)

## Built for Speed

TerminalNinja is native AOT from the ground up — not bolted on as an afterthought. The result: your app starts in **~20ms** and uses **under 18MB of memory** at startup. That's it. No JIT warmup, no GC pressure from framework initialization, no 100MB+ memory overhead just to render a console UI.

Every layer is designed with this in mind: zero-allocation cell-level diffing, packed 8-byte cell structures, and source generators that replace all reflection at compile time. You get a full WPF-style control framework at a fraction of the cost.

## Installation

```bash
dotnet add package TerminalNinja
```

The package includes the core library, source generators, and MSBuild targets — everything you need in a single reference.

## Quick Start

```csharp
using TerminalNinja.App;
using TerminalNinja.Controls;
using TerminalNinja.Xaml;

using var app = new Application(new ApplicationOptions
{
    TargetFps = 60,
    EnableMouseTracking = true,
    EnableTabNavigation = true
});

app.ThemeName = "GruvboxDark";

var window = TerminalXaml.Load<Window>("""
    <Window xmlns='http://schemas.terminalninja.dev/xaml' Title='Hello'>
        <TextBlock Text='Hello, TerminalNinja!' />
    </Window>
    """);
window.Show();
app.Run();
```

## Project Structure

```
TerminalNinja/              Core framework library
TerminalNinja.Generators/   Source generators (PropertyAccessor, ControlFactory, XamlClass)
TerminalNinja.Wasm/         Browser WASM module for the playground
TerminalNinja.Cli/          CLI snapshot tool
TerminalNinja.Tests/        Test suite
Sample/                     Sample app with navigable demo screens
docs/                       GitHub Pages site + XAML playground
```

## Samples

The `Sample/` project includes 24 demo screens accessible from a main menu:

| Sample | Description |
|--------|-------------|
| **Buttons** | ICommand binding with RelayCommand, hover/focus styling |
| **CheckBox** | Tri-state check boxes with data binding |
| **ColorPicker** | Interactive color selection |
| **ComboBox** | Drop-down selection with keyboard navigation |
| **DataBinding** | One-way, two-way, converters, and animated OKLCH color binding |
| **DataGrid** | Tabular data with columns, sorting, and selection |
| **DatePicker** | Calendar-based date selection |
| **DateTimePicker** | Combined date and time selection |
| **Dialogs** | Modal dialogs with ShowDialogAsync, theme color resolution |
| **FilePicker** | File browser dialog |
| **FolderPicker** | Folder browser dialog |
| **Grid Layout** | Rows, columns, star/fixed sizing, row/column spans |
| **Image** | Image rendering in the terminal |
| **ListView** | Multi-column list with custom columns |
| **Lists** | ListBox with ObservableCollection, add/remove, custom UserControl |
| **NumberPicker** | Numeric input with increment/decrement |
| **Progress Bars** | Determinate, indeterminate, and custom-character progress indicators |
| **RadioButton** | Grouped radio button selection |
| **ScrollViewer** | Scrollable content regions |
| **StackPanel Layout** | Vertical/horizontal stacking, Auto/Fixed/Stretch modes |
| **TabControl** | Tabbed content with keyboard navigation |
| **TextInput** | Text editing with selection and clipboard support |
| **TimePicker** | Time selection control |
| **TreeView** | Hierarchical data with expand/collapse |

```bash
dotnet run --project Sample/Sample.csproj
```

## Theming

Three built-in themes with implicit styles for all controls:

```csharp
app.ThemeName = "Dark";        // VS Code-inspired
app.ThemeName = "Dracula";     // Dracula color scheme
app.ThemeName = "GruvboxDark"; // Gruvbox dark palette
```

Custom themes can be loaded from XAML files:

```csharp
Application.LoadThemeFromFile("MyTheme.xaml");
```

## Building

```bash
dotnet build                                          # Build all projects
dotnet run --project Sample/Sample.csproj             # Run the sample app
dotnet run --project TerminalNinja.Tests              # Run tests
dotnet pack TerminalNinja/TerminalNinja.csproj -c Release  # Create NuGet package
```

## Publishing to NuGet

Packages are published automatically via GitHub Actions when a version tag is pushed:

```bash
git tag v0.1.0
git push origin v0.1.0
```

This triggers the workflow to build, test, pack, and publish to NuGet.org. Requires a `NUGET_API_KEY` secret configured in the repository.

You can also trigger a publish manually from the Actions tab using workflow dispatch.

## Learning XAML

TerminalNinja follows WPF conventions closely. If you're new to XAML, data binding, or the control model, the official Microsoft WPF docs are a great starting point:

- [WPF Overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/) — XAML fundamentals, dependency properties, data binding, styling
- [WPF Controls](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/) — control types, templates, and layout panels

Most WPF concepts (dependency properties, `{Binding}`, styles, `Grid`/`StackPanel` layout) translate directly to TerminalNinja.

## Requirements

- .NET 10.0 SDK
- C# 13 language features
- Windows 10+ (VT100 support) or Unix terminal

## License

[MIT](LICENSE)
