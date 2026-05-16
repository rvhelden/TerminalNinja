using System.Runtime.InteropServices;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace SkiaSpike;

/// <summary>
/// Step 5 / AOT canary: open a Silk.NET window with an OpenGL context, bind a SkiaSharp
/// GRContext over it, and draw a single line of text that exercises ASCII, ligatures,
/// CJK wide characters, and a ZWJ emoji. The goal is not visual quality — the goal is to
/// prove every package in this stack publishes cleanly under Native AOT before we commit
/// to the SkiaCellSink work in TerminalNinja proper.
/// </summary>
internal static class Program
{
    private const string TestText = "Hello fi -> != == >>= 中国 \U0001F468‍\U0001F469‍\U0001F467‍\U0001F466";

    private static GL? _gl;
    private static GRContext? _grContext;
    private static GRGlInterface? _grGlInterface;
    private static SKTypeface? _typeface;
    private static SKFont? _font;

    public static int Main(string[] args)
    {
        var options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(960, 240),
            Title = "TerminalNinja — Skia AOT spike",
            VSync = true,
            PreferredDepthBufferBits = 0,
            PreferredStencilBufferBits = 8,
        };

        using var window = Window.Create(options);

        window.Load += () => OnLoad(window);
        window.Render += _ => OnRender(window);
        window.Closing += OnClosing;

        // If anyone runs this and the window stays open with the test string visible,
        // the spike has confirmed runtime as well as AOT. AOT-only verification (no
        // window) happens via `dotnet publish -r <rid> -c Release` succeeding without
        // IL3000-class warnings.
        window.Run();
        return 0;
    }

    private static void OnLoad(IWindow window)
    {
        _gl = window.CreateOpenGL();

        // GRGlInterface.CreateOpenGl takes a glGetProc-style callback. Silk.NET's GL
        // wrapper exposes the address loader via its underlying IGLContext.
        var glContext = window.GLContext
            ?? throw new InvalidOperationException("Silk.NET did not surface a GL context — windowing/GL packages mismatched.");
        _grGlInterface = GRGlInterface.CreateOpenGl(name =>
            glContext.TryGetProcAddress(name, out var addr) ? addr : IntPtr.Zero);

        _grContext = GRContext.CreateGl(_grGlInterface)
            ?? throw new InvalidOperationException("GRContext.CreateGl returned null — Skia could not bind to the GL context.");

        _typeface = SKTypeface.Default;
        _font = new SKFont(_typeface, size: 32);
    }

    private static void OnRender(IWindow window)
    {
        if (_gl is null || _grContext is null || _font is null)
        {
            return;
        }

        var size = window.FramebufferSize;
        var width = size.X;
        var height = size.Y;

        _gl.Viewport(0, 0, (uint)width, (uint)height);
        _gl.ClearColor(0.07f, 0.07f, 0.10f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.StencilBufferBit);

        // Wrap the default framebuffer (0) in a Skia backend render target. Stencil bits
        // come from the GL context request above; sample count is 0 for the default FBO.
        var glFbInfo = new GRGlFramebufferInfo(fboId: 0, format: 0x8058 /* GL_RGBA8 */);
        using var renderTarget = new GRBackendRenderTarget(width, height, sampleCount: 0, stencilBits: 8, glFbInfo);
        using var surface = SKSurface.Create(_grContext, renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888)
            ?? throw new InvalidOperationException("SKSurface.Create returned null — render target / GL state mismatch.");

        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(0x12, 0x12, 0x1A));

        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };

        // SKShaper drives HarfBuzz under the hood; this is the path that has to work
        // under AOT for ligatures and complex scripts to render correctly.
        using var shaper = new SKShaper(_typeface);
        canvas.DrawShapedText(shaper, TestText, x: 24, y: 96, _font, paint);

        // Direct (non-shaped) draw as a control: confirms basic Skia text path also works,
        // and gives a visual baseline if shaping silently degrades.
        canvas.DrawText("[direct] " + TestText, x: 24, y: 160, SKTextAlign.Left, _font, paint);

        canvas.Flush();
        _grContext.Flush();
    }

    private static void OnClosing()
    {
        _font?.Dispose();
        _typeface?.Dispose();
        _grContext?.Dispose();
        _grGlInterface?.Dispose();
    }

    // Reference to keep the runtime from trimming HarfBuzzSharp away if every direct use
    // happens to be inlined / DCE'd. Defensive — should not be necessary, but cheap.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1823")]
    private static readonly Type _harfBuzzReference = typeof(HarfBuzzSharp.Blob);
}
