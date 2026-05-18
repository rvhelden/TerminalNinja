using System;
using System.Runtime.InteropServices;
using SkiaSharp;
using TerminalNinja.App;
using TerminalNinja.Controls;
using TerminalNinja.Input;
using TerminalNinja.Rendering;
using TerminalNinja.Resources;
using TerminalNinja.Skia.Native;

namespace TerminalNinja.Skia;

/// <summary>
/// SDL3-backed host for driving a TerminalNinja control tree through <see cref="SkiaCellSink"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SkiaApplication"/> wraps a regular <see cref="TerminalNinja.App.Application"/>
/// internally with our renderer + input backend injected via
/// <see cref="ApplicationOptions.RendererOverride"/>. Setting up the inner application also
/// publishes <see cref="TerminalNinja.App.Application.Current"/>, which controls like
/// <c>Window</c>, <c>Popup</c>, <c>RadioButton</c>, and the <c>FrameworkElement</c> resource
/// lookup chain depend on. From the consumer's view, <see cref="SkiaApplication"/> exposes
/// the same surface (Renderer, FocusManager, Resources, theme, overlays) as
/// <see cref="TerminalNinja.App.Application"/>; the GL/SDL3 specifics stay inside.
/// </para>
/// <para>
/// Native dependency: <c>SDL3.dll</c> on Windows, <c>libSDL3.so.0</c> on Linux must be on
/// the dynamic-library search path. See the NuGet package's <c>build/TerminalNinja.Skia.targets</c>
/// for the recommended layout.
/// </para>
/// </remarks>
public sealed class SkiaApplication : IDisposable
{
    private readonly SkiaApplicationOptions _options;
    private UIElement? _pendingRoot;
    private string? _pendingThemeName;
    private Action<KeyEvent, KeyEventArgs>? _pendingKeyDown;
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
    private Application? _app;
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

    /// <summary>The underlying <see cref="TerminalNinja.App.Application"/> instance that backs this host.</summary>
    public Application Application => _app ?? throw new InvalidOperationException("Application is not available until Run() has initialized the host.");

    /// <summary>The focus manager owned by the underlying <see cref="Application"/>.</summary>
    public FocusManager FocusManager => Application.FocusManager;

    /// <summary>Application-level resource dictionary (shared with the inner Application).</summary>
    public ResourceDictionary Resources => Application.Resources;

    /// <summary>Active theme name. Pass-through to <see cref="Application.ThemeName"/>.</summary>
    /// <remarks>
    /// Safe to set before <see cref="Run"/>: the value is queued and applied once
    /// <see cref="Initialize"/> has built the inner <see cref="App.Application"/>.
    /// </remarks>
    public string? ThemeName
    {
        get => _app is null ? _pendingThemeName : _app.ThemeName;
        set
        {
            if (_app is null)
            {
                _pendingThemeName = value;
            }
            else
            {
                _app.ThemeName = value;
            }
        }
    }

    /// <summary>
    /// Raised once after the inner <see cref="App.Application"/>, renderer, and input backend
    /// have been built, but before the first <c>ProcessTick</c>. Use it to set initial focus,
    /// adjust resources, or wire any state that requires the live Application.
    /// </summary>
    public event Action? Initialized;

    /// <summary>Raised after every <see cref="KeyEvent"/> the underlying application handles.</summary>
    /// <remarks>
    /// Subscriptions added before <see cref="Run"/> are forwarded to
    /// <see cref="App.Application.KeyDown"/> once the inner application exists.
    /// </remarks>
    public event Action<KeyEvent, KeyEventArgs>? KeyDown
    {
        add
        {
            if (value is null) return;
            if (_app is null)
            {
                _pendingKeyDown += value;
            }
            else
            {
                _app.KeyDown += value;
            }
        }
        remove
        {
            if (value is null) return;
            if (_app is null)
            {
                _pendingKeyDown -= value;
            }
            else
            {
                _app.KeyDown -= value;
            }
        }
    }

