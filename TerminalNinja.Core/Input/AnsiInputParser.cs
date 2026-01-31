using System.Text;

namespace TerminalNinja.Core.Input;

/// <summary>
/// Parses ANSI escape sequences from terminal input into structured events.
/// </summary>
public static class AnsiInputParser
{
    /// <summary>
    /// Tries to parse an SGR mouse event from the given buffer.
    /// Format: ESC[&lt;Cb;Cx;CyM (press) or ESC[&lt;Cb;Cx;Cym (release)
    /// </summary>
    /// <param name="buffer">The input buffer containing the escape sequence.</param>
    /// <param name="bytesConsumed">Number of bytes consumed from the buffer.</param>
    /// <returns>A MouseEvent if successfully parsed, null otherwise.</returns>
    public static MouseEvent? TryParseSgrMouse(ReadOnlySpan<char> buffer, out int bytesConsumed)
    {
        bytesConsumed = 0;
        
        // Minimum: ESC[<0;1;1M = 10 chars
        if (buffer.Length < 10)
            return null;
        
        // Must start with ESC[<
        if (buffer[0] != '\x1b' || buffer[1] != '[' || buffer[2] != '<')
            return null;
        
        var idx = 3;
        
        // Parse button code
        if (!TryParseNumber(buffer, ref idx, out var buttonCode) || idx >= buffer.Length || buffer[idx] != ';')
            return null;
        
        idx++; // Skip semicolon
        
        // Parse X coordinate (1-based)
        if (!TryParseNumber(buffer, ref idx, out var x) || idx >= buffer.Length || buffer[idx] != ';')
            return null;
        
        idx++; // Skip semicolon
        
        // Parse Y coordinate (1-based)
        if (!TryParseNumber(buffer, ref idx, out var y) || idx >= buffer.Length)
            return null;
        
        // Check terminator: 'M' for press, 'm' for release
        var terminator = buffer[idx];
        if (terminator != 'M' && terminator != 'm')
            return null;
        
        bytesConsumed = idx + 1;
        
        // Decode button and action
        var action = terminator == 'M' ? MouseAction.Press : MouseAction.Release;
        var button = MouseButton.None;
        
        // Parse button from button code
        var baseButton = buttonCode & 0x03; // Lower 2 bits
        button = baseButton switch
        {
            0 => MouseButton.Left,
            1 => MouseButton.Middle,
            2 => MouseButton.Right,
            _ => MouseButton.None
        };
        
        // Check for scroll events (button code 64, 65)
        if (buttonCode == 64)
        {
            action = MouseAction.ScrollUp;
            button = MouseButton.None;
        }
        else if (buttonCode == 65)
        {
            action = MouseAction.ScrollDown;
            button = MouseButton.None;
        }
        // Check for mouse move (button code has bit 5 set = 32)
        else if ((buttonCode & 32) != 0)
        {
            action = MouseAction.Move;
        }
        
        // Convert from 1-based to 0-based coordinates
        return new MouseEvent(x - 1, y - 1, button, action);
    }
    
    /// <summary>
    /// Tries to parse a keyboard escape sequence.
    /// Handles arrow keys, function keys, and other special keys.
    /// </summary>
    public static KeyEvent? TryParseKeyEscape(ReadOnlySpan<char> buffer, out int bytesConsumed)
    {
        bytesConsumed = 0;
        
        if (buffer.Length < 1 || buffer[0] != '\x1b')
            return null;
        
        // ESC alone -> Escape key
        if (buffer.Length == 1)
        {
            bytesConsumed = 1;
            return new KeyEvent(ConsoleKey.Escape, '\x1b', false, false, false);
        }
        
        // Double ESC -> Escape key (consume first ESC)
        if (buffer[1] == '\x1b')
        {
            bytesConsumed = 1;
            return new KeyEvent(ConsoleKey.Escape, '\x1b', false, false, false);
        }
        
        // ESC[ sequences (CSI)
        if (buffer[1] == '[' && buffer.Length >= 3)
        {
            var ch = buffer[2];
            ConsoleKey key;
            
            switch (ch)
            {
                case 'A': key = ConsoleKey.UpArrow; break;
                case 'B': key = ConsoleKey.DownArrow; break;
                case 'C': key = ConsoleKey.RightArrow; break;
                case 'D': key = ConsoleKey.LeftArrow; break;
                case 'H': key = ConsoleKey.Home; break;
                case 'F': key = ConsoleKey.End; break;
                default:
                    // Check for numeric codes like ESC[1~ (Home), ESC[2~ (Insert), etc.
                    if (char.IsDigit(ch) && buffer.Length >= 4 && buffer[3] == '~')
                    {
                        key = ch switch
                        {
                            '1' => ConsoleKey.Home,
                            '2' => ConsoleKey.Insert,
                            '3' => ConsoleKey.Delete,
                            '4' => ConsoleKey.End,
                            '5' => ConsoleKey.PageUp,
                            '6' => ConsoleKey.PageDown,
                            _ => ConsoleKey.None
                        };
                        
                        if (key != ConsoleKey.None)
                        {
                            bytesConsumed = 4;
                            return new KeyEvent(key, '\0', false, false, false);
                        }
                    }
                    return null;
            }
            
            bytesConsumed = 3;
            return new KeyEvent(key, '\0', false, false, false);
        }
        
        // ESC O sequences (SS3) - function keys
        if (buffer[1] == 'O' && buffer.Length >= 3)
        {
            var key = buffer[2] switch
            {
                'P' => ConsoleKey.F1,
                'Q' => ConsoleKey.F2,
                'R' => ConsoleKey.F3,
                'S' => ConsoleKey.F4,
                _ => ConsoleKey.None
            };
            
            if (key != ConsoleKey.None)
            {
                bytesConsumed = 3;
                return new KeyEvent(key, '\0', false, false, false);
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Helper to parse a decimal number from the buffer.
    /// </summary>
    private static bool TryParseNumber(ReadOnlySpan<char> buffer, ref int index, out int value)
    {
        value = 0;
        var startIndex = index;
        
        while (index < buffer.Length && char.IsDigit(buffer[index]))
        {
            value = value * 10 + (buffer[index] - '0');
            index++;
        }
        
        return index > startIndex; // At least one digit parsed
    }
}
