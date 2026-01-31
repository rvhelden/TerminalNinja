# TerminalNinja

A high-performance TUI (Terminal User Interface) framework for .NET 10 with zero-allocation rendering and cell-level diffing.

## Features

- **Zero-allocation rendering** - No GC pressure during render loops
- **Cell-level diffing** - Only changed cells are transmitted to terminal
- **24-bit true color** - Full RGB color support
- **C# 13 escape sequences** - Uses modern `\e` escape character
- **Memory-optimized** - Packed 8-byte cell structure with dirty tracking
- **ANSI escape sequences** - Direct ANSI output with optimized cursor movement
- **Cross-platform** - Windows (VT100) and Unix terminal support
- **Flexible sizing** - Absolute, relative (percentage), and stretch sizing modes
- **Multiple border styles** - Single, double, rounded, and ASCII borders
- **Alignment support** - Start, center, and end alignment on both axes

## Architecture

```
TerminalNinja.Core/
├── Primitives/           # Basic types (Color, Cell, Rect, Size, Alignment)
├── Styling/              # Border styles and characters
├── Buffers/              # Double-buffered cell buffer with dirty tracking
├── Ansi/                 # ANSI escape sequence generation
├── Console/              # Terminal setup and state management
├── Elements/             # UI elements (Rectangle)
└── Rendering/            # Main renderer orchestrator
```

## Quick Start

### Installation

```bash
dotnet add reference ../TerminalNinja.Core/TerminalNinja.Core.csproj
```

### Basic Usage

```csharp
using TerminalNinja.Core.Elements;
using TerminalNinja.Core.Primitives;
using TerminalNinja.Core.Rendering;
using TerminalNinja.Core.Styling;

using var renderer = new Renderer();

var box = new Rectangle
{
    X = Size.Percent(10),
    Y = Size.Absolute(5),
    Width = Size.Percent(80),
    Height = Size.Absolute(10),
    HorizontalAlignment = Alignment.Start,
    VerticalAlignment = Alignment.Start,
    Border = Border.Single(Color.Cyan),
    BackgroundColor = new Color(20, 20, 40),
    ForegroundColor = Color.Cyan
};

renderer.Clear();
renderer.Draw(box);
renderer.Present();  // Zero-allocation render!
```

## Performance Characteristics

### Memory Usage (200×50 terminal)

| Component | Size | Notes |
|-----------|------|-------|
| Cell Buffer (current) | 80 KB | 10K cells × 8 bytes |
| Cell Buffer (previous) | 80 KB | For diffing |
| ANSI Writer buffer | 64 KB | Pre-allocated output |
| **Per-frame allocations** | **0 bytes** | ✅ Zero allocations! |

### Key Optimizations

1. **Struct enumerators** - `ref struct` for zero-allocation iteration
2. **Dirty rectangle tracking** - Only diff changed regions
3. **ANSI style tracking** - Skip redundant color escape sequences
4. **Direct stream writing** - Bypass `Console.Write` overhead
5. **Packed cell format** - 8 bytes per cell with `[StructLayout(Pack = 1)]`
6. **Cursor movement optimization** - Skip movement for sequential cells
7. **Fast integer formatting** - Optimized for RGB values and coordinates

## Components

### Primitives

- **`Color`** - 24-bit RGB color (3 bytes)
- **`Cell`** - Terminal cell with char + colors (8 bytes)
- **`Rect`** - Rectangle bounds (16 bytes)
- **`Size`** - Sizing with mode (absolute/relative/stretch)
- **`Alignment`** - Start/Center/End positioning

### Buffers

- **`CellBuffer`** - Double-buffered with dirty tracking
- **`DirtyRect`** - Tracks modified screen region
- **`CellDiffEnumerator`** - Zero-allocation struct enumerator

### ANSI Output

- **`AnsiWriter`** - Direct stream writing with zero allocations
- **`AnsiCodes`** - Pre-computed escape sequences using C# 13 `\e`
- **`AnsiStyle`** - Tracks current style to minimize sequences

### Elements

- **`Rectangle`** - Box with borders, colors, and flexible sizing
  - Absolute positioning: `X = Size.Absolute(10)`
  - Relative positioning: `X = Size.Percent(50)`
  - Stretch to fill: `Width = Size.Stretch`
  - Alignment: `HorizontalAlignment = Alignment.Center`

## Building

```bash
# Build all projects
dotnet build

# Build in release mode
dotnet build -c Release

# Run tests
dotnet build TerminalNinja.Core.Tests/TerminalNinja.Core.Tests.csproj
dotnet exec TerminalNinja.Core.Tests/bin/Debug/net10.0/TerminalNinja.Core.Tests.dll

# Run sample
dotnet run --project Sample/Sample.csproj
```

## Testing

The project uses TUnit v1.12.93 for testing:

```bash
dotnet build TerminalNinja.Core.Tests/TerminalNinja.Core.Tests.csproj
dotnet exec TerminalNinja.Core.Tests/bin/Debug/net10.0/TerminalNinja.Core.Tests.dll
```

## Implementation Details

### Cell Structure (8 bytes)

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct Cell
{
    public readonly char Character;    // 2 bytes
    public readonly Color Foreground;  // 3 bytes  
    public readonly Color Background;  // 3 bytes
}
```

### Zero-Allocation Diffing

```csharp
// Struct enumerator - allocated on stack
public ref struct CellDiffEnumerator
{
    public bool MoveNext() { /* ... */ }
    public CellChange Current => _currentChange;
}

// Usage - no heap allocations!
foreach (var change in buffer.GetChanges())
{
    writer.WriteCell(change.X, change.Y, change.Cell);
}
```

### ANSI Escape Sequences (C# 13)

```csharp
// Using modern \e escape character
public static ReadOnlySpan<byte> Reset => "\e[0m"u8;
public static ReadOnlySpan<byte> HideCursor => "\e[?25l"u8;
public static ReadOnlySpan<byte> ForegroundPrefix => "\e[38;2;"u8;
```

## Roadmap

Future enhancements:
- [ ] Text rendering inside rectangles
- [ ] Nested elements (hierarchy)
- [ ] More UI primitives (text box, button, etc.)
- [ ] Input handling (keyboard, mouse)
- [ ] Layout containers (stack, grid)
- [ ] Double-buffered animations
- [ ] Performance benchmarks

## Requirements

- .NET 10.0 SDK
- C# 13 language features
- Windows 10+ (VT100 support) or Unix terminal

## License

See LICENSE file for details.

## Statistics

- **Lines of code**: ~1,300
- **Files**: 17 implementation files
- **Memory per frame**: 0 bytes allocated
- **Terminal overhead**: ~160 KB for 200×50 terminal

---

Built with performance in mind. Enjoy building fast, beautiful terminal UIs!