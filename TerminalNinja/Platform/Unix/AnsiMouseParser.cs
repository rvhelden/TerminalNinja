using System.Text;
using TerminalNinja.Input;

namespace TerminalNinja.Platform.Unix;

/// <summary>
/// Incremental parser for the ANSI mouse reports a terminal sends after tracking is enabled.
/// Fed the characters that follow an ESC, one at a time, and recognises:
/// <list type="bullet">
/// <item>SGR reports (DECSET 1006): <c>[&lt;Cb;Cx;CyM</c> for press/move, final <c>m</c> for release.</item>
/// <item>Legacy X10 reports: <c>[M</c> followed by three bytes (32+Cb, 32+Cx, 32+Cy), sent by
/// terminals that honour tracking (1003) but not SGR encoding (1006).</item>
/// </list>
/// The characters arrive through <c>Console.ReadKey</c>, which delivers an unrecognised escape
/// sequence one KeyChar at a time with the characters intact — verified empirically; the
/// coordinate digits would otherwise be decoded as phantom key presses, which is the bug that
/// kept mouse tracking disabled on Unix.
/// </summary>
internal sealed class AnsiMouseParser
{
    internal enum Status
    {
        /// <summary>Mid-sequence; feed the next character.</summary>
        Pending,

        /// <summary>A complete mouse report was consumed; see <see cref="Result"/>.</summary>
        Matched,

        /// <summary>Not a mouse report; the caller should replay what it consumed as key input.</summary>
        Failed,
    }

    private enum State
    {
        ExpectBracket,
        ExpectMarker,
        SgrParameters,
        X10Bytes,
    }

    // "Cb;Cx;Cy" for a 4-digit-coordinate terminal is 12 chars; anything past this is garbage.
    private const int MaxParameterLength = 24;

    private State _state = State.ExpectBracket;
    private readonly StringBuilder _parameters = new();
    private readonly int[] _x10 = new int[3];
    private int _x10Count;

    /// <summary>The decoded event after <see cref="Status.Matched"/>. Null when the report was
    /// well-formed but meaningless (e.g. zero coordinates from a broken terminal) — swallowed,
    /// never replayed as keys.</summary>
    public MouseEvent? Result { get; private set; }

    /// <summary>Consumes the next character after the ESC.</summary>
    public Status Feed(char c)
    {
        switch (_state)
        {
            case State.ExpectBracket:
                if (c != '[')
                {
                    return Status.Failed;
                }

                _state = State.ExpectMarker;
                return Status.Pending;

            case State.ExpectMarker:
                if (c == '<')
                {
                    _state = State.SgrParameters;
                    return Status.Pending;
                }

                if (c == 'M')
                {
                    _state = State.X10Bytes;
                    return Status.Pending;
                }

                return Status.Failed;

            case State.SgrParameters:
                if (c is (>= '0' and <= '9') or ';')
                {
                    if (_parameters.Length >= MaxParameterLength)
                    {
                        return Status.Failed;
                    }

                    _parameters.Append(c);
                    return Status.Pending;
                }

                if (c is 'M' or 'm')
                {
                    Result = DecodeSgr(_parameters.ToString(), release: c == 'm');
                    // A malformed parameter block is still a complete (consumed) report; replaying
                    // its digits as key presses is exactly the corruption this parser prevents.
                    return Status.Matched;
                }

                return Status.Failed;

            case State.X10Bytes:
                _x10[_x10Count++] = c;
                if (_x10Count < 3)
                {
                    return Status.Pending;
                }

                Result = DecodeX10(_x10[0] - 32, _x10[1] - 33, _x10[2] - 33);
                return Status.Matched;

            default:
                return Status.Failed;
        }
    }

    /// <summary>Decodes an SGR parameter block ("Cb;Cx;Cy", 1-based coordinates).</summary>
    internal static MouseEvent? DecodeSgr(string parameters, bool release)
    {
        var parts = parameters.Split(';');
        if (parts.Length != 3
            || !int.TryParse(parts[0], out var cb)
            || !int.TryParse(parts[1], out var x)
            || !int.TryParse(parts[2], out var y))
        {
            return null;
        }

        return Decode(cb, x - 1, y - 1, release);
    }

    /// <summary>Decodes a legacy X10 report (0-based coordinates, release encoded as button 3).</summary>
    internal static MouseEvent? DecodeX10(int cb, int x, int y) =>
        Decode(cb, x, y, release: (cb & 3) == 3 && (cb & 96) == 0);

    private static MouseEvent? Decode(int cb, int x, int y, bool release)
    {
        if (x < 0 || y < 0)
        {
            return null;
        }

        var shift = (cb & 4) != 0;
        var alt = (cb & 8) != 0;
        var ctrl = (cb & 16) != 0;

        // Wheel: 64 up, 65 down (plus modifier bits).
        if ((cb & 64) != 0)
        {
            var action = (cb & 3) == 0 ? MouseAction.ScrollUp : MouseAction.ScrollDown;
            return new MouseEvent(x, y, MouseButton.None, action, shift, alt, ctrl);
        }

        var button = (cb & 3) switch
        {
            0 => MouseButton.Left,
            1 => MouseButton.Middle,
            2 => MouseButton.Right,
            _ => MouseButton.None, // 3: motion with no button (any-event tracking), or X10 release
        };

        // Motion flag (any-event or button-event tracking).
        if ((cb & 32) != 0)
        {
            return new MouseEvent(x, y, button, MouseAction.Move, shift, alt, ctrl);
        }

        return new MouseEvent(x, y, button, release ? MouseAction.Release : MouseAction.Press, shift, alt, ctrl);
    }
}
