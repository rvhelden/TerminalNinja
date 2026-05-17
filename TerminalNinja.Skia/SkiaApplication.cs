using System.Runtime.InteropServices;
using SkiaSharp;
using TerminalNinja.Controls;
using TerminalNinja.Input;
using TerminalNinja.Primitives;
using TerminalNinja.Rendering;
using TerminalNinja.Skia.Native;

namespace TerminalNinja.Skia;

/// <summary>
/// SDL3-backed host for driving a TerminalNinja control tree through <see cref="SkiaCellSink"/>.
/// Opens a window with an OpenGL context, binds a SkiaSharp <see cref="GRContext"/> over it,
/// constructs a <see cref="Renderer"/> wired to the sink, drains SDL events through a
/// <see cref="SdlInputBackend"/>, dispatches them via a <see cref="FocusManager"/>, and
/// re-renders each frame.
/// </summary>
/// <remarks>
/// Native dependency: <c>SDL3.dll</c> on Windows, <c>libSDL3.so.0</c> on Linux must be on
/// the dynamic-library search path. Packaging that as a NuGet runtime asset is a separate task.
/// </remarks>
public sealed class SkiaApplication : IDisposable
{
    private readonly SkiaApplicationOptions _options;
    private IntPtr _window;
    private IntPtr _glContext;
    private GRContext? _grContext;
    private GRGlInterface? _grGlInterface;
    private SKSurface? _persistentSurface;
    private SKSurface? _screenSurface;
    private SKTypeface? _typeface;
    private SKFont? _font;
    private SkiaCellSink? _sink;
    private Renderer? _renderer;
    private SdlInputBackend? _input;
    private UIElement? _root;
    private bool _running;
    private bool _disposed;
    private int _pixelWidth;
    private int _pixelHeight;

    /// <summary>Active renderer, wired to a <see cref="SkiaCellSink"/>. Available after <see cref="Run"/> starts.</summary>
    public Renderer Renderer => _renderer ?? throw new InvalidOperationException("Renderer is not available until Run() has initialized the host.");

    /// <summary>The Skia sink the renderer drives. Available after <see cref="Run"/> starts.</summary>
    public SkiaCellSink Sink => _sink ?? throw new InvalidOperationException("Sink is not available until Run() has initialized the host.");

    /// <summary>The input backend draining SDL events for the run loop. Toggleable mouse tracking lives here.</summary>
    public SdlInputBackend Input => _input ?? throw new InvalidOperationException("Input backend is not available until Run() has initialized the host.");

    /// <summary>Manages keyboard focus and mouse hover for the active control tree.</summary>
    public FocusManager FocusManager { get; } = new();

    /// <summary>Raised after every <see cref="KeyEvent"/> the host receives, before <see cref="FocusManager"/> dispatch.</summary>
    public event Action<KeyEvent>? KeyDown;

    /// <summary>Raised after every <see cref="MouseEvent"/> the host receives, before <see cref="FocusManager"/> dispatch.</summary>
    public event Action<MouseEvent>? MouseInput;

    /// <summary>Creates a host with the given options. Initialization happens on the first call to <see cref="Run"/>.</summary>
    public SkiaApplication(SkiaApplicationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>Sets the root <see cref="UIElement"/> the host draws each frame.</summary>
    public void SetRoot(UIElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        _root = root;
    }

    /// <summary>
    /// Initializes SDL3 + GL + Skia and runs the event/render loop until <see cref="Stop"/>
    /// is called or the user closes the window.
    /// </summary>
    public void Run()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Initialize();

        _running = true;
        try
        {
            while (_running)
            {
                if (!PumpEvents())
                {
                    break;
                }

                RenderFrame();
                Sdl3.SDL_GL_SwapWindow(_window);
            }
        }
        finally
        {
            Shutdown();
        }
    }

    /// <summary>Signals the event loop to exit at the start of the next iteration.</summary>
    public void Stop() => _running = false;

