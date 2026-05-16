using System.Runtime.CompilerServices;
using System.Text;
using TerminalNinja.Primitives;
using TerminalNinja.Rendering;

namespace TerminalNinja.Ansi;

/// <summary>
/// Zero-allocation ANSI escape sequence writer that writes directly to a stream.
/// Uses C# 13's \e escape sequence for clean, safe code.
/// </summary>
public sealed class AnsiWriter : ICellSink
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
        if (_cursorX == x && _cursorY == y)
        {
            return;
        }

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
        if (!_currentStyle.NeedsForeground(color))
        {
            return;
        }

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
        if (!_currentStyle.NeedsBackground(color))
        {
            return;
        }

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
    /// Sets text decorations by emitting only the changed SGR codes.
    /// Uses diff-based emission: only decorations that differ from the current state produce escape sequences.
    /// </summary>
    public void SetDecorations(TextDecorations decorations)
    {
        if (!_currentStyle.NeedsDecorations(decorations))
        {
            return;
        }

        var current = _currentStyle.DecorationsSet ? _currentStyle.Decorations : TextDecorations.None;
        var diff = current ^ decorations;
        
        if (diff == TextDecorations.None)
        {
            _currentStyle.DecorationsSet = true;
            return;
        }
        
        // Bold and Dim share the same "off" code (\e[22m), so handle them together.
        // If either changed, we need to consider both.
        if ((diff & (TextDecorations.Bold | TextDecorations.Dim)) != 0)
        {
            var wantBold = (decorations & TextDecorations.Bold) != 0;
            var wantDim = (decorations & TextDecorations.Dim) != 0;
            var hadBold = (current & TextDecorations.Bold) != 0;
            var hadDim = (current & TextDecorations.Dim) != 0;
            
            // If turning off either bold or dim, we must emit \e[22m (resets both),
            // then re-enable whichever one we still want.
            if ((hadBold && !wantBold) || (hadDim && !wantDim))
            {
                WriteSpan(AnsiCodes.BoldOff); // \e[22m resets both
                if (wantBold)
                {
                    WriteSpan(AnsiCodes.BoldOn);
                }

                if (wantDim)
                {
                    WriteSpan(AnsiCodes.DimOn);
                }
            }
            else
            {
                // Turning on — just emit the "on" codes
                if (wantBold && !hadBold)
                {
                    WriteSpan(AnsiCodes.BoldOn);
                }

                if (wantDim && !hadDim)
                {
                    WriteSpan(AnsiCodes.DimOn);
                }
            }
        }
        
        if ((diff & TextDecorations.Italic) != 0)
        {
            WriteSpan((decorations & TextDecorations.Italic) != 0 ? AnsiCodes.ItalicOn : AnsiCodes.ItalicOff);
        }

        if ((diff & TextDecorations.Underline) != 0)
        {
            WriteSpan((decorations & TextDecorations.Underline) != 0 ? AnsiCodes.UnderlineOn : AnsiCodes.UnderlineOff);
        }

        if ((diff & TextDecorations.Blink) != 0)
        {
            WriteSpan((decorations & TextDecorations.Blink) != 0 ? AnsiCodes.BlinkOn : AnsiCodes.BlinkOff);
        }

        if ((diff & TextDecorations.Inverse) != 0)
        {
            WriteSpan((decorations & TextDecorations.Inverse) != 0 ? AnsiCodes.InverseOn : AnsiCodes.InverseOff);
        }

        if ((diff & TextDecorations.Strikethrough) != 0)
        {
            WriteSpan((decorations & TextDecorations.Strikethrough) != 0 ? AnsiCodes.StrikethroughOn : AnsiCodes.StrikethroughOff);
        }

        _currentStyle.Decorations = decorations;
        _currentStyle.DecorationsSet = true;
    }
    
    /// <summary>
    /// Writes a single BMP character to the output (UTF-8 encoded).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteChar(char c) => WriteCodepoint(c);

    /// <summary>
    /// Writes a Unicode scalar value to the output, UTF-8 encoded (1–4 bytes).
    /// Advances the tracked cursor by 2 for wide East Asian / emoji codepoints, otherwise by 1.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteCodepoint(uint codepoint)
    {
        // Fast path for ASCII (the vast majority of terminal output)
        if (codepoint < 128)
        {
            EnsureCapacity(1);
            _buffer[_position++] = (byte)codepoint;
            _cursorX++;
            return;
        }

        if (Rune.TryCreate(codepoint, out var rune))
        {
            Span<byte> bytes = stackalloc byte[4];
            var written = rune.EncodeToUtf8(bytes);
            EnsureCapacity(written);
            bytes[..written].CopyTo(_buffer.AsSpan(_position));
            _position += written;
            _cursorX += WidthTable.IsWide(codepoint) ? 2 : 1;
        }
        // Malformed codepoint (surrogates, > U+10FFFF): silently drop, matching
        // the previous char-only behavior. Cursor does not advance.
    }

    /// <summary>
    /// Writes a complete cell (position, colors, decorations, and codepoint) in one operation.
    /// Skips trailing wide cells (their leading cell already emitted the codepoint).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteCell(int x, int y, Cell cell)
    {
        // WideTrail cells are placeholders; the WideLead cell at (x-1, y) carried the codepoint
        // and advanced the cursor by 2. Emitting anything here would corrupt cursor tracking.
        if ((cell.Flags & CellFlags.WideTrail) != 0)
        {
            return;
        }

        MoveTo(x, y);
        SetForeground(cell.Foreground);
        SetBackground(cell.Background);
        SetDecorations(cell.Decorations);
        WriteCodepoint(cell.Codepoint);
    }

    /// <inheritdoc />
    public void BeginFrame() => ResetCursorTracking();

    /// <inheritdoc />
    public void EndFrame() => Flush();

    /// <inheritdoc />
    public void Resize(int width, int height)
    {
        Reset();
        ClearScreen();
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
    /// Invalidates the internal cursor position tracking without emitting any ANSI codes.
    /// Call this at the start of each frame to ensure the first cell always emits
    /// an absolute cursor position, preventing stale position state from causing
    /// MoveTo() to be incorrectly skipped on delta renders.
    /// </summary>
    public void ResetCursorTracking()
    {
        _cursorX = -1;
        _cursorY = -1;
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
    /// Clears the screen and moves cursor to home position (0,0).
    /// </summary>
    public void ClearScreen()
    {
        WriteSpan(AnsiCodes.ClearScreenAndHome);
        _cursorX = 0;
        _cursorY = 0;
    }
    
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
