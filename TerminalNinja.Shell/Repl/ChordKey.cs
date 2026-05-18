using System.Text;

namespace TerminalNinja.Shell.Repl;

/// <summary>
/// Canonical formatter for keyboard chord strings shared between the
/// <c>key</c> module's parser (which validates user-typed strings like
/// <c>"Ctrl+l"</c>) and <see cref="LineEditor"/> (which builds the same
/// strings from <see cref="ConsoleKeyInfo"/> at read time). Both ends MUST
/// route through here so a binding registered as <c>"Ctrl+l"</c> matches
/// the chord produced by an actual <c>Ctrl+L</c> keystroke.
/// </summary>
/// <remarks>
/// Format: <c>[Ctrl+][Alt+][Shift+]Key</c>. Modifiers are always written in
/// Ctrl/Alt/Shift order with PascalCase names. The key portion is normalised
/// so that a single character is upper-cased (<c>l → L</c>) and multi-character
/// names like <c>Tab</c> or <c>UpArrow</c> have their first letter upper-cased
/// (the rest is left as the caller spelled it — <see cref="ConsoleKey"/>'s
/// own ToString output already arrives PascalCase).
/// </remarks>
internal static class ChordKey
{
    /// <summary>Compose a canonical chord string from explicit modifier flags + key name.</summary>
    public static string Format(bool ctrl, bool alt, bool shift, string keyName)
    {
        var sb = new StringBuilder();
        if (ctrl) sb.Append("Ctrl+");
        if (alt) sb.Append("Alt+");
        if (shift) sb.Append("Shift+");
        sb.Append(Canonicalize(keyName));
        return sb.ToString();
    }

    private static string Canonicalize(string keyName)
    {
        if (string.IsNullOrEmpty(keyName)) return keyName;
        if (keyName.Length == 1) return keyName.ToUpperInvariant();
        return char.ToUpperInvariant(keyName[0]) + keyName[1..];
    }
}
