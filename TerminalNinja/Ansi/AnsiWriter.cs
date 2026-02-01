using System.Runtime.CompilerServices;
using System.Text;
using TerminalNinja.Primitives;

namespace TerminalNinja.Ansi;

/// <summary>
/// Zero-allocation ANSI escape sequence writer that writes directly to a stream.
/// Uses C# 13's \e escape sequence for clean, safe code.
/// </summary>
public sealed class AnsiWriter : IDisposable
{
    private readonly Stream _output;
    private readonly byte[] _buffer;
    private int _position;
    private AnsiStyle _currentStyle;
    private int _cursorX;
    private int _cursorY;
    
    private const int DefaultBufferSize = 65536;  // 64KB
    
    /// <summary>
    /// Creates a new ANSI writer that writes to the specified stream.
    /// </summary>
    public AnsiWriter(Stream output, int bufferSize = DefaultBufferSize)
    {
        _output = output;
        _buffer = new byte[bufferSize];
        _position = 0;
        _cursorX = -1;
        _cursorY = -1;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int required)
    {
        if (_position + required > _buffer.Length)
        {
            Flush();
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteByte(byte b)
    {
        EnsureCapacity(1);
        _buffer[_position++] = b;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteSpan(ReadOnlySpan<byte> bytes)
    {
        EnsureCapacity(bytes.Length);
        bytes.CopyTo(_buffer.AsSpan(_position));
        _position += bytes.Length;
    }
    
    /// <summary>
    /// Writes an integer without allocation (optimized for RGB values and coordinates).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteInt(int value)
    {
        // Fast path for single digits (very common)
        if (value < 10)
        {
            WriteByte((byte)('0' + value));
            return;
        }
        
        // Fast path for two digits
        if (value < 100)
        {
            WriteByte((byte)('0' + value / 10));
            WriteByte((byte)('0' + value % 10));
            return;
        }
        
        // Fast path for three digits (RGB values 0-255)
        if (value < 256)
        {
            WriteByte((byte)('0' + value / 100));
            WriteByte((byte)('0' + (value / 10) % 10));
            WriteByte((byte)('0' + value % 10));
            return;
        }
        
        // Fallback for larger numbers (terminal coordinates)
        Span<byte> temp = stackalloc byte[10];
        var pos = temp.Length;
        do
        {
            temp[--pos] = (byte)('0' + value % 10);
            value /= 10;
        } while (value > 0);
        
        WriteSpan(temp[pos..]);
    }
    
    /// <summary>
    /// Moves the cursor to the specified position (0-based coordinates).
    /// </summary>
    public void MoveTo(int x, int y)
    {
        // Skip if already at position
        if (_cursorX == x && _cursorY == y) return;
        
        // \e[{row};{col}H  (1-based coordinates for ANSI)
        WriteSpan(AnsiCodes.EscapeStart);
        WriteInt(y + 1);
        WriteByte((byte)';');
        WriteInt(x + 1);
        WriteByte((byte)'H');
        
        _cursorX = x;
        _cursorY = y;
    }
    
    /// <summary>
    /// Sets the foreground (text) color using 24-bit RGB.
    /// </summary>
    public void SetForeground(Color color)
    {
        if (!_currentStyle.NeedsForeground(color)) return;
        
        // \e[38;2;{r};{g};{b}m
        WriteSpan(AnsiCodes.ForegroundPrefix);
        WriteInt(color.R);
        WriteByte((byte)';');
        WriteInt(color.G);
        WriteByte((byte)';');
        WriteInt(color.B);
        WriteByte((byte)'m');
        
        _currentStyle.Foreground = color;
        _currentStyle.ForegroundSet = true;
    }
    
    /// <summary>
    /// Sets the background color using 24-bit RGB.
    /// </summary>
    public void SetBackground(Color color)
    {
        if (!_currentStyle.NeedsBackground(color)) return;
        
        // \e[48;2;{r};{g};{b}m
        WriteSpan(AnsiCodes.BackgroundPrefix);
        WriteInt(color.R);
        WriteByte((byte)';');
        WriteInt(color.G);
        WriteByte((byte)';');
        WriteInt(color.B);
        WriteByte((byte)'m');
        
        _currentStyle.Background = color;
        _currentStyle.BackgroundSet = true;
    }
    
    /// <summary>
    /// Writes a single character to the output.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteChar(char c)
    {
        // Fast path for ASCII (most terminal characters)
        if (c < 128)
        {
            EnsureCapacity(1);
            _buffer[_position++] = (byte)c;
        }
        else
        {
            // UTF-8 encode non-ASCII characters (box drawing, emoji, etc.)
            Span<char> chars = stackalloc char[1] { c };
            Span<byte> bytes = stackalloc byte[4];
            var written = Encoding.UTF8.GetBytes(chars, bytes);
            EnsureCapacity(written);
            bytes[..written].CopyTo(_buffer.AsSpan(_position));
            _position += written;
        }
        
        _cursorX++;
    }
    
    /// <summary>
    /// Writes a complete cell (position, colors, and character) in one operation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteCell(int x, int y, Cell cell)
    {
        MoveTo(x, y);
        SetForeground(cell.Foreground);
        SetBackground(cell.Background);
        WriteChar(cell.Character);
    }
    
    /// <summary>
    /// Resets all text attributes to defaults.
    /// </summary>
    public void Reset()
    {
        WriteSpan(AnsiCodes.Reset);
        _currentStyle.Reset();
    }
    
    /// <summary>
    /// Hides the cursor.
    /// </summary>
    public void HideCursor() => WriteSpan(AnsiCodes.HideCursor);
    
    /// <summary>
    /// Shows the cursor.
    /// </summary>
    public void ShowCursor() => WriteSpan(AnsiCodes.ShowCursor);
    
    /// <summary>
    /// Clears the screen and moves cursor to home.
    /// </summary>
    public void ClearScreen() => WriteSpan(AnsiCodes.ClearScreenAndHome);
    
    /// <summary>
    /// Flushes the internal buffer to the output stream.
    /// </summary>
    public void Flush()
    {
        if (_position > 0)
        {
            _output.Write(_buffer, 0, _position);
            _position = 0;
        }
    }
    
    /// <summary>
    /// Flushes any pending output and disposes resources.
    /// </summary>
    public void Dispose()
    {
        Flush();
    }
}
