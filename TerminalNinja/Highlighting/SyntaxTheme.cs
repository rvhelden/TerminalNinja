using TerminalNinja.Primitives;

namespace TerminalNinja.Highlighting;

/// <summary>
/// Maps <see cref="SyntaxTokenKind"/> to a <see cref="Color"/>. Themes are immutable —
/// the framework ships a single <see cref="Dark"/> default tuned to the
/// Catppuccin-mocha-ish palette the rest of <c>TerminalNinja</c> uses; apps can derive
/// their own by passing a fresh <see cref="SyntaxTheme"/> to whatever renders the tokens.
/// </summary>
public sealed class SyntaxTheme
{
    private readonly Color[] _byKind;

    /// <summary>Creates a theme where every kind defaults to <paramref name="fallback"/>
    /// unless overridden in <paramref name="overrides"/>.</summary>
    public SyntaxTheme(Color fallback, IReadOnlyDictionary<SyntaxTokenKind, Color>? overrides = null)
    {
        var kindCount = Enum.GetValues<SyntaxTokenKind>().Length;
        _byKind = new Color[kindCount];
        for (var i = 0; i < kindCount; i++) _byKind[i] = fallback;
        if (overrides is not null)
        {
            foreach (var kv in overrides)
            {
                _byKind[(int)kv.Key] = kv.Value;
            }
        }
    }

    /// <summary>Look up the colour for <paramref name="kind"/>; falls back to the theme's
    /// default colour for unrecognised values.</summary>
    public Color GetColor(SyntaxTokenKind kind)
    {
        var idx = (int)kind;
        if ((uint)idx >= (uint)_byKind.Length) return _byKind[0];
        return _byKind[idx];
    }

    /// <summary>The default theme — Catppuccin-mocha-inspired, designed to read against
    /// the <c>Dark</c> theme's <c>#1E1E2E</c> background.</summary>
    public static SyntaxTheme Dark { get; } = new(
        fallback: new Color(0xCD, 0xD6, 0xF4),
        overrides: new Dictionary<SyntaxTokenKind, Color>
        {
            [SyntaxTokenKind.Keyword] = new(0xCB, 0xA6, 0xF7),         // mauve
            [SyntaxTokenKind.Identifier] = new(0xCD, 0xD6, 0xF4),      // text (default-ish)
            [SyntaxTokenKind.StringLiteral] = new(0xA6, 0xE3, 0xA1),   // green
            [SyntaxTokenKind.NumberLiteral] = new(0xFA, 0xB3, 0x87),   // peach
            [SyntaxTokenKind.BoolLiteral] = new(0xFA, 0xB3, 0x87),     // peach (like numbers)
            [SyntaxTokenKind.Comment] = new(0x6C, 0x70, 0x86),         // overlay0 (dim)
            [SyntaxTokenKind.Punctuation] = new(0x9C, 0xA0, 0xB0),     // subtext0
            [SyntaxTokenKind.Operator] = new(0x89, 0xDC, 0xEB),        // sky
            [SyntaxTokenKind.ModuleName] = new(0xF9, 0xE2, 0xAF),      // yellow
            [SyntaxTokenKind.TypeName] = new(0xF9, 0xE2, 0xAF),        // yellow
            [SyntaxTokenKind.AttributeName] = new(0xF5, 0xC2, 0xE7),   // pink
            [SyntaxTokenKind.AttributeValue] = new(0xA6, 0xE3, 0xA1),  // green (like strings)
            [SyntaxTokenKind.Tag] = new(0xF3, 0x8B, 0xA8),             // red-pink
            [SyntaxTokenKind.Error] = new(0xF3, 0x8B, 0xA8),           // red
        });
}
