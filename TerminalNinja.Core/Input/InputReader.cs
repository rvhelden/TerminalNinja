using System.Text;

namespace TerminalNinja.Core.Input;

/// <summary>
/// Reads and parses input events from the terminal (keyboard, mouse, resize).
/// </summary>
public sealed class InputReader : IDisposable
{
    private readonly TextReader _input;
    private readonly int _initialWidth;
    private readonly int _initialHeight;
    private bool _mouseTrackingEnabled;
    private readonly char[] _buffer;
    private int _bufferLen;
    
    /// <summary>
    /// Initializes a new InputReader using Console.In.
    /// </summary>
    public InputReader()
        : this(System.Console.In, System.Console.WindowWidth, System.Console.WindowHeight)
    {
    }
    
    /// <summary>
    /// Initializes a new InputReader with the specified input stream and initial dimensions.
    /// </summary>
    /// <param name="input">The input stream to read from.</param>
    /// <param name="initialWidth">Initial terminal width.</param>
    /// <param name="initialHeight">Initial terminal height.</param>
    public InputReader(TextReader input, int initialWidth, int initialHeight)
    {
        _input = input;
        _initialWidth = initialWidth;
        _initialHeight = initialHeight;
        _buffer = new char[256];
        _bufferLen = 0;
    }
    
    /// <summary>
    /// Enables mouse tracking in the terminal.
    /// </summary>
    public void EnableMouseTracking()
    {
        if (_mouseTrackingEnabled) return;
        
        Console.Terminal.EnableMouseTracking();
        _mouseTrackingEnabled = true;
    }
    
    /// <summary>
    /// Disables mouse tracking in the terminal.
    /// </summary>
    public void DisableMouseTracking()
    {
        if (!_mouseTrackingEnabled) return;
        
        Console.Terminal.DisableMouseTracking();
        _mouseTrackingEnabled = false;
    }
    
    /// <summary>
    /// Tries to read an input event without blocking.
    /// Returns null if no input is available.
    /// </summary>
    public InputEvent? TryRead()
    {
        // Check for terminal resize
        var resizeEvent = CheckResize();
        if (resizeEvent != null)
            return resizeEvent;
        
        // Check if keyboard input is available
        if (!System.Console.KeyAvailable)
            return null;
        
        return ReadEvent();
    }
    
    /// <summary>
    /// Reads an input event, blocking until one is available.
    /// </summary>
    public InputEvent Read()
    {
        // Keep checking for resize while waiting
        while (true)
        {
            var resizeEvent = CheckResize();
            if (resizeEvent != null)
                return resizeEvent;
            
            if (System.Console.KeyAvailable)
                return ReadEvent();
            
            Thread.Sleep(10); // Small delay to avoid busy-waiting
        }
    }
    
    /// <summary>
    /// Checks if the terminal has been resized and returns a ResizeEvent if so.
    /// </summary>
    private ResizeEvent? CheckResize()
    {
        try
        {
            var currentWidth = System.Console.WindowWidth;
            var currentHeight = System.Console.WindowHeight;
            
            if (currentWidth != _initialWidth || currentHeight != _initialHeight)
            {
                // Note: We can't update _initialWidth/_initialHeight here because they're readonly
                // The caller should handle the resize and create a new InputReader if needed
                return new ResizeEvent(currentWidth, currentHeight);
            }
        }
        catch
        {
            // Ignore errors reading window size
        }
        
        return null;
    }
    
    /// <summary>
    /// Reads a single input event from the console.
    /// </summary>
    private InputEvent ReadEvent()
    {
        // Peek at the first character
        var keyInfo = System.Console.ReadKey(intercept: true);
        
        // If it's an escape character, we need to read more to determine the sequence
        if (keyInfo.KeyChar == '\x1b')
        {
            // Build the escape sequence in a buffer
            _bufferLen = 0;
            _buffer[_bufferLen++] = '\x1b';
            
            // Read additional characters (with timeout)
            var startTime = Environment.TickCount;
            while (Environment.TickCount - startTime < 50) // 50ms timeout
            {
                if (System.Console.KeyAvailable)
                {
                    var nextKey = System.Console.ReadKey(intercept: true);
                    _buffer[_bufferLen++] = nextKey.KeyChar;
                    
                    if (_bufferLen >= _buffer.Length)
                        break;
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
            
            // Try to parse as mouse event first
            var span = new ReadOnlySpan<char>(_buffer, 0, _bufferLen);
            var mouseEvent = AnsiInputParser.TryParseSgrMouse(span, out var consumed);
            if (mouseEvent != null)
                return mouseEvent;
            
            // Try to parse as keyboard escape sequence
            var keyEvent = AnsiInputParser.TryParseKeyEscape(span, out consumed);
            if (keyEvent != null)
                return keyEvent;
            
            // Fallback: treat as ESC key
            return new KeyEvent(ConsoleKey.Escape, '\x1b', false, false, false);
        }
        
        // Regular key event
        return new KeyEvent(
            keyInfo.Key,
            keyInfo.KeyChar,
            (keyInfo.Modifiers & ConsoleModifiers.Shift) != 0,
            (keyInfo.Modifiers & ConsoleModifiers.Alt) != 0,
            (keyInfo.Modifiers & ConsoleModifiers.Control) != 0
        );
    }
    
    /// <summary>
    /// Disposes resources and disables mouse tracking.
    /// </summary>
    public void Dispose()
    {
        DisableMouseTracking();
    }
}
