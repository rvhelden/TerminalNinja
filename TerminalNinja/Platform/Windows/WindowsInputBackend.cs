using System.Runtime.InteropServices;

namespace TerminalNinja.Platform.Windows;

/// <summary>
/// Windows-native input backend using ReadConsoleInput API.
/// Provides keyboard, mouse, and window resize events.
/// </summary>
public sealed class WindowsInputBackend : Input.IInputBackend
{
    private readonly IntPtr _inputHandle;
    private readonly uint _originalInputMode;
    private bool _disposed;
    private bool _mouseTrackingEnabled;

    public WindowsInputBackend()
    {
        // Get input handle
        _inputHandle = Kernel32.GetStdHandle(Kernel32.STD_INPUT_HANDLE);
        if (_inputHandle == IntPtr.Zero || _inputHandle == new IntPtr(-1))
        {
            throw new InvalidOperationException("Failed to get console input handle.");
        }

        // Save original input mode
        if (!Kernel32.GetConsoleMode(_inputHandle, out _originalInputMode))
        {
            throw new InvalidOperationException($"Failed to get console input mode. Error: {Marshal.GetLastWin32Error()}");
        }

        // Configure input mode for raw input
        uint inputMode = _originalInputMode;

        // Disable line input, echo, and processing for raw input
        inputMode &= ~Kernel32.ENABLE_LINE_INPUT;
        inputMode &= ~Kernel32.ENABLE_ECHO_INPUT;
        inputMode &= ~Kernel32.ENABLE_PROCESSED_INPUT;

        // Disable quick edit mode (allows mouse events)
        inputMode &= ~Kernel32.ENABLE_QUICK_EDIT_MODE;
        inputMode |= Kernel32.ENABLE_EXTENDED_FLAGS;

        // Enable window resize events
        inputMode |= Kernel32.ENABLE_WINDOW_INPUT;

        if (!Kernel32.SetConsoleMode(_inputHandle, inputMode))
        {
            throw new InvalidOperationException($"Failed to set console input mode. Error: {Marshal.GetLastWin32Error()}");
        }
    }

    public IReadOnlyList<Input.InputEvent>? TryRead()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Check if there are pending events
        if (!Kernel32.GetNumberOfConsoleInputEvents(_inputHandle, out uint count))
        {
            return null;
        }

        if (count == 0)
        {
            return null;
        }

        // Peek at events
        var buffer = new Kernel32.INPUT_RECORD[Math.Min(count, 128)];
        if (!Kernel32.PeekConsoleInput(_inputHandle, buffer, (uint)buffer.Length, out uint eventsRead))
        {
            return null;
        }

        if (eventsRead == 0)
        {
            return null;
        }

        // Now read them (remove from buffer)
        if (!Kernel32.ReadConsoleInput(_inputHandle, buffer, eventsRead, out uint actualRead))
        {
            return null;
        }

