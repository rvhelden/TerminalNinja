using System.Runtime.InteropServices;

namespace TerminalNinja.Core.Console;

/// <summary>
/// Low-level terminal access and ANSI mode configuration.
/// </summary>
public static class Terminal
{
    /// <summary>Gets the current terminal width in columns.</summary>
    public static int Width
    {
        get
        {
            try
            {
                return System.Console.WindowWidth;
            }
            catch
            {
                return 80;  // Default fallback
            }
        }
    }
    
    /// <summary>Gets the current terminal height in rows.</summary>
    public static int Height
    {
        get
        {
            try
            {
                return System.Console.WindowHeight;
            }
            catch
            {
                return 24;  // Default fallback
            }
        }
    }
    
    /// <summary>Opens the standard output stream for direct writing.</summary>
    public static Stream OpenStdout() => System.Console.OpenStandardOutput();
    
    // Windows P/Invoke declarations
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr handle, out uint mode);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr handle, uint mode);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int handle);
    
    private const int STD_OUTPUT_HANDLE = -11;
    private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
    private const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;
    private const uint ENABLE_MOUSE_INPUT  = 0x0010;
    
    private static uint _originalMode;
    private static bool _modeChanged;
    
    /// <summary>
    /// Enables ANSI escape sequence processing.
    /// On Windows, this enables VT100 processing. On Unix, ANSI is already enabled.
    /// </summary>
    /// <returns>True if successful, false otherwise.</returns>
    public static bool EnableAnsiMode()
    {
        // Unix systems have ANSI support by default
        if (!OperatingSystem.IsWindows()) 
            return true;
        
        var handle = GetStdHandle(STD_OUTPUT_HANDLE);
        if (handle == IntPtr.Zero) 
            return false;
        
        if (!GetConsoleMode(handle, out _originalMode)) 
            return false;
        
        var newMode = _originalMode | ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN | ENABLE_MOUSE_INPUT;
        
        if (SetConsoleMode(handle, newMode))
        {
            _modeChanged = true;
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Disables ANSI escape sequence processing and restores original mode.
    /// </summary>
    public static void DisableAnsiMode()
    {
        if (!OperatingSystem.IsWindows() || !_modeChanged) 
            return;
        
        var handle = GetStdHandle(STD_OUTPUT_HANDLE);
        if (handle != IntPtr.Zero)
        {
            SetConsoleMode(handle, _originalMode);
            _modeChanged = false;
        }
    }
    
    /// <summary>
    /// Enables mouse tracking in the terminal.
    /// Sends ANSI escape sequences to enable any-event mouse tracking and SGR extended mode.
    /// </summary>
    public static void EnableMouseTracking()
    {
        var stdout = System.Console.Out;
        
        // Enable any-event tracking (includes hover)
        stdout.Write("\e[?1003h");
        
        // Enable SGR extended mouse mode (supports large terminals)
        stdout.Write("\e[?1006h");
        
        stdout.Flush();
    }
    
    /// <summary>
    /// Disables mouse tracking in the terminal.
    /// </summary>
    public static void DisableMouseTracking()
    {
        var stdout = System.Console.Out;
        
        // Disable any-event tracking
        stdout.Write("\e[?1003l");
        
        // Disable SGR extended mouse mode
        stdout.Write("\e[?1006l");
        
        stdout.Flush();
    }
}
