namespace TerminalNinja.Primitives;

/// <summary>
/// Per-cell rendering flags. Most cells carry <see cref="None"/>; the flags
/// mark cells that participate in multi-cell or multi-codepoint sequences.
/// </summary>
[Flags]
public enum CellFlags : byte
{
    /// <summary>Single-cell, single-codepoint content (the common case).</summary>
    None = 0,

    /// <summary>
    /// Leading cell of a two-cell wide character (East Asian Width = Wide/Fullwidth).
    /// The trailing cell sits at the next column and is flagged <see cref="WideTrail"/>.
    /// </summary>
    WideLead = 1 << 0,

    /// <summary>
    /// Trailing cell of a two-cell wide character. <see cref="Cell.Codepoint"/> is 0;
    /// the renderer must skip this cell — the lead cell's codepoint already advanced
    /// the cursor by two columns.
    /// </summary>
    WideTrail = 1 << 1,

    /// <summary>
    /// The cell stores the first codepoint of a multi-codepoint grapheme cluster
    /// (e.g. decomposed é, ZWJ emoji, Devanagari). The full sequence lives in the
    /// row-side grapheme table on <see cref="Buffers.CellBuffer"/>.
    /// </summary>
    HasGrapheme = 1 << 2,

    /// <summary>
    /// Reserved for shaped-text rendering: marks a cell occupied by a non-initial
    /// glyph of a shaped run (ligature continuation). Unused until the GUI backend lands.
    /// </summary>
    LigatureMid = 1 << 3,
}
