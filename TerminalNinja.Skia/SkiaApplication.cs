using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SkiaSharp;
using TerminalNinja.Controls;
using TerminalNinja.Rendering;
using TerminalNinja.Skia.Native;

namespace TerminalNinja.Skia;

/// <summary>
/// SDL3-backed host for driving a TerminalNinja control tree through <see cref="SkiaCellSink"/>.
/// Opens a window with an OpenGL context, binds a SkiaSharp <see cref="GRContext"/> over it,
/// constructs a <see cref="Renderer"/> wired to the sink, and pumps an event loop that
/// re-renders each frame.
/// </summary>
/// <remarks>
/// <para>
/// This is the Step 6 deliverable: per-cell rasterization with no HarfBuzz shaping yet,
/// and minimal input (window-close + Escape-to-quit). Full input integration with
/// TerminalNinja's <c>IInputBackend</c> is Step 9.
/// </para>
/// <para>
/// Native dependency: <c>SDL3.dll</c> on Windows, <c>libSDL3.so.0</c> on Linux must be on
/// the dynamic-library search path. Packaging that as a NuGet runtime asset is a separate task.
/// </para>
/// </remarks>
public sealed class SkiaApplication : IDisposable
{
    private readonly SkiaApplicationOptions _options;
    private IntPtr _window;
    private IntPtr _glContext;
    private GRContext? _grContext;
    private GRGlInterface? _grGlInterface;
    private SKSurface? _surface;
    private SKTypeface? _typeface;
    private SKFont? _font;
    private SkiaCellSink? _sink;
    private Renderer? _renderer;
    private UIElement? _root;
    private bool _running;
    private bool _disposed;
    private int _pixelWidth;
    private int _pixelHeight;

    /// <summary>Active renderer, wired to a <see cref="SkiaCellSink"/>. Available after <see cref="Run"/> starts.</summary>
    public Renderer Renderer => _renderer ?? throw new InvalidOperationException("Renderer is not available until Run() has initialized the host.");

    /// <summary>The Skia sink the renderer drives. Available after <see cref="Run"/> starts.</summary>
    public SkiaCellSink Sink => _sink ?? throw new InvalidOperationException("Sink is not available until Run() has initialized the host.");

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

        _surface = CreateSurface(_pixelWidth, _pixelHeight);
        _sink = new SkiaCellSink(_surface, _font, _options.CellWidth, _options.CellHeight);
        _renderer = new Renderer(_sink, _options.CellsWide, _options.CellsTall);
    }

    private SKSurface CreateSurface(int width, int height)
    {
        // Default framebuffer (id 0) wrapped as a Skia render target. GL_RGBA8 = 0x8058.
        var glFbInfo = new GRGlFramebufferInfo(fboId: 0, format: 0x8058u);
        var renderTarget = new GRBackendRenderTarget(width, height, sampleCount: 0, stencilBits: 8, glFbInfo);
        return SKSurface.Create(_grContext!, renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888)
            ?? throw new InvalidOperationException("SKSurface.Create returned null — Skia could not wrap the framebuffer.");
    }

    private bool PumpEvents()
    {
        while (Sdl3.SDL_PollEvent(out var evt) == 1)
        {
            if (evt.type == Sdl3.SDL_EVENT_QUIT)
            {
                return false;
            }

            if (evt.type == Sdl3.SDL_EVENT_KEY_DOWN)
            {
                ref var key = ref Unsafe.As<Sdl3.SDL_Event, Sdl3.SDL_KeyboardEvent>(ref evt);
                if (key.key == Sdl3.SDLK_ESCAPE)
                {
                    return false;
                }
            }

            if (evt.type == Sdl3.SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED)
            {
                HandleResize();
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

        _surface?.Dispose();
        _surface = CreateSurface(_pixelWidth, _pixelHeight);
        _sink!.SetSurface(_surface);

        var newCellsWide = Math.Max(1, _pixelWidth / _options.CellWidth);
        var newCellsTall = Math.Max(1, _pixelHeight / _options.CellHeight);
        _renderer!.Resize(newCellsWide, newCellsTall);
    }

    private void RenderFrame()
    {
        if (_root is null)
        {
            return;
        }

        // Clear the surface to a neutral background before drawing the cell grid. The cell
        // sink paints solid background rectangles per cell, so this only shows through if
        // the grid is shorter than the surface (e.g. fractional cell sizes at the bottom).
        _surface!.Canvas.Clear(new SKColor(0x12, 0x12, 0x1A));

        _renderer!.Clear();
        _renderer.Draw(_root);
        _renderer.Present();
    }

    private void Shutdown()
    {
        _renderer?.Dispose();
        _sink?.Dispose();
        _surface?.Dispose();
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
        _surface = null;
        _font = null;
        _typeface = null;
        _grContext = null;
        _grGlInterface = null;
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