        return ConvertInputRecords(buffer, (int)actualRead);
    }

    public IReadOnlyList<Input.InputEvent> Read()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var buffer = new Kernel32.INPUT_RECORD[128];

        if (!Kernel32.ReadConsoleInput(_inputHandle, buffer, (uint)buffer.Length, out uint eventsRead))
        {
            throw new InvalidOperationException($"ReadConsoleInput failed. Error: {Marshal.GetLastWin32Error()}");
        }

        return ConvertInputRecords(buffer, (int)eventsRead);
    }

    public void EnableMouseTracking()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_mouseTrackingEnabled)
            return;

        if (!Kernel32.GetConsoleMode(_inputHandle, out uint currentMode))
        {
            throw new InvalidOperationException($"Failed to get console mode. Error: {Marshal.GetLastWin32Error()}");
        }

        currentMode |= Kernel32.ENABLE_MOUSE_INPUT;

        if (!Kernel32.SetConsoleMode(_inputHandle, currentMode))
        {
            throw new InvalidOperationException($"Failed to enable mouse tracking. Error: {Marshal.GetLastWin32Error()}");
        }

        _mouseTrackingEnabled = true;
    }

    public void DisableMouseTracking()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_mouseTrackingEnabled)
            return;

        if (!Kernel32.GetConsoleMode(_inputHandle, out uint currentMode))
        {
            throw new InvalidOperationException($"Failed to get console mode. Error: {Marshal.GetLastWin32Error()}");
        }

        currentMode &= ~Kernel32.ENABLE_MOUSE_INPUT;

        if (!Kernel32.SetConsoleMode(_inputHandle, currentMode))
        {
            throw new InvalidOperationException($"Failed to disable mouse tracking. Error: {Marshal.GetLastWin32Error()}");
        }

        _mouseTrackingEnabled = false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        // Restore original input mode
        Kernel32.SetConsoleMode(_inputHandle, _originalInputMode);

        _disposed = true;
    }

    private List<Input.InputEvent> ConvertInputRecords(Kernel32.INPUT_RECORD[] records, int count)
    {
        var events = new List<Input.InputEvent>(count);

        for (int i = 0; i < count; i++)
        {
            var record = records[i];

            switch (record.EventType)
            {
                case Kernel32.KEY_EVENT:
                    var keyEvent = ConvertKeyEvent(record.KeyEvent);
                    if (keyEvent != null)
                        events.Add(keyEvent);
                    break;

                case Kernel32.MOUSE_EVENT:
                    if (_mouseTrackingEnabled)
                    {
                        var mouseEvent = ConvertMouseEvent(record.MouseEvent);
                        if (mouseEvent != null)
                            events.Add(mouseEvent);
                    }
                    break;

                case Kernel32.WINDOW_BUFFER_SIZE_EVENT:
                    var resizeEvent = ConvertResizeEvent(record.WindowBufferSizeEvent);
                    events.Add(resizeEvent);
                    break;

                case Kernel32.FOCUS_EVENT:
                case Kernel32.MENU_EVENT:
                    // Ignore these internal events
                    break;
            }
        }

        return events;
    }

    private Input.KeyEvent? ConvertKeyEvent(Kernel32.KEY_EVENT_RECORD keyRecord)
    {
        // Only process key down events (ignore key up)
        if (!keyRecord.bKeyDown)
            return null;

        // Skip if no character and no special key
        if (keyRecord.UnicodeChar == '\0' && keyRecord.wVirtualKeyCode == 0)
            return null;

        var key = (ConsoleKey)keyRecord.wVirtualKeyCode;
        var keyChar = keyRecord.UnicodeChar;
        var state = keyRecord.dwControlKeyState;

        var shift = state.HasFlag(Kernel32.ControlKeyState.SHIFT_PRESSED);
        var alt = state.HasFlag(Kernel32.ControlKeyState.LEFT_ALT_PRESSED) ||
                  state.HasFlag(Kernel32.ControlKeyState.RIGHT_ALT_PRESSED);
        var ctrl = state.HasFlag(Kernel32.ControlKeyState.LEFT_CTRL_PRESSED) ||
                   state.HasFlag(Kernel32.ControlKeyState.RIGHT_CTRL_PRESSED);

        return new Input.KeyEvent(key, keyChar, shift, alt, ctrl);
    }

    private Input.MouseEvent? ConvertMouseEvent(Kernel32.MOUSE_EVENT_RECORD mouseRecord)
    {
        var x = mouseRecord.dwMousePosition.X;
        var y = mouseRecord.dwMousePosition.Y;

        // Handle mouse wheel events
        if (mouseRecord.dwEventFlags.HasFlag(Kernel32.MouseEventFlags.MOUSE_WHEELED))
        {
            // High word of dwButtonState contains wheel delta
            int delta = (int)mouseRecord.dwButtonState >> 16;
            var action = delta > 0 ? Input.MouseAction.ScrollUp : Input.MouseAction.ScrollDown;
            return new Input.MouseEvent(x, y, Input.MouseButton.None, action);
        }

        if (mouseRecord.dwEventFlags.HasFlag(Kernel32.MouseEventFlags.MOUSE_HWHEELED))
        {
            // Horizontal wheel - we don't have a specific action for this yet, skip
            return null;
        }

        // Handle mouse move
        if (mouseRecord.dwEventFlags.HasFlag(Kernel32.MouseEventFlags.MOUSE_MOVED))
        {
            var button = ConvertMouseButton(mouseRecord.dwButtonState);
            return new Input.MouseEvent(x, y, button, Input.MouseAction.Move);
        }

        // Handle button press/release
        if (mouseRecord.dwEventFlags == Kernel32.MouseEventFlags.None)
        {
            var button = ConvertMouseButton(mouseRecord.dwButtonState);
            var action = mouseRecord.dwButtonState != Kernel32.MouseButtonState.None
                ? Input.MouseAction.Press
                : Input.MouseAction.Release;

            return new Input.MouseEvent(x, y, button, action);
        }

        // Handle double click
        if (mouseRecord.dwEventFlags.HasFlag(Kernel32.MouseEventFlags.DOUBLE_CLICK))
        {
            var button = ConvertMouseButton(mouseRecord.dwButtonState);
            return new Input.MouseEvent(x, y, button, Input.MouseAction.Press);
        }

        return null;
    }

    private Input.ResizeEvent ConvertResizeEvent(Kernel32.WINDOW_BUFFER_SIZE_RECORD resizeRecord)
    {
        return new Input.ResizeEvent(resizeRecord.dwSize.X, resizeRecord.dwSize.Y);
    }

    private Input.MouseButton ConvertMouseButton(Kernel32.MouseButtonState state)
    {
        if (state.HasFlag(Kernel32.MouseButtonState.FROM_LEFT_1ST_BUTTON_PRESSED))
            return Input.MouseButton.Left;

        if (state.HasFlag(Kernel32.MouseButtonState.RIGHTMOST_BUTTON_PRESSED))
            return Input.MouseButton.Right;

        if (state.HasFlag(Kernel32.MouseButtonState.FROM_LEFT_2ND_BUTTON_PRESSED))
            return Input.MouseButton.Middle;

        return Input.MouseButton.None;
    }
}
