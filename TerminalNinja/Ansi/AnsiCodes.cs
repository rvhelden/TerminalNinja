namespace TerminalNinja.Ansi;

/// <summary>
/// Pre-computed ANSI escape sequences using C# 13's \e escape character.
/// </summary>
public static class AnsiCodes
{
    /// <summary>The ESCAPE character (\e is U+001B).</summary>
    public const char Escape = '\e';
    
    /// <summary>Reset all attributes (\e[0m).</summary>
    public static ReadOnlySpan<byte> Reset => "\e[0m"u8;
    
    /// <summary>Clear screen (\e[2J).</summary>
    public static ReadOnlySpan<byte> ClearScreen => "\e[2J"u8;
    
    /// <summary>Clear screen and move cursor to home (\e[2J\e[H).</summary>
    public static ReadOnlySpan<byte> ClearScreenAndHome => "\e[2J\e[H"u8;
    
    /// <summary>Hide cursor (\e[?25l).</summary>
    public static ReadOnlySpan<byte> HideCursor => "\e[?25l"u8;
    
    /// <summary>Show cursor (\e[?25h).</summary>
    public static ReadOnlySpan<byte> ShowCursor => "\e[?25h"u8;
    
    /// <summary>Move cursor to home position (\e[H).</summary>
    public static ReadOnlySpan<byte> Home => "\e[H"u8;
    
    /// <summary>Start of escape sequence (\e[).</summary>
    public static ReadOnlySpan<byte> EscapeStart => "\e["u8;
    
    /// <summary>Foreground color prefix (\e[38;2;).</summary>
    public static ReadOnlySpan<byte> ForegroundPrefix => "\e[38;2;"u8;
    
    /// <summary>Background color prefix (\e[48;2;).</summary>
    public static ReadOnlySpan<byte> BackgroundPrefix => "\e[48;2;"u8;
}
