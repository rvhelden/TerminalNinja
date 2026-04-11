# TerminalNinja

A WPF-inspired terminal UI framework for .NET 10, built native AOT-first.

## Features

- **XAML-first UI** — declarative layouts with the TerminalNinja XML namespace
- **Dependency property system** — WPF-aligned DPs with metadata, callbacks, and binding support
- **Data binding** — `{Binding}`, `RelativeSource`, `IValueConverter`, OneWay/TwoWay/OneTime modes
- **MVVM pattern** — `ViewModelBase`, `RelayCommand`, `INotifyPropertyChanged`
- **Theming** — 3 built-in themes (Dark, Dracula, Gruvbox Dark) with implicit styles
- **Rich controls** — Grid, StackPanel, ListBox, Button, ProgressBar, Border, TextBlock, Window
- **Modal dialogs** — `ShowDialogAsync()` with overlay stack and dimmed background
- **Keyboard and mouse input** — focus management, tab navigation, hit testing
- **Native AOT** — source generators for property accessors, control factories, and XAML code-behind
- **Zero-allocation rendering** — cell-level diffing with packed 8-byte cell structures
- **Cross-platform** — Windows (VT100) and Unix terminal support
- **WASM playground** — try XAML in the browser at the docs site

## Project Structure

```
TerminalNinja/              Core framework library
TerminalNinja.Generators/   Source generators (PropertyAccessor, ControlFactory, XamlClass)
TerminalNinja.Wasm/         Browser WASM module for the playground
TerminalNinja.Cli/          CLI snapshot tool
TerminalNinja.Tests/        Test suite (1033 tests)
Sample/                     Sample app with 7 navigable demo screens
docs/                       GitHub Pages site + XAML playground
```

## Quick Start

```bash
dotnet new console -n MyApp
cd MyApp
dotnet add reference ../TerminalNinja/TerminalNinja.csproj
```

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

var window = TerminalXaml.Load<Window>("<Window xmlns='http://schemas.terminalninja.dev/xaml' Title='Hello'><TextBlock Text='Hello, TerminalNinja!' /></Window>");
window.Show();
app.Run();
```

## Samples

The `Sample/` project includes 7 demo screens accessible from a main menu:

| Sample | Description |
|--------|-------------|
| **Progress Bars** | Determinate, indeterminate, and custom-character progress indicators |
| **Buttons** | ICommand binding with RelayCommand, hover/focus styling |
| **Data Binding** | One-way, two-way, converters, and animated OKLCH color binding |
| **Dialogs** | Modal dialogs with ShowDialogAsync, theme color resolution |
| **Lists** | ListBox with ObservableCollection, add/remove, custom UserControl |
| **Grid Layout** | Rows, columns, star/fixed sizing, row/column spans |
| **StackPanel Layout** | Vertical/horizontal stacking, Auto/Fixed/Stretch modes |

```bash
dotnet run --project Sample/Sample.csproj
```

## Theming

Three built-in themes with 24 color resource keys and implicit styles:

```csharp
app.ThemeName = "Dark";        // VS Code-inspired
app.ThemeName = "Dracula";     // Dracula color scheme
app.ThemeName = "GruvboxDark"; // Gruvbox dark palette
```

## Building

```bash
dotnet build                                          # Build all projects
dotnet run --project Sample/Sample.csproj             # Run the sample app
dotnet run --project TerminalNinja.Tests              # Run all 1033 tests
dotnet build TerminalNinja.Wasm/TerminalNinja.Wasm.csproj  # Build WASM module
```

## Requirements

- .NET 10.0 SDK
- C# 13 language features
- Windows 10+ (VT100 support) or Unix terminal

## License

See LICENSE file for details.
