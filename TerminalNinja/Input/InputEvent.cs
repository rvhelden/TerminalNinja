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
/// Represents a mouse input event.
/// </summary>
public sealed record MouseEvent(
    int X,
    int Y,
    MouseButton Button,
    MouseAction Action
) : InputEvent;

/// <summary>
/// Represents a terminal resize event.
/// </summary>
public sealed record ResizeEvent(
    int Width,
    int Height
) : InputEvent;
