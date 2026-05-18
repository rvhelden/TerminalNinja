namespace TerminalNinja.Input;

/// <summary>
/// Base type for all input events.
/// </summary>
public abstract record InputEvent;

/// <summary>
/// Represents a keyboard input event.
/// </summary>
public sealed record KeyEvent(
    ConsoleKey Key,
    char KeyChar,
    bool Shift,
    bool Alt,
    bool Ctrl
) : InputEvent
{
    /// <summary>
    /// Returns true if any modifier key (Shift, Alt, Ctrl) is pressed.
    /// </summary>
    public bool HasModifiers => Shift || Alt || Ctrl;
}

/// <summary>
/// Represents a mouse input event. Modifier flags reflect the keyboard state
/// at the moment the event was generated, populated by the input backend
/// (e.g. <c>TerminalNinja.Skia.SdlInputBackend</c> calls <c>SDL_GetModState</c>).
/// All three modifier flags default to <c>false</c> so existing callers that
/// construct events positionally without them keep compiling unchanged.
/// </summary>
public sealed record MouseEvent(
    int X,
    int Y,
    MouseButton Button,
    MouseAction Action,
    bool Shift = false,
    bool Alt = false,
    bool Ctrl = false
) : InputEvent
{
    /// <summary>True when any modifier (Shift / Alt / Ctrl) was held when this event fired.</summary>
    public bool HasModifiers => Shift || Alt || Ctrl;
}

/// <summary>
/// Represents a terminal resize event.
/// </summary>
public sealed record ResizeEvent(
    int Width,
    int Height
) : InputEvent;
