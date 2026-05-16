using System.Runtime.InteropServices;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace SkiaSpike;

/// <summary>
/// Step 5 / AOT canary: open an SDL3 window with an OpenGL context, bind a SkiaSharp
/// GRContext over it, and draw a single line of text that exercises ASCII, ligatures,
/// CJK wide characters, and a ZWJ emoji. Pure SDL3 P/Invoke + Skia — no Silk.NET,
/// no SkiaSharp.Views.* — so the entire stack is AOT-clean.
/// </summary>
internal static class Program
{
    private const string TestText = "Hello fi -> != == >>= 中国 \U0001F468‍\U0001F469‍\U0001F467‍\U0001F466";

    public static int Main(string[] args)
    {
        if (Sdl3.SDL_Init(Sdl3.SDL_INIT_VIDEO | Sdl3.SDL_INIT_EVENTS) != 1)
        {
            ReportSdlError("SDL_Init");
            return 1;
        }

        try
        {
            return RunLoop();
        }
        finally
        {
            Sdl3.SDL_Quit();
        }
    }

    private static int RunLoop()
    {
        // Request a stencil buffer; Skia uses it for clipping. 8 bits is plenty for our needs.
        Sdl3.SDL_GL_SetAttribute(Sdl3.SDL_GL_STENCIL_SIZE, 8);
        Sdl3.SDL_GL_SetAttribute(Sdl3.SDL_GL_DOUBLEBUFFER, 1);
        Sdl3.SDL_GL_SetAttribute(Sdl3.SDL_GL_DEPTH_SIZE, 0);

        var window = Sdl3.SDL_CreateWindow(
            "TerminalNinja — Skia AOT spike",
            w: 960,
            h: 240,
            flags: Sdl3.SDL_WINDOW_OPENGL | Sdl3.SDL_WINDOW_RESIZABLE | Sdl3.SDL_WINDOW_HIGH_PIXEL_DENSITY);

        if (window == IntPtr.Zero)
        {
            ReportSdlError("SDL_CreateWindow");
            return 2;
        }

        var glContext = Sdl3.SDL_GL_CreateContext(window);
        if (glContext == IntPtr.Zero)
        {
            ReportSdlError("SDL_GL_CreateContext");
            Sdl3.SDL_DestroyWindow(window);
            return 3;
        }

        Sdl3.SDL_GL_MakeCurrent(window, glContext);
        Sdl3.SDL_GL_SetSwapInterval(1); // vsync

        // Skia's GL interface uses SDL3's proc-address loader directly. No Silk.NET.OpenGL
        // wrapper needed — Skia drives every GL call internally.
        using var grGlInterface = GRGlInterface.CreateOpenGl(Sdl3.SDL_GL_GetProcAddress)
            ?? throw new InvalidOperationException("GRGlInterface.CreateOpenGl returned null.");
        using var grContext = GRContext.CreateGl(grGlInterface)
            ?? throw new InvalidOperationException("GRContext.CreateGl returned null — Skia failed to bind to the GL context.");

        using var typeface = SKTypeface.Default;
        using var font = new SKFont(typeface, size: 32);
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var shaper = new SKShaper(typeface);

        try
        {
            var running = true;
            while (running)
            {
                while (Sdl3.SDL_PollEvent(out var evt) == 1)
                {
                    if (evt.type == Sdl3.SDL_EVENT_QUIT)
                    {
                        running = false;
                        break;
                    }

                    if (evt.type == Sdl3.SDL_EVENT_KEY_DOWN)
                    {
                        // Reinterpret the SDL_Event union as the SDL_KeyboardEvent variant.
                        ref var key = ref System.Runtime.CompilerServices.Unsafe.As<Sdl3.SDL_Event, Sdl3.SDL_KeyboardEvent>(ref evt);
                        if (key.key == Sdl3.SDLK_ESCAPE)
                        {
                            running = false;
                            break;
                        }
                    }
                }

                if (!running) break;

                RenderFrame(window, grContext, font, paint, shaper);
                Sdl3.SDL_GL_SwapWindow(window);
            }
        }
        finally
        {
            Sdl3.SDL_GL_DestroyContext(glContext);
            Sdl3.SDL_DestroyWindow(window);
        }

        return 0;
    }

    private static void RenderFrame(IntPtr window, GRContext grContext, SKFont font, SKPaint paint, SKShaper shaper)
    {
        Sdl3.SDL_GetWindowSizeInPixels(window, out var width, out var height);

        // Default framebuffer (id 0) wrapped as a Skia render target. RGBA8 internal format.
        var glFbInfo = new GRGlFramebufferInfo(fboId: 0, format: 0x8058u /* GL_RGBA8 */);
        using var renderTarget = new GRBackendRenderTarget(width, height, sampleCount: 0, stencilBits: 8, glFbInfo);
        using var surface = SKSurface.Create(grContext, renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888)
            ?? throw new InvalidOperationException("SKSurface.Create returned null.");

        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(0x12, 0x12, 0x1A));

        // SKShaper drives HarfBuzz; this is the path that has to work under AOT for
        // ligatures and complex scripts to render correctly.
        canvas.DrawShapedText(shaper, TestText, x: 24, y: 96, font, paint);

        // Direct (non-shaped) draw as a control: confirms basic Skia text path also works,
        // and gives a visual baseline if shaping silently degrades.
        canvas.DrawText("[direct] " + TestText, x: 24, y: 160, SKTextAlign.Left, font, paint);

        canvas.Flush();
        grContext.Flush();
    }

    private static void ReportSdlError(string source)
    {
        var ptr = Sdl3.SDL_GetError();
        var msg = ptr == IntPtr.Zero ? "<no error>" : Marshal.PtrToStringUTF8(ptr) ?? "<unreadable>";
        Console.Error.WriteLine($"{source} failed: {msg}");
    }

    // Defensive reference: keeps HarfBuzzSharp from being trimmed away if every direct use
    // happens to be inlined / DCE'd. Cheap; should not be necessary, but cheap.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1823", Justification = "Trim retention.")]
    private static readonly Type _harfBuzzReference = typeof(HarfBuzzSharp.Blob);
}
