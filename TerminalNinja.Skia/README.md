# TerminalNinja.Skia

GPU-accelerated rendering backend for TerminalNinja — renders the same `UIElement` tree
into a windowed SDL3 + OpenGL surface via SkiaSharp and HarfBuzz. Ligatures, color emoji,
proper grapheme cluster handling, HiDPI / fractional scaling, and the standard
TerminalNinja input flow (keyboard, mouse, focus traversal) all work in this host.

## Native dependency: SDL3

This library uses SDL3 via `[LibraryImport]` for windowing and input. The native
binary (`SDL3.dll` on Windows, `libSDL3.so.0` on Linux, `libSDL3.dylib` on macOS)
must be on the dynamic loader's search path at runtime — typically next to your
application executable.

The NuGet package ships an MSBuild target that copies SDL3 from a conventional
`runtimes/<rid>/native/` layout to your build output. You just need to put the
native binary in the right place once.

### Quick setup

**Windows (PowerShell):**

```powershell
# From your project's directory:
../path/to/TerminalNinja/scripts/get-sdl3.ps1
```

This downloads SDL3 from the official libsdl-org GitHub release zip and places
`SDL3.dll` at `./runtimes/win-x64/native/SDL3.dll`. Override the RID and version with
parameters — see `Get-Help ./scripts/get-sdl3.ps1`.

**Linux / macOS (Bash):**

```bash
# 1. Install SDL3 via your package manager
sudo apt install libsdl3-0          # Debian / Ubuntu
sudo dnf install SDL3               # Fedora
sudo pacman -S sdl3                 # Arch
brew install sdl3                   # macOS

# 2. Copy it into the conventional layout
./scripts/get-sdl3.sh
```

### Manual setup

If you'd rather not run the scripts, you have three options:

1. **NuGet layout (recommended):** Place the SDL3 binary at
   `runtimes/<rid>/native/SDL3.dll` (or the platform equivalent) inside your project
   directory. The MSBuild target picks it up automatically.

2. **Beside the project file:** Drop the binary next to your `.csproj`. Same target
   picks it up.

3. **System-wide install:** If SDL3 is installed system-wide (already on PATH or
   under `/usr/lib`), set `<TerminalNinjaSkiaSkipSdl3Probe>true</TerminalNinjaSkiaSkipSdl3Probe>`
   in your project to suppress the build-time probe.

If none of the above is in place at build time, the MSBuild target emits warning
`TNSKIA001`. The build still succeeds; the app will fail at runtime with
`DllNotFoundException` when `SkiaApplication.Initialize` calls `SDL_Init`.

## Quick start

```csharp
using TerminalNinja.Controls;
using TerminalNinja.Primitives;
using TerminalNinja.Skia;

using var app = new SkiaApplication(new SkiaApplicationOptions
{
    Title = "My TerminalNinja App",
    CellsWide = 80,
    CellsTall = 24,
});

app.SetRoot(new Border
{
    Background = new Color(0x1E, 0x1E, 0x2E),
    Child = new TextBlock { Text = "Hello, GPU world!" },
});

app.Run();
```

## What's in the box

- **`SkiaApplication`** — the host. Owns the SDL3 window, GL context, GRContext,
  persistent + screen Skia surfaces, sink, renderer, focus manager, input backend.
- **`SkiaCellSink`** — implements `ICellSink` + `IShapedRunSink`. Per-cell rasterization
  for non-shaped sinks; HarfBuzz-shaped runs with SKTextBlob caching for the GUI path.
- **`SdlInputBackend`** — implements `IInputBackend`. Translates SDL3 keyboard / mouse /
  resize / display-scale events to the existing `KeyEvent` / `MouseEvent` / `ResizeEvent`
  records used by the console host.
- **MSBuild target** — `build/TerminalNinja.Skia.targets`, auto-imported via NuGet.

## Known gaps

- Bold / italic decorations are not yet wired through to a typeface fallback chain.
- Native binary auto-download as part of build is on the roadmap; for now the
  one-shot scripts above are the recommended path.
- Runtime testing in this repo currently happens via CI / a Linux box — the
  local Windows dev environment in this codebase lacks the MSVC linker, so AOT
  publish stops at "Generating native code".
