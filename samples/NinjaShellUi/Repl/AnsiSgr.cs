using TerminalNinja.Primitives;

namespace NinjaShellUi;

/// <summary>
/// Minimal ANSI SGR (Select Graphic Rendition) parser used by the REPL's output
/// drawer. Supports the subset the shell actually emits: reset, bold/dim toggle,
/// basic + bright 8-color fg, 256-color fg, truecolor fg, and the matching
/// default-fg / clear-bold-or-dim reset codes.
/// </summary>
internal static class AnsiSgr
{
    /// <summary>
    /// Apply a single SGR payload — the semicolon-separated numeric codes between
    /// <c>\e[</c> and <c>m</c> — to the current rendering state.
    /// </summary>
    public static void Apply(ReadOnlySpan<char> payload, ref Color fg, ref Color bg,
                             ref TextDecorations deco, Color defaultFg, Color defaultBg)
    {
        Span<int> codes = stackalloc int[16];
        int n = 0;
        int cur = 0;
        bool any = false;
        foreach (var ch in payload)
        {
            if (ch == ';')
            {
                if (n < codes.Length) codes[n++] = any ? cur : 0;
                cur = 0; any = false;
            }
            else if (ch is >= '0' and <= '9')
            {
                cur = cur * 10 + (ch - '0');
                any = true;
            }
        }
        if (n < codes.Length) codes[n++] = any ? cur : 0;

        int k = 0;
        while (k < n)
        {
            int code = codes[k];
            switch (code)
            {
                case 0: fg = defaultFg; bg = defaultBg; deco = TextDecorations.None; k++; break;
                case 1: deco |= TextDecorations.Bold; k++; break;
                case 2: deco |= TextDecorations.Dim; k++; break;
                case 22: deco &= ~(TextDecorations.Bold | TextDecorations.Dim); k++; break;
                case 30: case 31: case 32: case 33: case 34: case 35: case 36: case 37:
                    fg = BasicColor(code - 30); k++; break;
                case 38:
                    if (k + 4 < n && codes[k + 1] == 2)
                    { fg = new Color((byte)codes[k + 2], (byte)codes[k + 3], (byte)codes[k + 4]); k += 5; }
                    else if (k + 2 < n && codes[k + 1] == 5)
                    { fg = BasicColor(codes[k + 2] & 0xF); k += 3; }
                    else k++;
                    break;
                case 39: fg = defaultFg; k++; break;
                case 90: case 91: case 92: case 93: case 94: case 95: case 96: case 97:
                    fg = BrightColor(code - 90); k++; break;
                default: k++; break;
            }
        }
    }

    public static Color BasicColor(int idx) => idx switch
    {
        0 => new Color(0x00, 0x00, 0x00),
        1 => new Color(0xF3, 0x8B, 0xA8),
        2 => new Color(0xA6, 0xE3, 0xA1),
        3 => new Color(0xF9, 0xE2, 0xAF),
        4 => new Color(0x89, 0xB4, 0xFA),
        5 => new Color(0xCB, 0xA6, 0xF7),
        6 => new Color(0x94, 0xE2, 0xD5),
        7 => new Color(0xCD, 0xD6, 0xF4),
        _ => new Color(0xCD, 0xD6, 0xF4),
    };

    public static Color BrightColor(int idx) => idx switch
    {
        0 => new Color(0x58, 0x5B, 0x70),
        1 => new Color(0xF3, 0x8B, 0xA8),
        2 => new Color(0xA6, 0xE3, 0xA1),
        3 => new Color(0xF9, 0xE2, 0xAF),
        4 => new Color(0x89, 0xB4, 0xFA),
        5 => new Color(0xCB, 0xA6, 0xF7),
        6 => new Color(0x94, 0xE2, 0xD5),
        7 => new Color(0xCD, 0xD6, 0xF4),
        _ => new Color(0xCD, 0xD6, 0xF4),
    };
}
