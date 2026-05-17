using System.Runtime.CompilerServices;
using TerminalNinja.Input;
using TerminalNinja.Skia.Native;

namespace TerminalNinja.Skia;

/// <summary>
/// <see cref="IInputBackend"/> backed by SDL3's event queue. Drains <c>SDL_PollEvent</c>
/// on each <see cref="TryRead"/> / <see cref="Read"/> call and converts SDL key, mouse,
/// and window-resize events to TerminalNinja's existing <see cref="InputEvent"/> records
/// so all the existing controls work unchanged in the GUI host.
/// </summary>
/// <remarks>
/// <para>
/// The backend converts mouse pixel coordinates to cell coordinates using the cell metrics
/// supplied at construction time. Resize events emit cell-grid dimensions, not pixels —
/// matching the contract the existing console-side input backends
/// (<c>WindowsInputBackend</c>, <c>UnixInputBackend</c>) follow.
/// </para>
/// <para>
/// SDL3's <c>SDL_PollEvent</c> must be called from the same thread that owns the SDL window.
/// In practice this is the thread driving the host (<see cref="SkiaApplication"/>'s run loop).
/// </para>
/// </remarks>
public sealed class SdlInputBackend : IInputBackend
{
    // Not readonly: hosts that handle display-scale changes call SetCellMetrics to update
    // pixel→cell conversion without disposing and recreating the backend (which would also
    // tear down the QuitRequested / DisplayScaleChanged flag state).
    private int _cellWidth;
    private int _cellHeight;
    private readonly List<InputEvent> _scratch = [];
    private bool _disposed;
    private bool _mouseTrackingEnabled = true;

    /// <summary>
    /// Set when SDL surfaces an <c>SDL_EVENT_QUIT</c> (window close button or OS request).
    /// The host polls this between <see cref="TryRead"/> calls to know when to exit the loop.
    /// </summary>
    public bool QuitRequested { get; private set; }

    private bool _displayScaleChanged;