    private void Initialize()
    {
        if (Sdl3.SDL_Init(Sdl3.SDL_INIT_VIDEO | Sdl3.SDL_INIT_EVENTS) != 1)
        {
            throw new InvalidOperationException($"SDL_Init failed: {GetSdlError()}");
        }

        Sdl3.SDL_GL_SetAttribute(Sdl3.SDL_GL_STENCIL_SIZE, 8);
        Sdl3.SDL_GL_SetAttribute(Sdl3.SDL_GL_DOUBLEBUFFER, 1);
        Sdl3.SDL_GL_SetAttribute(Sdl3.SDL_GL_DEPTH_SIZE, 0);

        _pixelWidth = _options.CellWidth * _options.CellsWide;
        _pixelHeight = _options.CellHeight * _options.CellsTall;

        _window = Sdl3.SDL_CreateWindow(
            _options.Title,
            _pixelWidth,
            _pixelHeight,
            Sdl3.SDL_WINDOW_OPENGL | Sdl3.SDL_WINDOW_RESIZABLE | Sdl3.SDL_WINDOW_HIGH_PIXEL_DENSITY);

        if (_window == IntPtr.Zero)
        {
            throw new InvalidOperationException($"SDL_CreateWindow failed: {GetSdlError()}");
        }

        _glContext = Sdl3.SDL_GL_CreateContext(_window);
        if (_glContext == IntPtr.Zero)
        {
            Sdl3.SDL_DestroyWindow(_window);
            throw new InvalidOperationException($"SDL_GL_CreateContext failed: {GetSdlError()}");
        }

        Sdl3.SDL_GL_MakeCurrent(_window, _glContext);
        Sdl3.SDL_GL_SetSwapInterval(_options.VSync ? 1 : 0);

        _grGlInterface = GRGlInterface.CreateOpenGl(Sdl3.SDL_GL_GetProcAddress)
            ?? throw new InvalidOperationException("GRGlInterface.CreateOpenGl returned null.");
        _grContext = GRContext.CreateGl(_grGlInterface)
            ?? throw new InvalidOperationException("GRContext.CreateGl returned null.");

        _typeface = _options.FontFamily is null
            ? SKTypeface.Default
            : SKTypeface.FromFamilyName(_options.FontFamily) ?? SKTypeface.Default;
        _font = new SKFont(_typeface, _options.FontPixelSize);

        // Get the actual framebuffer size (HiDPI may make it larger than the logical size).
        Sdl3.SDL_GetWindowSizeInPixels(_window, out _pixelWidth, out _pixelHeight);

        CreateSurfaces(_pixelWidth, _pixelHeight);
        _sink = new SkiaCellSink(_persistentSurface!, _font, _typeface, _options.CellWidth, _options.CellHeight);
        _sink.ClearAll(_options.CellsWide, _options.CellsTall); // wipe initial undefined GPU texture state to default bg
        _renderer = new Renderer(_sink, _options.CellsWide, _options.CellsTall);
        _input = new SdlInputBackend(_options.CellWidth, _options.CellHeight);
        if (!_options.EnableMouseTracking)
        {
            _input.DisableMouseTracking();
        }
    }

    private void CreateSurfaces(int width, int height)
    {
        // Two surfaces. The persistent surface is a Skia-allocated GPU texture that survives
        // between frames — the sink paints only the dirty region into it each frame, so
        // non-changing pixels are preserved without a per-frame full clear. The screen surface
        // wraps the default GL framebuffer (fboId=0); we copy the persistent surface onto it
        // before each SDL_GL_SwapWindow so the user sees the current state.
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        _persistentSurface = SKSurface.Create(_grContext!, budgeted: false, info)
            ?? throw new InvalidOperationException("SKSurface.Create (persistent) returned null.");

        var glFbInfo = new GRGlFramebufferInfo(fboId: 0, format: 0x8058u /* GL_RGBA8 */);
        var renderTarget = new GRBackendRenderTarget(width, height, sampleCount: 0, stencilBits: 8, glFbInfo);
        _screenSurface = SKSurface.Create(_grContext!, renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888)
            ?? throw new InvalidOperationException("SKSurface.Create (screen) returned null — Skia could not wrap the framebuffer.");
    }

