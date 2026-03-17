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
    
    // ─── Text Decoration SGR Codes ───────────────────────────────────

    /// <summary>Bold on (\e[1m).</summary>
    public static ReadOnlySpan<byte> BoldOn => "\e[1m"u8;
    
    /// <summary>Bold/dim off (\e[22m). Resets both bold and dim.</summary>
    public static ReadOnlySpan<byte> BoldOff => "\e[22m"u8;
    
    /// <summary>Dim/faint on (\e[2m).</summary>
    public static ReadOnlySpan<byte> DimOn => "\e[2m"u8;
    
    /// <summary>Dim off (\e[22m). Same as BoldOff — resets both bold and dim.</summary>
    public static ReadOnlySpan<byte> DimOff => "\e[22m"u8;
    
    /// <summary>Italic on (\e[3m).</summary>
    public static ReadOnlySpan<byte> ItalicOn => "\e[3m"u8;
    
    /// <summary>Italic off (\e[23m).</summary>
    public static ReadOnlySpan<byte> ItalicOff => "\e[23m"u8;
    
    /// <summary>Underline on (\e[4m).</summary>
    public static ReadOnlySpan<byte> UnderlineOn => "\e[4m"u8;
    
    /// <summary>Underline off (\e[24m).</summary>
    public static ReadOnlySpan<byte> UnderlineOff => "\e[24m"u8;
    
    /// <summary>Blink on (\e[5m).</summary>
    public static ReadOnlySpan<byte> BlinkOn => "\e[5m"u8;
    
    /// <summary>Blink off (\e[25m).</summary>
    public static ReadOnlySpan<byte> BlinkOff => "\e[25m"u8;
    
    /// <summary>Inverse/reverse video on (\e[7m).</summary>
    public static ReadOnlySpan<byte> InverseOn => "\e[7m"u8;
    
    /// <summary>Inverse off (\e[27m).</summary>
    public static ReadOnlySpan<byte> InverseOff => "\e[27m"u8;
    
    /// <summary>Strikethrough on (\e[9m).</summary>
    public static ReadOnlySpan<byte> StrikethroughOn => "\e[9m"u8;
    
    /// <summary>Strikethrough off (\e[29m).</summary>
    public static ReadOnlySpan<byte> StrikethroughOff => "\e[29m"u8;
}
