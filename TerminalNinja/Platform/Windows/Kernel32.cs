using System.Runtime.InteropServices;

namespace TerminalNinja.Platform.Windows;

/// <summary>
/// Windows Console API P/Invoke declarations, structures, and constants.
/// </summary>
public static class Kernel32
{
    #region Standard Handles

    public const int STD_INPUT_HANDLE = -10;
    public const int STD_OUTPUT_HANDLE = -11;
    public const int STD_ERROR_HANDLE = -12;

    #endregion

    #region Console Mode Flags

    // Input mode flags
    public const uint ENABLE_PROCESSED_INPUT = 0x0001;
    public const uint ENABLE_LINE_INPUT = 0x0002;
    public const uint ENABLE_ECHO_INPUT = 0x0004;
    public const uint ENABLE_WINDOW_INPUT = 0x0008;
    public const uint ENABLE_MOUSE_INPUT = 0x0010;
    public const uint ENABLE_INSERT_MODE = 0x0020;
    public const uint ENABLE_QUICK_EDIT_MODE = 0x0040;
    public const uint ENABLE_EXTENDED_FLAGS = 0x0080;
    public const uint ENABLE_AUTO_POSITION = 0x0100;
    public const uint ENABLE_VIRTUAL_TERMINAL_INPUT = 0x0200;

    // Output mode flags
    public const uint ENABLE_PROCESSED_OUTPUT = 0x0001;
    public const uint ENABLE_WRAP_AT_EOL_OUTPUT = 0x0002;
    public const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
    public const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;
    public const uint ENABLE_LVB_GRID_WORLDWIDE = 0x0010;

    #endregion

    #region Event Type Constants

    public const ushort KEY_EVENT = 0x0001;
    public const ushort MOUSE_EVENT = 0x0002;
    public const ushort WINDOW_BUFFER_SIZE_EVENT = 0x0004;
    public const ushort MENU_EVENT = 0x0008;
    public const ushort FOCUS_EVENT = 0x0010;

    #endregion

    #region Control Key State Flags

    [Flags]
    public enum ControlKeyState : uint
    {
        None = 0,
        RIGHT_ALT_PRESSED = 0x0001,
        LEFT_ALT_PRESSED = 0x0002,
        RIGHT_CTRL_PRESSED = 0x0004,
        LEFT_CTRL_PRESSED = 0x0008,
        SHIFT_PRESSED = 0x0010,
        NUMLOCK_ON = 0x0020,
        SCROLLLOCK_ON = 0x0040,
        CAPSLOCK_ON = 0x0080,
        ENHANCED_KEY = 0x0100
    }

    #endregion

    #region Mouse Button State Flags

    [Flags]
    public enum MouseButtonState : uint
    {
        None = 0,
        FROM_LEFT_1ST_BUTTON_PRESSED = 0x0001,
        RIGHTMOST_BUTTON_PRESSED = 0x0002,
        FROM_LEFT_2ND_BUTTON_PRESSED = 0x0004,
        FROM_LEFT_3RD_BUTTON_PRESSED = 0x0008,
        FROM_LEFT_4TH_BUTTON_PRESSED = 0x0010
    }

    #endregion

    #region Mouse Event Flags

    [Flags]
    public enum MouseEventFlags : uint
    {
        None = 0,
        MOUSE_MOVED = 0x0001,
        DOUBLE_CLICK = 0x0002,
        MOUSE_WHEELED = 0x0004,
        MOUSE_HWHEELED = 0x0008
    }

    #endregion

    #region Structures

    /// <summary>
    /// Console screen buffer coordinate.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct COORD
    {
        public short X;
        public short Y;

        public COORD(short x, short y)
        {
            X = x;
            Y = y;
        }

        public override string ToString() => $"({X}, {Y})";
    }

    /// <summary>
    /// Describes a keyboard input event.
    /// Uses explicit layout to handle the union of UnicodeChar/AsciiChar.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
    public struct KEY_EVENT_RECORD
    {
        /// <summary>
        /// TRUE if the key is pressed, FALSE if released.
        /// </summary>
        [FieldOffset(0)]
        [MarshalAs(UnmanagedType.Bool)]
        public bool bKeyDown;

        /// <summary>
        /// Repeat count (key held down).
        /// </summary>
        [FieldOffset(4)]
        public ushort wRepeatCount;

        /// <summary>
        /// Virtual-key code.
        /// </summary>
        [FieldOffset(6)]
        public ushort wVirtualKeyCode;

        /// <summary>
        /// Virtual scan code (device-dependent).
        /// </summary>
        [FieldOffset(8)]
        public ushort wVirtualScanCode;

        /// <summary>
        /// Unicode character (in uChar union).
        /// </summary>
        [FieldOffset(10)]
        public char UnicodeChar;

        /// <summary>
        /// ASCII character (in uChar union - overlaps UnicodeChar).
        /// Use UnicodeChar instead for modern applications.
        /// </summary>
        [FieldOffset(10)]
        public byte AsciiChar;

