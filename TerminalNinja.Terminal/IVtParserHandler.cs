using System;

namespace TerminalNinja.Terminal;

/// <summary>
/// Handler interface for actions emitted by <see cref="VtParser"/>. Implementations sit
/// downstream of the parser and translate VT/ANSI sequences into terminal-state changes
/// (cursor moves, SGR colour updates, screen erase, window-title changes, hyperlinks).
/// </summary>
/// <remarks>
/// <para>
/// All methods are invoked synchronously from <see cref="VtParser.Feed"/>; handlers may
/// stay on the caller's thread. The spans passed to handler methods are valid only for
/// the duration of the call — copy if you need to retain them.
/// </para>
/// <para>
/// The action set mirrors Paul Williams' VT500 parser
/// (<see href="https://vt100.net/emu/dec_ansi_parser"/>) minus DCS / SOS / PM / APC, which
/// are deferred to a follow-up. The common terminal-output sequences (SGR, cursor moves,
/// erase, OSC titles, hyperlinks) are all covered.
/// </para>
/// </remarks>
public interface IVtParserHandler
{
    /// <summary>
    /// A printable codepoint arrived. UTF-8 multi-byte sequences are decoded by the parser
    /// before this fires; <paramref name="codepoint"/> is a complete Unicode scalar value.
    /// </summary>
    void OnPrint(uint codepoint);

    /// <summary>
    /// A C0 / C1 control byte fired (e.g. <c>\b</c>, <c>\t</c>, <c>\n</c>, <c>\r</c>, BEL).
    /// ESC (0x1B) is not surfaced here — the parser handles it directly to enter the
    /// Escape state.
    /// </summary>
    void OnExecute(byte controlByte);

    /// <summary>
    /// A CSI (<c>ESC [</c>) sequence completed with final byte <paramref name="finalByte"/>.
    /// </summary>
    /// <param name="finalByte">The terminating byte (0x40-0x7E) — e.g. <c>'m'</c> for SGR, <c>'H'</c> for CUP.</param>
    /// <param name="parameters">
    /// Numeric parameters as parsed. Missing parameters are represented as <c>-1</c>
    /// (so a "default" can be distinguished from an explicit zero).
    /// </param>
    /// <param name="intermediates">Intermediate bytes (0x20-0x2F) that appeared before the final byte.</param>
    /// <param name="isPrivate">True when the parameter section started with <c>?</c>, <c>&gt;</c>, <c>&lt;</c>, or <c>=</c>.</param>
    void OnCsiDispatch(byte finalByte, ReadOnlySpan<int> parameters, ReadOnlySpan<byte> intermediates, bool isPrivate);

    /// <summary>
    /// An ESC sequence (no <c>[</c>, <c>]</c>, etc.) completed with final byte
    /// <paramref name="finalByte"/>. Used for charset selection, RIS (<c>ESC c</c>),
    /// IND (<c>ESC D</c>), and similar.
    /// </summary>
    void OnEscDispatch(byte finalByte, ReadOnlySpan<byte> intermediates);

    /// <summary>
    /// An OSC sequence (<c>ESC ]</c> ... <c>BEL</c> or <c>ESC \</c>) completed.
    /// <paramref name="command"/> is the leading numeric command (e.g. 0 / 1 / 2 for
    /// window title, 8 for hyperlinks, 52 for clipboard). <paramref name="data"/> is
    /// the remainder of the string (everything after the first <c>;</c>), as raw bytes.
    /// </summary>
    void OnOscDispatch(int command, ReadOnlySpan<byte> data);
}
