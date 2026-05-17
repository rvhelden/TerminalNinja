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

    // Scaled / "physical" metrics derived from the options + the current display scale. These
    // are what the cell sink, font, surfaces, and input backend actually use. The base values
    // on SkiaApplicationOptions are at scale 1.0; we multiply by the queried scale and round
    // to integer pixels so cell origins land on the pixel grid.
    private float _displayScale = 1f;
    private int _scaledCellWidth;
    private int _scaledCellHeight;
    private float _scaledFontSize;

    /// <summary>The most recently observed display scale (1.0 = 100%, 2.0 = 200%, etc.).</summary>
    public float DisplayScale => _displayScale;

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

        // Create the window at the base (1.0×) pixel size; we'll query the display's actual
        // scale once the window exists and resize if it differs. SDL_GetDisplayContentScale
        // pre-creation is also an option but cross-platform behavior is less reliable, and
        // this lets the SDL_WINDOW_HIGH_PIXEL_DENSITY flag inform us via the queried scale.
        var baseWidth = _options.CellWidth * _options.CellsWide;
        var baseHeight = _options.CellHeight * _options.CellsTall;

        _window = Sdl3.SDL_CreateWindow(
            _options.Title,
            baseWidth,
            baseHeight,
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

        // Apply display scaling. Resize the window to the post-scale pixel size so the
        // framebuffer matches our scaled cell metrics exactly (no fractional cells, no
        // unused border).
        UpdateScaledMetrics(QueryDisplayScale());
        if (_scaledCellWidth != _options.CellWidth || _scaledCellHeight != _options.CellHeight)
        {
            Sdl3.SDL_SetWindowSize(_window,
                _scaledCellWidth * _options.CellsWide,
                _scaledCellHeight * _options.CellsTall);
        }

        Sdl3.SDL_GetWindowSizeInPixels(_window, out _pixelWidth, out _pixelHeight);

        _font = new SKFont(_typeface, _scaledFontSize);

        CreateSurfaces(_pixelWidth, _pixelHeight);
        _sink = new SkiaCellSink(_persistentSurface!, _font, _typeface, _scaledCellWidth, _scaledCellHeight);
        _sink.ClearAll(_options.CellsWide, _options.CellsTall);
        _renderer = new Renderer(_sink, _options.CellsWide, _options.CellsTall);
        _input = new SdlInputBackend(_scaledCellWidth, _scaledCellHeight);
        if (!_options.EnableMouseTracking)
        {
            _input.DisableMouseTracking();
        }
    }

    /// <summary>
    /// Returns the current display scale (1.0 = unscaled) for the window, or 1.0 if scaling
    /// is disabled or unavailable. SDL returns 0 if it can't query the value; we clamp to 1.0.
    /// </summary>
    private float QueryDisplayScale()
    {
        if (!_options.AutoScale)
        {
            return 1f;
        }

        var scale = Sdl3.SDL_GetWindowDisplayScale(_window);
        return scale > 0f ? scale : 1f;
    }

    /// <summary>
    /// Recomputes <see cref="_scaledCellWidth"/>, <see cref="_scaledCellHeight"/>, and
    /// <see cref="_scaledFontSize"/> from the options + given scale. Cell dimensions round
    /// to nearest integer (cell origins must land on pixel boundaries — sub-pixel positioning
    /// makes the GPU sample-between-texels and blurs glyphs). Font size also rounds — Skia
    /// accepts float but the visible difference between e.g. 13.5 and 14 is mostly noise.
    /// </summary>
    private void UpdateScaledMetrics(float scale)
    {
        _displayScale = scale;
        _scaledCellWidth = Math.Max(1, (int)Math.Round(_options.CellWidth * scale, MidpointRounding.AwayFromZero));
        _scaledCellHeight = Math.Max(1, (int)Math.Round(_options.CellHeight * scale, MidpointRounding.AwayFromZero));
        _scaledFontSize = MathF.Round(_options.FontPixelSize * scale, MidpointRounding.AwayFromZero);
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
        // Peek SDL events for the display-scale-changed case before the input backend drains
        // them. The scale change is host-specific (resources to rebuild) and doesn't have a
        // clean equivalent in TerminalNinja's InputEvent records. We use SDL_PeepEvents-style
        // logic via the input backend's own state — see SdlInputBackend.ConsumeDisplayScaleChange.
        if (_input!.ConsumeDisplayScaleChange())
        {
            HandleDisplayScaleChange();
        }

        var events = _input.TryRead();
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

        // Cell counts are derived from the post-scale cell metrics so the grid stays
        // sized correctly under HiDPI: resizing on a 2× display gives e.g. 100 cells of
        // 18px each rather than 200 cells of 9px each.
        var newCellsWide = Math.Max(1, _pixelWidth / _scaledCellWidth);
        var newCellsTall = Math.Max(1, _pixelHeight / _scaledCellHeight);
        _sink.ClearAll(newCellsWide, newCellsTall);
        _renderer!.Resize(newCellsWide, newCellsTall);
    }

    private void HandleDisplayScaleChange()
    {
        var newScale = QueryDisplayScale();
        if (Math.Abs(newScale - _displayScale) < 0.001f)
        {
            return;
        }

        // Recompute scaled metrics from the new scale, then rebuild every host-owned
        // resource that depends on them: font, sink (different cell pixel sizes; the
        // SKTextBlob cache it owns is also implicitly flushed via Dispose), surfaces
        // (different pixel dimensions), and input backend (different pixel→cell
        // conversion). The renderer's cell grid stays the same — the control tree was
        // designed in cell units, not pixels.
        UpdateScaledMetrics(newScale);

        var newPxWidth = _scaledCellWidth * _options.CellsWide;
        var newPxHeight = _scaledCellHeight * _options.CellsTall;
        Sdl3.SDL_SetWindowSize(_window, newPxWidth, newPxHeight);
        Sdl3.SDL_GetWindowSizeInPixels(_window, out _pixelWidth, out _pixelHeight);

        _sink?.Dispose();
        _font?.Dispose();
        _persistentSurface?.Dispose();
        _screenSurface?.Dispose();
        _input?.Dispose();

        _font = new SKFont(_typeface!, _scaledFontSize);
        CreateSurfaces(_pixelWidth, _pixelHeight);
        _sink = new SkiaCellSink(_persistentSurface!, _font, _typeface!, _scaledCellWidth, _scaledCellHeight);
        _sink.ClearAll(_options.CellsWide, _options.CellsTall);
        _input = new SdlInputBackend(_scaledCellWidth, _scaledCellHeight);
        if (!_options.EnableMouseTracking)
        {
            _input.DisableMouseTracking();
        }

        // Force a full repaint into the freshly cleared persistent surface — _previous in
        // the cell buffer must be empty so the renderer's diff yields every non-empty cell.
        _renderer!.InvalidateDisplayCache();
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