    /// <summary>
    /// Creates a backend that converts mouse pixel coordinates to cell coordinates using
    /// <paramref name="cellWidth"/> and <paramref name="cellHeight"/>, and resize events
    /// into cell-grid dimensions. SDL must already be initialised by the host.
    /// </summary>
    public SdlInputBackend(int cellWidth, int cellHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellHeight);
        _cellWidth = cellWidth;
        _cellHeight = cellHeight;
    }

    /// <inheritdoc />
    public IReadOnlyList<InputEvent>? TryRead()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _scratch.Clear();
        while (Sdl3.SDL_PollEvent(out var evt) == 1)
        {
            var converted = Convert(evt);
            if (converted is not null)
            {
                _scratch.Add(converted);
            }
        }

        return _scratch.Count == 0 ? null : _scratch;
    }

    /// <inheritdoc />
    /// <remarks>
    /// SDL3 has <c>SDL_WaitEvent</c> for blocking reads, but the GUI host typically drives
    /// at vsync via the render loop, so this just delegates to <see cref="TryRead"/> and
    /// returns the (possibly empty) result. Callers that genuinely need blocking input on
    /// SDL should call <c>SDL_WaitEvent</c> via the native binding directly.
    /// </remarks>
    public IReadOnlyList<InputEvent> Read()
    {
        var events = TryRead();
        return events ?? Array.Empty<InputEvent>();
    }

    /// <inheritdoc />
    public void EnableMouseTracking()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _mouseTrackingEnabled = true;
    }

    /// <inheritdoc />
    public void DisableMouseTracking()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _mouseTrackingEnabled = false;
    }

    /// <summary>
    /// Returns true if a display-scale-changed event has been seen since the last call to
    /// this method, then clears the flag. The host calls this once per frame and rebuilds
    /// scale-dependent resources when it returns true.
    /// </summary>
    public bool ConsumeDisplayScaleChange()
    {
        var was = _displayScaleChanged;
        _displayScaleChanged = false;
        return was;
    }

    /// <summary>Test hook: sets the display-scale-changed flag without needing a real SDL event.</summary>
    internal void SetDisplayScaleChangedForTesting() => _displayScaleChanged = true;

    /// <summary>
    /// Updates the cell metrics used for pixel→cell coordinate conversion in subsequent
    /// mouse events. Used by the host after a display-scale change.
    /// </summary>
    public void SetCellMetrics(int cellWidth, int cellHeight)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellHeight);
        _cellWidth = cellWidth;
        _cellHeight = cellHeight;
    }

    /// <inheritdoc />
    public void Dispose() => _disposed = true;

    private InputEvent? Convert(Sdl3.SDL_Event evt)
    {
        switch (evt.type)
        {
            case Sdl3.SDL_EVENT_KEY_DOWN:
            {
                ref var key = ref Unsafe.As<Sdl3.SDL_Event, Sdl3.SDL_KeyboardEvent>(ref evt);
                return ConvertKey(key);
            }
            case Sdl3.SDL_EVENT_MOUSE_MOTION when _mouseTrackingEnabled:
            {
                ref var mouse = ref Unsafe.As<Sdl3.SDL_Event, Sdl3.SDL_MouseMotionEvent>(ref evt);
                return BuildMouseEvent(mouse.x, mouse.y, MouseButton.None, MouseAction.Move);
            }
            case Sdl3.SDL_EVENT_MOUSE_BUTTON_DOWN when _mouseTrackingEnabled:
            {
                ref var mouse = ref Unsafe.As<Sdl3.SDL_Event, Sdl3.SDL_MouseButtonEvent>(ref evt);
                return BuildMouseEvent(mouse.x, mouse.y, MapButton(mouse.button), MouseAction.Press);
            }
            case Sdl3.SDL_EVENT_MOUSE_BUTTON_UP when _mouseTrackingEnabled:
            {
                ref var mouse = ref Unsafe.As<Sdl3.SDL_Event, Sdl3.SDL_MouseButtonEvent>(ref evt);
                return BuildMouseEvent(mouse.x, mouse.y, MapButton(mouse.button), MouseAction.Release);
            }
            case Sdl3.SDL_EVENT_MOUSE_WHEEL when _mouseTrackingEnabled:
            {
                ref var wheel = ref Unsafe.As<Sdl3.SDL_Event, Sdl3.SDL_MouseWheelEvent>(ref evt);
                var action = wheel.y > 0 ? MouseAction.ScrollUp : MouseAction.ScrollDown;
                return BuildMouseEvent(wheel.mouse_x, wheel.mouse_y, MouseButton.None, action);
            }
            case Sdl3.SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED:
            case Sdl3.SDL_EVENT_WINDOW_RESIZED:
            {
                ref var win = ref Unsafe.As<Sdl3.SDL_Event, Sdl3.SDL_WindowEvent>(ref evt);
                // data1, data2 carry the new size in pixels for PIXEL_SIZE_CHANGED and in
                // logical units for RESIZED. We treat both as pixels here — the host code
                // re-queries the actual framebuffer size before rebuilding the surface, so
                // the values just need to be reasonable cell counts to wake up the renderer.
                var cellsWide = Math.Max(1, win.data1 / _cellWidth);
                var cellsTall = Math.Max(1, win.data2 / _cellHeight);
                return new ResizeEvent(cellsWide, cellsTall);
            }
            case Sdl3.SDL_EVENT_WINDOW_DISPLAY_SCALE_CHANGED:
            {
                // Host-specific handling — set a flag the SkiaApplication polls between
                // input drains. Returning null keeps this out of the InputEvent stream.
                _displayScaleChanged = true;
                return null;
            }
            case Sdl3.SDL_EVENT_QUIT:
            {
                // No direct equivalent in TerminalNinja's InputEvent hierarchy. Flip the
                // QuitRequested flag so the host can break out of its loop after draining
                // the rest of the event queue.
                QuitRequested = true;
                return null;
            }
            default:
                return null;
        }
    }

    private MouseEvent BuildMouseEvent(float pixelX, float pixelY, MouseButton button, MouseAction action)
    {
        var cellX = (int)(pixelX / _cellWidth);
        var cellY = (int)(pixelY / _cellHeight);
        return new MouseEvent(cellX, cellY, button, action);
    }

    internal static MouseButton MapButton(byte sdlButton) => sdlButton switch
    {
        Sdl3.SDL_BUTTON_LEFT => MouseButton.Left,
        Sdl3.SDL_BUTTON_MIDDLE => MouseButton.Middle,
        Sdl3.SDL_BUTTON_RIGHT => MouseButton.Right,
        _ => MouseButton.None,
    };

    private static KeyEvent ConvertKey(in Sdl3.SDL_KeyboardEvent key)
    {
        var shift = (key.mod & Sdl3.SDL_KMOD_SHIFT) != 0;
        var ctrl = (key.mod & Sdl3.SDL_KMOD_CTRL) != 0;
        var alt = (key.mod & Sdl3.SDL_KMOD_ALT) != 0;

        var consoleKey = MapKeycode(key.key);
        var keyChar = key.key < 0x7F ? (char)key.key : '\0';

        // Letters: SDL3 reports lowercase ASCII for unshifted A-Z. Match the existing
        // backends' behavior where KeyChar reflects the case the user typed.
        if (consoleKey >= ConsoleKey.A && consoleKey <= ConsoleKey.Z && !shift)
        {
            keyChar = (char)('a' + (consoleKey - ConsoleKey.A));
        }
        else if (consoleKey >= ConsoleKey.A && consoleKey <= ConsoleKey.Z && shift)
        {
            keyChar = (char)('A' + (consoleKey - ConsoleKey.A));
        }

        return new KeyEvent(consoleKey, keyChar, shift, alt, ctrl);
    }

    /// <summary>
    /// Maps an SDL3 keycode (SDL_keycode.h) to the closest <see cref="ConsoleKey"/> value.
    /// Returns <see cref="ConsoleKey.NoName"/> when no good match exists.
    /// </summary>
    internal static ConsoleKey MapKeycode(uint sdlKey) => sdlKey switch
    {
        Sdl3.SDLK_RETURN => ConsoleKey.Enter,
        Sdl3.SDLK_ESCAPE => ConsoleKey.Escape,
        Sdl3.SDLK_BACKSPACE => ConsoleKey.Backspace,
        Sdl3.SDLK_TAB => ConsoleKey.Tab,
        Sdl3.SDLK_SPACE => ConsoleKey.Spacebar,
        Sdl3.SDLK_DELETE => ConsoleKey.Delete,
        Sdl3.SDLK_LEFT => ConsoleKey.LeftArrow,
        Sdl3.SDLK_RIGHT => ConsoleKey.RightArrow,
        Sdl3.SDLK_UP => ConsoleKey.UpArrow,
        Sdl3.SDLK_DOWN => ConsoleKey.DownArrow,
        Sdl3.SDLK_HOME => ConsoleKey.Home,
        Sdl3.SDLK_END => ConsoleKey.End,
        Sdl3.SDLK_PAGEUP => ConsoleKey.PageUp,
        Sdl3.SDLK_PAGEDOWN => ConsoleKey.PageDown,
        Sdl3.SDLK_INSERT => ConsoleKey.Insert,
        Sdl3.SDLK_F1 => ConsoleKey.F1,
        Sdl3.SDLK_F2 => ConsoleKey.F2,
        Sdl3.SDLK_F3 => ConsoleKey.F3,
        Sdl3.SDLK_F4 => ConsoleKey.F4,
        Sdl3.SDLK_F5 => ConsoleKey.F5,
        Sdl3.SDLK_F6 => ConsoleKey.F6,
        Sdl3.SDLK_F7 => ConsoleKey.F7,
        Sdl3.SDLK_F8 => ConsoleKey.F8,
        Sdl3.SDLK_F9 => ConsoleKey.F9,
        Sdl3.SDLK_F10 => ConsoleKey.F10,
        Sdl3.SDLK_F11 => ConsoleKey.F11,
        Sdl3.SDLK_F12 => ConsoleKey.F12,
        // Lowercase ASCII letters from SDL3 map to ConsoleKey.A..Z (uppercase). Numbers map directly.
        >= 0x61 and <= 0x7A => (ConsoleKey)(sdlKey - 0x20), // 'a'..'z' → ConsoleKey.A..Z
        >= 0x30 and <= 0x39 => (ConsoleKey)sdlKey,           // '0'..'9' → ConsoleKey.D0..D9
        _ => ConsoleKey.NoName,
    };
}