    private bool PumpEvents()
    {
        var events = _input!.TryRead();
        if (_input.QuitRequested)
        {
            return false;
        }

        if (events is null)
        {
            return true;
        }

        foreach (var evt in events)
        {
            switch (evt)
            {
                case KeyEvent key:
                    KeyDown?.Invoke(key);

                    if (_options.EscapeQuits && key.Key == ConsoleKey.Escape)
                    {
                        return false;
                    }

                    if (_options.EnableTabNavigation && _root is not null && key.Key == ConsoleKey.Tab)
                    {
                        var bounds = new Rect(0, 0, _renderer!.Width, _renderer.Height);
                        if (key.Shift)
                        {
                            FocusManager.FocusPrevious(_root, bounds);
                        }
                        else
                        {
                            FocusManager.FocusNext(_root, bounds);
                        }

                        break;
                    }

                    FocusManager.HandleKeyEvent(key);
                    break;

                case MouseEvent mouse:
                    MouseInput?.Invoke(mouse);
                    if (_root is not null)
                    {
                        var bounds = new Rect(0, 0, _renderer!.Width, _renderer.Height);
                        FocusManager.HandleMouseEvent(_root, bounds, mouse);
                    }

                    break;

                case ResizeEvent:
                    // SDL3 also emits SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED through TryRead's
                    // internal poll, but the cell-grid resize uses the framebuffer pixel size
                    // queried directly from SDL — more reliable on HiDPI than the resize event's
                    // logical-units payload.
                    HandleResize();
                    break;
            }
        }

        return true;
    }

    private void HandleResize()
    {
        Sdl3.SDL_GetWindowSizeInPixels(_window, out var newPxWidth, out var newPxHeight);
        if (newPxWidth == _pixelWidth && newPxHeight == _pixelHeight)
        {
            return;
        }

        _pixelWidth = newPxWidth;
        _pixelHeight = newPxHeight;

        _persistentSurface?.Dispose();
        _screenSurface?.Dispose();
        CreateSurfaces(_pixelWidth, _pixelHeight);
        _sink!.SetSurface(_persistentSurface!);

        var newCellsWide = Math.Max(1, _pixelWidth / _options.CellWidth);
        var newCellsTall = Math.Max(1, _pixelHeight / _options.CellHeight);
        _sink.ClearAll(newCellsWide, newCellsTall); // fresh GPU texture has undefined contents
        _renderer!.Resize(newCellsWide, newCellsTall);
    }

    private void RenderFrame()
    {
        if (_root is null)
        {
            return;
        }

        // Persistent-surface model: the cell sink paints only the dirty region of the
        // persistent surface each frame, so we deliberately do NOT canvas.Clear() it here.
        // Non-changing pixels stay intact from previous frames. Initial clear happens at
        // surface creation; subsequent frames trust the renderer's dirty-region drawing.
        _renderer!.Clear();
        _renderer.Draw(_root);
        _renderer.Present();

        // Blit the persistent surface onto the GL default framebuffer so SDL_GL_SwapWindow
        // shows the current state. Snapshot on a GPU surface is cheap (texture reference).
        using var snapshot = _persistentSurface!.Snapshot();
        _screenSurface!.Canvas.DrawImage(snapshot, 0, 0);
        _screenSurface.Canvas.Flush();
    }

    private void Shutdown()
    {
        _input?.Dispose();
        _renderer?.Dispose();
        _sink?.Dispose();
        _persistentSurface?.Dispose();
        _screenSurface?.Dispose();
        _font?.Dispose();
        _typeface?.Dispose();
        _grContext?.Dispose();
        _grGlInterface?.Dispose();

        if (_glContext != IntPtr.Zero)
        {
            Sdl3.SDL_GL_DestroyContext(_glContext);
            _glContext = IntPtr.Zero;
        }

        if (_window != IntPtr.Zero)
        {
            Sdl3.SDL_DestroyWindow(_window);
            _window = IntPtr.Zero;
        }

        Sdl3.SDL_Quit();

        _renderer = null;
        _sink = null;
        _persistentSurface = null;
        _screenSurface = null;
        _font = null;
        _typeface = null;
        _grContext = null;
        _grGlInterface = null;
        _input = null;
    }

    private static string GetSdlError()
    {
        var ptr = Sdl3.SDL_GetError();
        return ptr == IntPtr.Zero ? "<no error>" : Marshal.PtrToStringUTF8(ptr) ?? "<unreadable>";
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_running)
        {
            _running = false;
        }

        Shutdown();
    }
}
