# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

TerminalNinja is a WPF-inspired TUI (Terminal User Interface) framework for .NET 10 with zero-allocation rendering, cell-level diffing, and XAML support. It is **Windows-only** due to the `net10.0-windows` target (needed for `System.Windows.Markup` XAML IntelliSense in Rider/VS).

- **Language**: C# 13 with nullable reference types enabled
- **Test framework**: TUnit v1.12.93 (all tests are `async Task`)
- **XAML parser**: Portable.Xaml (runtime); `System.Windows.Markup` attributes for IDE IntelliSense

### Solution Structure

| Project | Purpose |
|---|---|
| `TerminalNinja/` | Core library — controls, rendering, XAML, input |
| `TerminalNinja.Tests/` | Unit tests (~497 tests, all passing) |
| `Sample/` | Demo app showing XAML usage |

---

## Build & Test Commands

```bash
# Build entire solution
dotnet build

# Run all tests
dotnet test

# Run tests for a specific class
dotnet test --treenode-filter /*/*/RectangleTests/*

# Run a single test by name
dotnet test --treenode-filter /*/*/*/Render_Border_DrawsCorrectly

# Run the sample app
dotnet run --project Sample/Sample.csproj
```

> **Note:** `global.json` sets the test runner to `Microsoft.Testing.Platform`. Use `--treenode-filter` for TUnit-style filtering instead of `--filter`.

---

## Architecture

### Control Hierarchy

```
IControl
  └── ControlBase (INPC + invalidation)
        └── FrameworkElement (resources, styles, data binding)
              ├── Panel (abstract – Children collection, Background)
              │     ├── StackPanel (Orientation, sequential layout)
              │     └── Grid (rows/columns layout)
              ├── ItemsControl (ItemsSource, ItemTemplate, ItemsPanel)
              └── Other controls (Rectangle, Label, Button, Window)
```

### Key Namespaces

- `TerminalNinja.Controls` — All UI controls (StackPanel, Grid, ItemsControl, Window, Rectangle, Button, Label)
- `TerminalNinja.Primitives` — Color, Size, Rect, Thickness, Border, Alignment
- `TerminalNinja.Buffers` — Double-buffered CellBuffer with dirty tracking
- `TerminalNinja.Rendering` — ANSI terminal renderer (zero-allocation)
- `TerminalNinja.Xaml` — XAML loading (`TerminalXaml.Parse<T>`, `TerminalXaml.LoadFromFile<T>`)
- `TerminalNinja.Resources` — ResourceDictionary for shared resources
- `TerminalNinja.Styling` — Style/Setter pattern
- `TerminalNinja.Input` — Keyboard and mouse input

### XAML System

All CLR namespaces map to `http://schemas.terminalninja.dev/xaml`. Both `Portable.Xaml.Markup.XmlnsDefinition` and `System.Windows.Markup.XmlnsDefinition` attributes are registered in `Properties/AssemblyInfo.cs` — the former for runtime, the latter for IDE IntelliSense. Do not remove either set.

XAML features supported: `{StaticResource Key}`, `{Binding PropertyName}`, attached properties (e.g., `StackPanel.SizeMode`, `Grid.Row`), and `Style`/`Setter`.

Types used in XAML must have a `[TypeConverter]` attribute. Existing converters: `ColorTypeConverter`, `SizeTypeConverter`, `ThicknessTypeConverter`, `BorderTypeConverter`, `GridLengthTypeConverter`.

### Zero-Allocation Rendering

The renderer uses a double-buffered `CellBuffer`, a `ref struct CellDiffEnumerator` for zero-allocation diffing, and `AnsiWriter` for direct stream output. Per-frame heap allocations are zero.

---

## Code Conventions

- **File-scoped namespaces** — `namespace TerminalNinja.ComponentName;`
- **One public type per file**; file name matches the type name
- **Private fields**: `_camelCase`; everything else: PascalCase
- Use **"control"** not "element"; use **"Content"** for child properties (WPF alignment)
- `FrameworkElement` keeps its exact name
- `[NotInParallel]` is required on `GridTests` because `AttachablePropertyServices` uses a non-thread-safe static dictionary — keep this in mind when adding Grid-related tests

### Adding New Controls

1. Place in `TerminalNinja/Controls/`
2. Inherit from `FrameworkElement` (resource/style support) or `ControlBase` (minimal)
3. Implement `IFocusable` if the control handles input
4. Add `[ContentProperty]`, `[RuntimeNameProperty]`, and `[TypeConverter]` attributes as needed
5. Register XAML namespace mapping in `Properties/AssemblyInfo.cs`
6. Add tests in `TerminalNinja.Tests/Unit/Controls/`

### Test Pattern

```csharp
// Naming: MethodName_Scenario_ExpectedBehavior
[Test]
public async Task Render_Border_DrawsTopLeft()
{
    // Arrange / Act / Assert
    await Assert.That(buffer.GetCell(0, 0).Char).IsEqualTo('┌');
}
```

---

## Reference Sources (third-party, read-only)

- `e:\thirdparty\Portable.Xaml\` — XAML parser used at runtime
- `e:\thirdparty\spectre\` — Spectre.Console (reference)
- `e:\thirdparty\wpf\src\Microsoft.DotNet.Wpf\` — WPF source (patterns to align with)
