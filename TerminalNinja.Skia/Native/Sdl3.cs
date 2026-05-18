using System;
using System.Runtime.InteropServices;

namespace TerminalNinja.Skia.Native;

/// <summary>
/// Hand-rolled SDL3 P/Invoke surface for the GUI host. Uses <see cref="LibraryImportAttribute"/>
/// (source-generated marshalling) so the entire surface is AOT-clean — no reflection-based
/// fallback. Only the SDL3 functions the host actually needs are bound; extend as new
/// features (mouse, IME, clipboard) come online in later steps.
/// </summary>
/// <remarks>
/// We chose SDL3 over Silk.NET 2.x windowing because Silk.NET's platform-discovery layer
/// uses <c>Activator.CreateInstance</c> on attribute-supplied types and its native-asset
/// loader uses <c>Assembly.Location</c> / <c>DependencyContext.Default</c> — neither works
/// in a single-file / Native AOT app. The spike at <c>samples/SkiaSpike</c> documents that
/// before/after comparison (7 trim warnings → 0).
/// </remarks>
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
    public const uint SDL_EVENT_KEY_UP = 0x301;
    public const uint SDL_EVENT_TEXT_INPUT = 0x303;
    public const uint SDL_EVENT_MOUSE_MOTION = 0x400;
    public const uint SDL_EVENT_MOUSE_BUTTON_DOWN = 0x401;
    public const uint SDL_EVENT_MOUSE_BUTTON_UP = 0x402;
    public const uint SDL_EVENT_MOUSE_WHEEL = 0x403;
    // Window event values per SDL3 SDL_events.h (counting from SDL_EVENT_WINDOW_SHOWN = 0x202).
    // Earlier work used 0x204 / 0x205 which actually correspond to EXPOSED and MOVED — the
    // resize path was misfiring on those events instead of the real RESIZED / PIXEL_SIZE_CHANGED.
    public const uint SDL_EVENT_WINDOW_RESIZED = 0x206;
    public const uint SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED = 0x207;
    public const uint SDL_EVENT_WINDOW_DISPLAY_SCALE_CHANGED = 0x214;

    // Key modifiers (SDL_keycode.h)
    public const ushort SDL_KMOD_LSHIFT = 0x0001;
    public const ushort SDL_KMOD_RSHIFT = 0x0002;
    public const ushort SDL_KMOD_LCTRL = 0x0040;
    public const ushort SDL_KMOD_RCTRL = 0x0080;
    public const ushort SDL_KMOD_LALT = 0x0100;
    public const ushort SDL_KMOD_RALT = 0x0200;
    public const ushort SDL_KMOD_SHIFT = SDL_KMOD_LSHIFT | SDL_KMOD_RSHIFT;
    public const ushort SDL_KMOD_CTRL = SDL_KMOD_LCTRL | SDL_KMOD_RCTRL;
    public const ushort SDL_KMOD_ALT = SDL_KMOD_LALT | SDL_KMOD_RALT;

    // Mouse buttons (SDL_mouse.h)
    public const byte SDL_BUTTON_LEFT = 1;
    public const byte SDL_BUTTON_MIDDLE = 2;
    public const byte SDL_BUTTON_RIGHT = 3;

    // Keycodes used by the host (SDL_keycode.h). SDL3 mixes ASCII for printable codepoints
    // with 0x40000000 + scancode for non-printables; we map the ones the controls care about.
    public const uint SDLK_ESCAPE = 0x1B;
    public const uint SDLK_RETURN = 0x0D;
    public const uint SDLK_BACKSPACE = 0x08;
    public const uint SDLK_TAB = 0x09;
    public const uint SDLK_SPACE = 0x20;
    public const uint SDLK_DELETE = 0x7F;
    public const uint SDLK_LEFT = 0x40000050;
    public const uint SDLK_RIGHT = 0x4000004F;
    public const uint SDLK_UP = 0x40000052;
    public const uint SDLK_DOWN = 0x40000051;
    public const uint SDLK_HOME = 0x4000004A;
    public const uint SDLK_END = 0x4000004D;
    public const uint SDLK_PAGEUP = 0x4000004B;
    public const uint SDLK_PAGEDOWN = 0x4000004E;
    public const uint SDLK_INSERT = 0x40000049;
    public const uint SDLK_F1 = 0x4000003A;
    public const uint SDLK_F2 = 0x4000003B;
    public const uint SDLK_F3 = 0x4000003C;
    public const uint SDLK_F4 = 0x4000003D;
    public const uint SDLK_F5 = 0x4000003E;
    public const uint SDLK_F6 = 0x4000003F;
    public const uint SDLK_F7 = 0x40000040;
    public const uint SDLK_F8 = 0x40000041;
    public const uint SDLK_F9 = 0x40000042;
    public const uint SDLK_F10 = 0x40000043;
    public const uint SDLK_F11 = 0x40000044;
    public const uint SDLK_F12 = 0x40000045;

    // SDL3's SDL_Init returns C99 `bool` (1 byte) — must be marshalled as U1, not as int,
    // because the x64 ABI doesn't guarantee the high bytes of the return register are
    // zero-extended when the function returns a bool. Marshalling as int can read garbage
    // upper bytes and make a successful init look like failure.
    [LibraryImport(Lib)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SDL_Init(uint flags);

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
    public static partial int SDL_SetWindowSize(IntPtr window, int w, int h);

    /// <summary>
    /// Returns the content scale of the display the window is on (1.0 = 100% / unscaled,
    /// 1.5 = 150%, 2.0 = 200%, etc.). Used to size cells and fonts so text renders crisply
    /// on HiDPI displays. Returns 1.0 if the value can't be queried.
    /// </summary>
    [LibraryImport(Lib)]
    public static partial float SDL_GetWindowDisplayScale(IntPtr window);

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

    // SDL_PollEvent also returns C99 `bool` in SDL3 — same U1 marshalling requirement.
    [LibraryImport(Lib)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SDL_PollEvent(out SDL_Event evt);

    // SDL3 text input: must be enabled per-window before SDL_EVENT_TEXT_INPUT events flow.
    // Without these, KEY_DOWN's keycode is the only source of typed characters, and SDL3's
    // logical keycode is unreliable for shifted symbols, dead keys, IME-composed text, and
    // non-US layouts. SDL_StartTextInput / SDL_StopTextInput are the canonical SDL3 API for
    // text input.
    [LibraryImport(Lib)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SDL_StartTextInput(IntPtr window);

    [LibraryImport(Lib)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SDL_StopTextInput(IntPtr window);

    /// <summary>
    /// Returns the current modifier-key state bitmask (combination of
    /// <see cref="SDL_KMOD_SHIFT"/> / <see cref="SDL_KMOD_CTRL"/> /
    /// <see cref="SDL_KMOD_ALT"/> etc.). Used to enrich mouse events
    /// with modifier state — SDL3's mouse-event variants don't carry it.
    /// </summary>
    [LibraryImport(Lib)]
    public static partial ushort SDL_GetModState();

    /// <summary>
    /// Returns a pointer to a UTF-8 string with the OS clipboard contents.
    /// Caller must <see cref="SDL_free"/> the result. Returns <see cref="IntPtr.Zero"/>
    /// when the clipboard is empty or unavailable.
    /// </summary>
    [LibraryImport(Lib)]
    public static partial IntPtr SDL_GetClipboardText();

    /// <summary>Replace the OS clipboard contents with the given UTF-8 string.</summary>
    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SDL_SetClipboardText(string text);

    /// <summary>Free memory that SDL allocated (e.g. clipboard text strings).</summary>
    [LibraryImport(Lib)]
    public static partial void SDL_free(IntPtr mem);

    /// <summary>
    /// SDL3 event union. Variants are reinterpreted via <see cref="System.Runtime.CompilerServices.Unsafe.As{TFrom, TTo}(ref TFrom)"/>
    /// once <see cref="type"/> identifies which variant SDL3 wrote.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 128)]
    public struct SDL_Event
    {
        public uint type;
        // Padding to 128 bytes covers any event variant SDL3 writes — see SDL3's SDL_events.h
        // SDL_Event union for the full list. We never marshal beyond `type` directly; readers
        // cast to a specific variant struct.
    }

    /// <summary>
    /// SDL_KeyboardEvent layout (SDL_events.h). Field order matches SDL3 3.2.x:
    /// type, reserved, timestamp, windowID, which, scancode, key, mod, raw, down, repeat.
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

    /// <summary>SDL_MouseMotionEvent layout (SDL_events.h).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SDL_MouseMotionEvent
    {
        public uint type;
        public uint reserved;
        public ulong timestamp;
        public uint windowID;
        public uint which;
        public uint state;
        public float x;
        public float y;
        public float xrel;
        public float yrel;
    }

    /// <summary>SDL_MouseButtonEvent layout (SDL_events.h).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SDL_MouseButtonEvent
    {
        public uint type;
        public uint reserved;
        public ulong timestamp;
        public uint windowID;
        public uint which;
        public byte button;
        [MarshalAs(UnmanagedType.U1)] public bool down;
        public byte clicks;
        public byte padding;
        public float x;
        public float y;
    }

    /// <summary>SDL_MouseWheelEvent layout (SDL_events.h).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SDL_MouseWheelEvent
    {
        public uint type;
        public uint reserved;
        public ulong timestamp;
        public uint windowID;
        public uint which;
        public float x;
        public float y;
        public uint direction;
        public float mouse_x;
        public float mouse_y;
        public int integer_x;
        public int integer_y;
    }

    /// <summary>SDL_WindowEvent layout (SDL_events.h) — covers resize and pixel-size-change.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SDL_WindowEvent
    {
        public uint type;
        public uint reserved;
        public ulong timestamp;
        public uint windowID;
        public int data1;
        public int data2;
    }

    /// <summary>
    /// SDL_TextInputEvent layout (SDL_events.h). The <c>text</c> field is a pointer to a
    /// UTF-8 string that SDL3 owns and invalidates after the next <c>SDL_PollEvent</c>.
    /// Callers must copy the string out before the next poll.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SDL_TextInputEvent
    {
        public uint type;
        public uint reserved;
        public ulong timestamp;
        public uint windowID;
        public IntPtr text;
    }
}
