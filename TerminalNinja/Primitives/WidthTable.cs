namespace TerminalNinja.Primitives;

/// <summary>
/// East Asian Width lookup (UAX #11) for terminal cell layout. Returns true for
/// codepoints that should occupy two cell columns (Wide / Fullwidth categories,
/// plus the emoji presentation ranges that terminals universally render as wide).
/// </summary>
/// <remarks>
/// Coverage is intentionally minimal — the ranges below are the ones actually
/// exercised by samples and tests. Extend as needed; do not regenerate from the
/// full UCD without measuring the AOT impact.
/// </remarks>
public static class WidthTable
{
    /// <summary>
    /// Returns <c>true</c> if the codepoint should occupy two terminal cells.
    /// </summary>
    public static bool IsWide(uint codepoint)
    {
        return (codepoint >= 0x1100 && codepoint <= 0x115F)     // Hangul Jamo
            || (codepoint >= 0x2E80 && codepoint <= 0x303E)     // CJK Radicals / Kangxi
            || (codepoint >= 0x3041 && codepoint <= 0x33FF)     // Hiragana / Katakana / CJK Symbols
            || (codepoint >= 0x3400 && codepoint <= 0x4DBF)     // CJK Ext A
            || (codepoint >= 0x4E00 && codepoint <= 0x9FFF)     // CJK Unified Ideographs
            || (codepoint >= 0xA000 && codepoint <= 0xA4CF)     // Yi
            || (codepoint >= 0xAC00 && codepoint <= 0xD7A3)     // Hangul Syllables
            || (codepoint >= 0xF900 && codepoint <= 0xFAFF)     // CJK Compatibility Ideographs
            || (codepoint >= 0xFE30 && codepoint <= 0xFE4F)     // CJK Compatibility Forms
            || (codepoint >= 0xFF00 && codepoint <= 0xFF60)     // Fullwidth ASCII variants
            || (codepoint >= 0xFFE0 && codepoint <= 0xFFE6)     // Fullwidth signs
            || (codepoint >= 0x1F300 && codepoint <= 0x1F64F)   // Misc Symbols & Pictographs, Emoticons
            || (codepoint >= 0x1F680 && codepoint <= 0x1F6FF)   // Transport & Map
            || (codepoint >= 0x1F900 && codepoint <= 0x1F9FF)   // Supplemental Symbols & Pictographs
            || (codepoint >= 0x20000 && codepoint <= 0x2FFFD)   // CJK Ext B-F
            || (codepoint >= 0x30000 && codepoint <= 0x3FFFD);  // CJK Ext G+
    }
}
