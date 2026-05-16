using System.Runtime.InteropServices;

namespace SkiaSpike;

/// <summary>
/// Hand-rolled SDL3 P/Invoke surface for the AOT spike. Uses [LibraryImport] (source-generated,
/// AOT-friendly) so no reflection-based marshalling is involved. Only the SDL3 functions the
/// spike actually uses are bound — full bindings will live in the TerminalNinja.Skia project
/// once the spike approach is validated.
/// </summary>
internal static partial class Sdl3
{
    private const string Lib = "SDL3";

    // Init flags (SDL_init.h)
    public const uint SDL_INIT_VIDEO = 0x00000020;
    public const uint SDL_INIT_EVENTS = 0x00004000;

    // Window flags (SDL_video.h)
    public const ulong SDL_WINDOW_OPENGL = 0x0000000000000002UL;
    public const ulong SDL_WINDOW_RESIZABLE = 0x0000000000000020UL;
    public const ulong SDL_WINDOW_HIGH_PIXEL_DENSITY = 0x0000000000002000UL;

    // GL attributes (SDL_video.h)
    public const int SDL_GL_RED_SIZE = 0;
    public const int SDL_GL_GREEN_SIZE = 1;
    public const int SDL_GL_BLUE_SIZE = 2;
    public const int SDL_GL_ALPHA_SIZE = 3;
    public const int SDL_GL_DEPTH_SIZE = 6;
    public const int SDL_GL_STENCIL_SIZE = 7;
    public const int SDL_GL_DOUBLEBUFFER = 5;
    public const int SDL_GL_CONTEXT_MAJOR_VERSION = 17;
    public const int SDL_GL_CONTEXT_MINOR_VERSION = 18;

    // Event types (SDL_events.h)
    public const uint SDL_EVENT_QUIT = 0x100;
    public const uint SDL_EVENT_KEY_DOWN = 0x300;

    // Common keycodes used by the spike (SDL_keycode.h)
    public const uint SDLK_ESCAPE = 0x1B;

    [LibraryImport(Lib)]
    public static partial int SDL_Init(uint flags);

    [LibraryImport(Lib)]
    public static partial void SDL_Quit();

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr SDL_GetError();

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr SDL_CreateWindow(string title, int w, int h, ulong flags);

    [LibraryImport(Lib)]
    public static partial void SDL_DestroyWindow(IntPtr window);

    [LibraryImport(Lib)]
    public static partial int SDL_GetWindowSizeInPixels(IntPtr window, out int w, out int h);

    [LibraryImport(Lib)]
    public static partial int SDL_GL_SetAttribute(int attr, int value);

    [LibraryImport(Lib)]
    public static partial IntPtr SDL_GL_CreateContext(IntPtr window);

    [LibraryImport(Lib)]
    public static partial int SDL_GL_DestroyContext(IntPtr context);

    [LibraryImport(Lib)]
    public static partial int SDL_GL_MakeCurrent(IntPtr window, IntPtr context);

    [LibraryImport(Lib)]
    public static partial int SDL_GL_SwapWindow(IntPtr window);

    [LibraryImport(Lib)]
    public static partial int SDL_GL_SetSwapInterval(int interval);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr SDL_GL_GetProcAddress(string proc);

    [LibraryImport(Lib)]
    public static partial int SDL_PollEvent(out SDL_Event evt);

    /// <summary>
    /// Common SDL3 event header. Full event payloads vary by type; the spike only needs the
    /// discriminator and the keycode for the key-down case, so we read just enough.
    /// SDL_Event is a union — the largest variant is 128 bytes. We carry the type field
    /// plus a fixed buffer that's large enough to hold any variant SDL3 might write.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 128)]
    public struct SDL_Event
    {
        public uint type;
        // padding to 128 bytes covers any event variant SDL3 writes;
        // the spike only inspects `type` and the key event payload below.
    }

    /// <summary>
    /// SDL_KeyboardEvent layout (SDL_events.h). Cast an <see cref="SDL_Event"/> via
    /// <see cref="MemoryMarshal.AsRef{T}"/> when type == SDL_EVENT_KEY_DOWN.
    /// Field order matches SDL3 3.2.x: type, reserved, timestamp, windowID, which, scancode, key, mod, raw, down, repeat.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SDL_KeyboardEvent
    {
        public uint type;
        public uint reserved;
        public ulong timestamp;
        public uint windowID;
        public uint which;
        public int scancode;
        public uint key;
        public ushort mod;
        public ushort raw;
        [MarshalAs(UnmanagedType.U1)] public bool down;
        [MarshalAs(UnmanagedType.U1)] public bool repeat;
    }
}