    /// <summary>Creates a host with the given options. Initialization happens on the first call to <see cref="Run"/>.</summary>
    public SkiaApplication(SkiaApplicationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>Sets the root <see cref="UIElement"/> the host draws each frame.</summary>
    /// <remarks>
    /// Safe to call before <see cref="Run"/>: the value is queued and applied once
    /// <see cref="Initialize"/> has built the inner <see cref="App.Application"/>.
    /// </remarks>
    public void SetRoot(UIElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (_app is null)
        {
            _pendingRoot = root;
        }
        else
        {
            _app.RootControl = root;
        }
    }

    /// <summary>
    /// Initializes SDL3 + GL + Skia + the underlying <see cref="App.Application"/> and runs
    /// the event/render loop until <see cref="Stop"/> is called or the user closes the window.
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
                // SDL3-specific: handle scale change before the Application sees the frame
                // so the rebuilt sink + font are in place when controls render.
                if (_input!.ConsumeDisplayScaleChange())
                {
                    HandleDisplayScaleChange();
                }

                // Hand off to Application: pump input, dispatch to controls, draw into our
                // persistent Skia surface. Application does not call SDL_GL_SwapWindow —
                // we own that after the blit below.
                _app!.ProcessTick();

                if (_input.QuitRequested || !_running)
                {
                    break;
                }

                // Blit the persistent surface onto the GL default framebuffer so the swap
                // shows the current state. Snapshot on a GPU surface is cheap.
                using var snapshot = _persistentSurface!.Snapshot();
                _screenSurface!.Canvas.DrawImage(snapshot, 0, 0);
                _screenSurface.Canvas.Flush();
                Sdl3.SDL_GL_SwapWindow(_window);
            }
        }
        finally
        {
            Shutdown();
        }
    }

    /// <summary>Signals the event loop to exit at the start of the next iteration.</summary>
    public void Stop()
    {
        _running = false;
        _app?.Exit();
    }

    private void Initialize()
    {
        if (!Sdl3.SDL_Init(Sdl3.SDL_INIT_VIDEO | Sdl3.SDL_INIT_EVENTS))
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

        ApplyWindowIcon();

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

        // Resolution order:
        //   1. An explicit pre-loaded typeface (consumer shipped a font file / embedded resource).
        //   2. A family-name lookup against the system font manager.
        //   3. Skia's default typeface as a final fallback.
        // (1) lets apps like NinjaShell ship a Nerd Font in-assembly so symbols + ligatures
        // render correctly on machines that don't have the font installed system-wide.
        _typeface = _options.Typeface
            ?? (_options.FontFamily is null
                ? SKTypeface.Default
                : SKTypeface.FromFamilyName(_options.FontFamily) ?? SKTypeface.Default);

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

        // Enable SDL3 text input so SDL_EVENT_TEXT_INPUT events flow. Without this, the only
        // source of typed characters is SDL_EVENT_KEY_DOWN's logical keycode — which is
        // unreliable for shifted symbols, dead keys, IME composition, and non-US layouts.
        // The TerminalView and TextBox controls receive the resulting characters via the
        // KeyEvent stream from SdlInputBackend.
        Sdl3.SDL_StartTextInput(_window);

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

        // Wrap a TerminalNinja.App.Application around our renderer + input. This sets
        // Application.Current (controls like Window.Show / Popup / RadioButton focus
        // require it), wires the FrameworkElement resource lookup, and gives us
        // ProcessTick to drive the standard input + render flow without duplicating it.
        _app = new Application(new ApplicationOptions
        {
            RendererOverride = _renderer,
            InputBackend = _input,
            EnableMouseTracking = _options.EnableMouseTracking,
            EnableTabNavigation = _options.EnableTabNavigation,
            // VSync paces the loop; bypass Application's Thread.Sleep frame limiter.
            TargetFps = 1000,
            SuppressConsoleSetup = true,
        });

        // Replace the headless-default ProcessClipboard with the SDL3-bridged one so
        // copy/paste flows to the real OS clipboard. Tests / non-Skia hosts keep the
        // process-internal default.
        _app.Clipboard = new Sdl3Clipboard();

        // Application.HandleResizeEvent fires our Resize subscriber after the input pump
        // sees a ResizeEvent. The console renderer's HandleResize is a no-op for our
        // sink-backed Renderer, so we do the SDL3-specific rebuild ourselves here.
        _app.Resize += _ => HandleSdlResize();

        // Flush anything that was configured before Run(): theme, root, KeyDown subscribers.
        if (_pendingThemeName is not null)
        {
            _app.ThemeName = _pendingThemeName;
            _pendingThemeName = null;
        }
        if (_pendingRoot is not null)
        {
            _app.RootControl = _pendingRoot;
            _pendingRoot = null;
        }
        if (_pendingKeyDown is not null)
        {
            _app.KeyDown += _pendingKeyDown;
            _pendingKeyDown = null;
        }

        Initialized?.Invoke();
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

    private void HandleSdlResize()
    {
        // Called from Application.Resize after the input pump sees a SDL3-originated
        // ResizeEvent. The console renderer's HandleResize is a no-op for our sink-backed
        // Renderer, so we own the actual GL surface rebuild here.
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

        // Cell counts derive from the post-scale cell metrics so a resize on a HiDPI
        // display gives the expected cell-grid size (e.g. 100 cells of 18px at 2× rather
        // than 200 cells of 9px).
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

        // Rebuild every host-owned resource whose pixel dimensions depend on scale:
        // font, sink (different cell pixel sizes; SKTextBlob cache flushes via Dispose),
        // surfaces (different pixel dimensions). The renderer and input backend stay alive —
        // they're owned by the inner Application and we hot-swap their state instead:
        //   • Renderer.SwapSink replaces the sink reference; the buffer + dirty tracking
        //     keep their cell-grid identity.
        //   • SdlInputBackend.SetCellMetrics updates pixel→cell conversion in place.
        // The cell grid (cellsWide / cellsTall) is unchanged — the control tree was designed
        // in cell units, not pixels.
        UpdateScaledMetrics(newScale);

        var newPxWidth = _scaledCellWidth * _options.CellsWide;
        var newPxHeight = _scaledCellHeight * _options.CellsTall;
        Sdl3.SDL_SetWindowSize(_window, newPxWidth, newPxHeight);
        Sdl3.SDL_GetWindowSizeInPixels(_window, out _pixelWidth, out _pixelHeight);

        var oldSink = _sink;
        var oldFont = _font;
        _persistentSurface?.Dispose();
        _screenSurface?.Dispose();

        _font = new SKFont(_typeface!, _scaledFontSize);
        CreateSurfaces(_pixelWidth, _pixelHeight);
        _sink = new SkiaCellSink(_persistentSurface!, _font, _typeface!, _scaledCellWidth, _scaledCellHeight);
        _sink.ClearAll(_options.CellsWide, _options.CellsTall);
        _renderer!.SwapSink(_sink);
        _input!.SetCellMetrics(_scaledCellWidth, _scaledCellHeight);

        // Dispose old resources after the swap so the renderer never sees a disposed sink.
        oldSink?.Dispose();
        oldFont?.Dispose();

        // Force a full repaint into the freshly cleared persistent surface — _previous in
        // the cell buffer must be empty so the renderer's diff yields every non-empty cell.
        _renderer.InvalidateDisplayCache();
        _app!.Invalidate();
    }

    private void Shutdown()
    {
        // Dispose the inner Application first — it owns the renderer + input backend (via
        // RendererOverride / InputBackend injection) and releases them through its own
        // disposer. The renderer's current sink is also disposed there.
        _app?.Dispose();

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

        _app = null;
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

    /// <summary>
    /// Pushes <see cref="SkiaApplicationOptions.WindowIcon"/> (if any) onto the SDL window so
    /// taskbar / window-chrome glyphs render with the app's brand. SDL3 wants an SDL_Surface,
    /// so we wrap the bitmap's pixel buffer in a throwaway one via <c>SDL_CreateSurfaceFrom</c>.
    /// SDL_SetWindowIcon copies the image internally, so the surface + bitmap can be released
    /// the moment the call returns — no pinning lifetime to manage. Failures are non-fatal:
    /// the icon is purely cosmetic, and the run loop is what consumers actually care about.
    /// </summary>
    private void ApplyWindowIcon()
    {
        var icon = _options.WindowIcon;
        if (icon is null)
        {
            return;
        }

        var pixels = icon.GetPixels();
        if (pixels == IntPtr.Zero)
        {
            icon.Dispose();
            return;
        }

        var surface = Sdl3.SDL_CreateSurfaceFrom(
            icon.Width,
            icon.Height,
            Sdl3.SDL_PIXELFORMAT_ABGR8888,
            pixels,
            icon.RowBytes);

        if (surface != IntPtr.Zero)
        {
            Sdl3.SDL_SetWindowIcon(_window, surface);
            Sdl3.SDL_DestroySurface(surface);
        }

        // SDL has copied the pixels internally by now (SDL_SetWindowIcon docs); the bitmap is
        // no longer needed. Disposing here matches the typeface-ownership pattern documented
        // on SkiaApplicationOptions.WindowIcon.
        icon.Dispose();
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