        /// <summary>
        /// State of control keys.
        /// </summary>
        [FieldOffset(12)]
        public ControlKeyState dwControlKeyState;

        public override string ToString()
        {
            var keyChar = UnicodeChar != '\0' ? UnicodeChar.ToString() : "(none)";
            return $"Key: VK={wVirtualKeyCode:X4}, Char='{keyChar}', Down={bKeyDown}, Ctrl={dwControlKeyState}";
        }
    }

    /// <summary>
    /// Describes a mouse input event.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct MOUSE_EVENT_RECORD
    {
        /// <summary>
        /// Cursor position in character cells.
        /// </summary>
        [FieldOffset(0)]
        public COORD dwMousePosition;

        /// <summary>
        /// Status of mouse buttons.
        /// </summary>
        [FieldOffset(4)]
        public MouseButtonState dwButtonState;

        /// <summary>
        /// State of control keys.
        /// </summary>
        [FieldOffset(8)]
        public ControlKeyState dwControlKeyState;

        /// <summary>
        /// Type of mouse event (move, click, wheel).
        /// </summary>
        [FieldOffset(12)]
        public MouseEventFlags dwEventFlags;

        public override string ToString()
        {
            return $"Mouse: Pos={dwMousePosition}, Buttons={dwButtonState}, Flags={dwEventFlags}";
        }
    }

    /// <summary>
    /// Describes a change in console screen buffer size.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOW_BUFFER_SIZE_RECORD
    {
        /// <summary>
        /// New size of the console screen buffer.
        /// </summary>
        public COORD dwSize;

        public override string ToString() => $"WindowSize: {dwSize}";
    }

    /// <summary>
    /// Menu event record (used internally, should be ignored).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MENU_EVENT_RECORD
    {
        public uint dwCommandId;
    }

    /// <summary>
    /// Focus event record (used internally, should be ignored).
    /// NOTE: Using uint instead of bool to avoid marshalling issues.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct FOCUS_EVENT_RECORD
    {
        public uint bSetFocus; // Use uint, not bool - prevents alignment issues
    }

    /// <summary>
    /// Describes an input event in the console input buffer.
    /// Uses explicit layout to simulate a C union for the Event member.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct INPUT_RECORD
    {
        /// <summary>
        /// Type of event (KEY_EVENT, MOUSE_EVENT, etc.)
        /// </summary>
        [FieldOffset(0)]
        public ushort EventType;

        // All event types start at offset 4 (after EventType + 2 bytes padding)
        // This simulates the C union behavior

        [FieldOffset(4)]
        public KEY_EVENT_RECORD KeyEvent;

        [FieldOffset(4)]
        public MOUSE_EVENT_RECORD MouseEvent;

        [FieldOffset(4)]
        public WINDOW_BUFFER_SIZE_RECORD WindowBufferSizeEvent;

        [FieldOffset(4)]
        public MENU_EVENT_RECORD MenuEvent;

        [FieldOffset(4)]
        public FOCUS_EVENT_RECORD FocusEvent;

        public override string ToString()
        {
            return EventType switch
            {
                KEY_EVENT => KeyEvent.ToString(),
                MOUSE_EVENT => MouseEvent.ToString(),
                WINDOW_BUFFER_SIZE_EVENT => WindowBufferSizeEvent.ToString(),
                MENU_EVENT => $"Menu: CommandId={MenuEvent.dwCommandId}",
                FOCUS_EVENT => $"Focus: SetFocus={FocusEvent.bSetFocus}",
                _ => $"Unknown EventType: {EventType}"
            };
        }
    }

    #endregion

    #region P/Invoke Methods

    /// <summary>
    /// Retrieves a handle to the specified standard device.
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetStdHandle(int nStdHandle);

    /// <summary>
    /// Retrieves the current console mode.
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    /// <summary>
    /// Sets the console mode.
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    /// <summary>
    /// Reads data from a console input buffer and removes it from the buffer.
    /// </summary>
    [DllImport("kernel32.dll", EntryPoint = "ReadConsoleInputW", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool ReadConsoleInput(
        IntPtr hConsoleInput,
        [Out] INPUT_RECORD[] lpBuffer,
        uint nLength,
        out uint lpNumberOfEventsRead);

    /// <summary>
    /// Reads data from a console input buffer without removing it.
    /// </summary>
    [DllImport("kernel32.dll", EntryPoint = "PeekConsoleInputW", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool PeekConsoleInput(
        IntPtr hConsoleInput,
        [Out] INPUT_RECORD[] lpBuffer,
        uint nLength,
        out uint lpNumberOfEventsRead);

    /// <summary>
    /// Retrieves the number of unread input records in the console's input buffer.
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetNumberOfConsoleInputEvents(
        IntPtr hConsoleInput,
        out uint lpcNumberOfEvents);

    /// <summary>
    /// Flushes the console input buffer.
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool FlushConsoleInputBuffer(IntPtr hConsoleInput);

    #endregion
}
