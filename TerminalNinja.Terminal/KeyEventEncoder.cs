using System;
using TerminalNinja.Input;

namespace TerminalNinja.Terminal;

/// <summary>
/// Encodes <see cref="KeyEvent"/>s into the ANSI/VT byte sequences a shell expects on its
/// standard input. The mapping mirrors what xterm sends in its default mode — arrow keys
/// as <c>ESC [ A/B/C/D</c>, function keys as <c>ESC O P/Q/R/S</c>, page navigation as
/// <c>ESC [ 5~ / 6~</c>, Ctrl+letter as the corresponding C0 control byte, regular ASCII
/// passes through unchanged.
/// </summary>
/// <remarks>
/// Application-cursor-keys mode (DECCKM, <c>ESC [ ? 1 h</c>) where arrows become
/// <c>ESC O A/B/C/D</c> is out of scope for the MVP. Same for keypad keys and
/// modifyOtherKeys-style extended encodings; those are follow-ups.
/// </remarks>
public static class KeyEventEncoder
{
    /// <summary>
    /// Returns the byte sequence to send for <paramref name="key"/>, or
    /// <see langword="null"/> if the key has no terminal-input encoding (e.g. modifier-only
    /// presses that the caller should drop).
    /// </summary>
    public static byte[]? Encode(KeyEvent key)
    {
        // Ctrl + A-Z: 0x01..0x1A (the C0 control bytes).
        if (key.Ctrl && !key.Alt && key.Key is >= ConsoleKey.A and <= ConsoleKey.Z)
        {
            return [(byte)(key.Key - ConsoleKey.A + 1)];
        }

        // Named keys. We check Key before KeyChar so e.g. Enter doesn't fall through to a
        // '\r' KeyChar — most shells treat the two interchangeably but the explicit
        // mappings keep cursor + line-discipline behaviour predictable.
        switch (key.Key)
        {
            case ConsoleKey.Enter: return [0x0D];
            case ConsoleKey.Tab: return key.Shift ? [0x1B, (byte)'[', (byte)'Z'] : [0x09];
            // Linux / macOS shells expect DEL (0x7F) for backspace; Windows historically used BS
            // (0x08). xterm sends DEL by default; we follow that.
            case ConsoleKey.Backspace: return [0x7F];
            case ConsoleKey.Escape: return [0x1B];
            case ConsoleKey.Delete: return [0x1B, (byte)'[', (byte)'3', (byte)'~'];
            case ConsoleKey.Insert: return [0x1B, (byte)'[', (byte)'2', (byte)'~'];

            case ConsoleKey.UpArrow: return [0x1B, (byte)'[', (byte)'A'];
            case ConsoleKey.DownArrow: return [0x1B, (byte)'[', (byte)'B'];
            case ConsoleKey.RightArrow: return [0x1B, (byte)'[', (byte)'C'];
            case ConsoleKey.LeftArrow: return [0x1B, (byte)'[', (byte)'D'];

            case ConsoleKey.Home: return [0x1B, (byte)'[', (byte)'H'];
            case ConsoleKey.End: return [0x1B, (byte)'[', (byte)'F'];
            case ConsoleKey.PageUp: return [0x1B, (byte)'[', (byte)'5', (byte)'~'];
            case ConsoleKey.PageDown: return [0x1B, (byte)'[', (byte)'6', (byte)'~'];

            case ConsoleKey.F1: return [0x1B, (byte)'O', (byte)'P'];
            case ConsoleKey.F2: return [0x1B, (byte)'O', (byte)'Q'];
            case ConsoleKey.F3: return [0x1B, (byte)'O', (byte)'R'];
            case ConsoleKey.F4: return [0x1B, (byte)'O', (byte)'S'];
            case ConsoleKey.F5: return [0x1B, (byte)'[', (byte)'1', (byte)'5', (byte)'~'];
            case ConsoleKey.F6: return [0x1B, (byte)'[', (byte)'1', (byte)'7', (byte)'~'];
            case ConsoleKey.F7: return [0x1B, (byte)'[', (byte)'1', (byte)'8', (byte)'~'];
            case ConsoleKey.F8: return [0x1B, (byte)'[', (byte)'1', (byte)'9', (byte)'~'];
            case ConsoleKey.F9: return [0x1B, (byte)'[', (byte)'2', (byte)'0', (byte)'~'];
            case ConsoleKey.F10: return [0x1B, (byte)'[', (byte)'2', (byte)'1', (byte)'~'];
            case ConsoleKey.F11: return [0x1B, (byte)'[', (byte)'2', (byte)'3', (byte)'~'];
            case ConsoleKey.F12: return [0x1B, (byte)'[', (byte)'2', (byte)'4', (byte)'~'];
        }

        // Alt + printable: ESC + char (xterm convention).
        if (key.Alt && !key.Ctrl && key.KeyChar != '\0' && key.KeyChar < 0x80)
        {
            return [0x1B, (byte)key.KeyChar];
        }

        // Regular printable ASCII. Anything beyond 0x7F is harder — terminal input encoding
        // for non-ASCII typed text is OS-locale-dependent; the GUI host's text-input event
        // path (Step 11 IME work) is the right place for it. For now drop non-ASCII KeyChars.
        if (key.KeyChar >= 0x20 && key.KeyChar < 0x7F)
        {
            return [(byte)key.KeyChar];
        }

        return null;
    }
}
