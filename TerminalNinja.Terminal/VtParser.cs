using System.Runtime.CompilerServices;

namespace TerminalNinja.Terminal;

/// <summary>
/// VT/ANSI escape-sequence parser following Paul Williams' VT500-series state machine
/// (<see href="https://vt100.net/emu/dec_ansi_parser"/>) restricted to the subset needed
/// for common terminal output: Ground, Escape, CSI (entry / param / intermediate),
/// ESC intermediate, and OSC strings. UTF-8 multi-byte sequences are decoded inline so
/// <see cref="IVtParserHandler.OnPrint"/> always receives a complete Unicode scalar value.
/// </summary>
/// <remarks>
/// <para>
/// DCS, SOS, PM, and APC strings are deferred to a follow-up commit — they are rare in
/// modern terminal output and are best implemented alongside the controls that consume
/// them (e.g. Sixel graphics, terminal capability reports).
/// </para>
/// <para>
/// Threading: not thread-safe. Maintain one parser per byte stream.
/// </para>
/// </remarks>
public sealed class VtParser
{
    private const int MaxParams = 16;
    private const int MaxIntermediates = 4;
    private const int OscBufferCap = 1024;
    private const int Utf8BufferCap = 4;

    private State _state = State.Ground;

    // CSI parameter buffer — int per parameter so a missing param is -1.
    private readonly int[] _params = new int[MaxParams];
    private int _paramCount;
    private int _currentParam = -1;
    private bool _csiPrivate;
    // True once any digit or semicolon has been consumed inside this CSI. Distinguishes
    // "ESC [ J" (zero params) from "ESC [ ; J" (one missing param) and "ESC [ 0 J" (one zero).
    private bool _csiSawParamByte;

    // Intermediates buffer for CSI and ESC sequences.
    private readonly byte[] _intermediates = new byte[MaxIntermediates];
    private int _intermediateCount;

    // OSC string buffer.
    private readonly byte[] _oscBuffer = new byte[OscBufferCap];
    private int _oscLength;
    private bool _oscSeenSt; // true after ESC inside OSC (waiting for '\' to confirm ST)

    // UTF-8 accumulator.
    private readonly byte[] _utf8 = new byte[Utf8BufferCap];
    private int _utf8Length;
    private int _utf8Expected;

    private enum State : byte
    {
        Ground,
        Escape,
        EscIntermediate,
        CsiEntry,
        CsiParam,
        CsiIntermediate,
        CsiIgnore,
        OscString,
    }

    /// <summary>Resets the parser to <see cref="State.Ground"/> with no buffered state.</summary>
    public void Reset()
    {
        _state = State.Ground;
        _paramCount = 0;
        _currentParam = -1;
        _csiPrivate = false;
        _csiSawParamByte = false;
        _intermediateCount = 0;
        _oscLength = 0;
        _oscSeenSt = false;
        _utf8Length = 0;
        _utf8Expected = 0;
    }

    /// <summary>
    /// Feeds bytes into the parser. The parser may complete or carry state across feeds —
    /// callers receiving a TCP/PTY byte stream can pass partial chunks.
    /// </summary>
    public void Feed(ReadOnlySpan<byte> bytes, IVtParserHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        for (var i = 0; i < bytes.Length; i++)
        {
            FeedByte(bytes[i], handler);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FeedByte(byte b, IVtParserHandler handler)
    {
        // Ground state's printable ASCII fast path — by far the hottest case for typical
        // terminal output. Anything outside that range falls through to the full table.
        if (_state == State.Ground && _utf8Expected == 0 && b is >= 0x20 and <= 0x7E)
        {
            handler.OnPrint(b);
            return;
        }

        switch (_state)
        {
            case State.Ground:
                HandleGround(b, handler);
                break;
            case State.Escape:
                HandleEscape(b, handler);
                break;
            case State.EscIntermediate:
                HandleEscIntermediate(b, handler);
                break;
            case State.CsiEntry:
                HandleCsiEntry(b, handler);
                break;
            case State.CsiParam:
                HandleCsiParam(b, handler);
                break;
            case State.CsiIntermediate:
                HandleCsiIntermediate(b, handler);
                break;
            case State.CsiIgnore:
                HandleCsiIgnore(b);
                break;
            case State.OscString:
                HandleOscString(b, handler);
                break;
        }
    }

    private void HandleGround(byte b, IVtParserHandler handler)
    {
        // UTF-8 continuation byte arrived for an in-progress multi-byte sequence.
        if (_utf8Expected > 0)
        {
            if ((b & 0xC0) != 0x80)
            {
                // Invalid continuation — reset UTF-8 state and re-handle this byte as if fresh.
                _utf8Length = 0;
                _utf8Expected = 0;
                handler.OnPrint(0xFFFD); // replacement character
            }
            else
            {
                _utf8[_utf8Length++] = b;
                if (_utf8Length == _utf8Expected)
                {
                    EmitDecodedCodepoint(handler);
                    _utf8Length = 0;
                    _utf8Expected = 0;
                }
                return;
            }
        }

        if (b is >= 0x20 and <= 0x7E)
        {
            handler.OnPrint(b);
            return;
        }

        if (b == 0x1B) // ESC
        {
            EnterEscape();
            return;
        }

        if (b < 0x20 || b == 0x7F)
        {
            // C0 control bytes (and DEL) — execute, not print. ESC handled above.
            handler.OnExecute(b);
            return;
        }

        // High-bit byte — UTF-8 lead or invalid.
        if ((b & 0xE0) == 0xC0)
        {
            _utf8Expected = 2;
            _utf8Length = 1;
            _utf8[0] = b;
        }
        else if ((b & 0xF0) == 0xE0)
        {
            _utf8Expected = 3;
            _utf8Length = 1;
            _utf8[0] = b;
        }
        else if ((b & 0xF8) == 0xF0)
        {
            _utf8Expected = 4;
            _utf8Length = 1;
            _utf8[0] = b;
        }
        else
        {
            // Stray continuation byte or invalid lead — emit replacement.
            handler.OnPrint(0xFFFD);
        }
    }

    private void EmitDecodedCodepoint(IVtParserHandler handler)
    {
        uint cp;
        switch (_utf8Length)
        {
            case 2:
                cp = (uint)((_utf8[0] & 0x1F) << 6 | (_utf8[1] & 0x3F));
                break;
            case 3:
                cp = (uint)((_utf8[0] & 0x0F) << 12 | (_utf8[1] & 0x3F) << 6 | (_utf8[2] & 0x3F));
                break;
            case 4:
                cp = (uint)((_utf8[0] & 0x07) << 18 | (_utf8[1] & 0x3F) << 12 | (_utf8[2] & 0x3F) << 6 | (_utf8[3] & 0x3F));
                break;
            default:
                cp = 0xFFFD;
                break;
        }

        // Reject overlong encodings + surrogates per RFC 3629.
        if (cp is >= 0xD800 and <= 0xDFFF or > 0x10FFFF)
        {
            cp = 0xFFFD;
        }

        handler.OnPrint(cp);
    }

    private void EnterEscape()
    {
        _state = State.Escape;
        _intermediateCount = 0;
        _paramCount = 0;
        _currentParam = -1;
        _csiPrivate = false;
        _csiSawParamByte = false;
        _oscLength = 0;
        _oscSeenSt = false;
    }

    private void HandleEscape(byte b, IVtParserHandler handler)
    {
        switch (b)
        {
            case 0x5B: // '['
                _state = State.CsiEntry;
                return;
            case 0x5D: // ']'
                _state = State.OscString;
                return;
            case 0x1B: // ESC ESC — restart Escape state
                EnterEscape();
                return;
        }

        if (b is >= 0x20 and <= 0x2F)
        {
            CollectIntermediate(b);
            _state = State.EscIntermediate;
            return;
        }

        if (b is >= 0x30 and <= 0x7E)
        {
            handler.OnEscDispatch(b, _intermediates.AsSpan(0, _intermediateCount));
            _state = State.Ground;
            return;
        }

        if (b < 0x20)
        {
            // Control byte inside an escape sequence — execute it, stay in Escape.
            handler.OnExecute(b);
            return;
        }

        // Anything else cancels.
        _state = State.Ground;
    }

    private void HandleEscIntermediate(byte b, IVtParserHandler handler)
    {
        if (b is >= 0x20 and <= 0x2F)
        {
            CollectIntermediate(b);
            return;
        }

        if (b is >= 0x30 and <= 0x7E)
        {
            handler.OnEscDispatch(b, _intermediates.AsSpan(0, _intermediateCount));
            _state = State.Ground;
            return;
        }

        if (b == 0x1B)
        {
            EnterEscape();
            return;
        }

        _state = State.Ground;
    }

    private void HandleCsiEntry(byte b, IVtParserHandler handler)
    {
        // Private-marker characters appear only at the very start of CSI parameters.
        // The marker itself does NOT contribute a parameter slot — only digits and
        // semicolons do.
        if (b is 0x3C or 0x3D or 0x3E or 0x3F) // '<' '=' '>' '?'
        {
            _csiPrivate = true;
            _state = State.CsiParam;
            return;
        }

        HandleCsiParam(b, handler);
    }

    private void HandleCsiParam(byte b, IVtParserHandler handler)
    {
        if (b is >= 0x30 and <= 0x39) // '0'..'9'
        {
            if (_currentParam == -1) _currentParam = 0;
            _currentParam = _currentParam * 10 + (b - 0x30);
            _csiSawParamByte = true;
            _state = State.CsiParam;
            return;
        }

        if (b == 0x3B) // ';'
        {
            FinishParam();
            _csiSawParamByte = true;
            _state = State.CsiParam;
            return;
        }

        if (b is >= 0x20 and <= 0x2F) // intermediate
        {
            if (_csiSawParamByte) FinishParam();
            CollectIntermediate(b);
            _state = State.CsiIntermediate;
            return;
        }

        if (b is >= 0x40 and <= 0x7E) // final
        {
            if (_csiSawParamByte) FinishParam();
            handler.OnCsiDispatch(b, _params.AsSpan(0, _paramCount), _intermediates.AsSpan(0, _intermediateCount), _csiPrivate);
            _state = State.Ground;
            return;
        }

        if (b == 0x1B)
        {
            EnterEscape();
            return;
        }

        // Anything else — ignore the rest of this CSI.
        _state = State.CsiIgnore;
    }

    private void HandleCsiIntermediate(byte b, IVtParserHandler handler)
    {
        if (b is >= 0x20 and <= 0x2F)
        {
            CollectIntermediate(b);
            return;
        }

        if (b is >= 0x40 and <= 0x7E)
        {
            handler.OnCsiDispatch(b, _params.AsSpan(0, _paramCount), _intermediates.AsSpan(0, _intermediateCount), _csiPrivate);
            _state = State.Ground;
            return;
        }

        if (b == 0x1B)
        {
            EnterEscape();
            return;
        }

        _state = State.CsiIgnore;
    }

    private void HandleCsiIgnore(byte b)
    {
        // Eat bytes until we see a final.
        if (b is >= 0x40 and <= 0x7E)
        {
            _state = State.Ground;
        }
        else if (b == 0x1B)
        {
            EnterEscape();
        }
    }

    private void HandleOscString(byte b, IVtParserHandler handler)
    {
        if (b == 0x07) // BEL — string terminator
        {
            DispatchOsc(handler);
            _state = State.Ground;
            return;
        }

        if (b == 0x1B)
        {
            // Wait for '\' to confirm ST; if not, abort to Escape state for the new ESC.
            _oscSeenSt = true;
            return;
        }

        if (_oscSeenSt)
        {
            if (b == 0x5C) // '\'
            {
                DispatchOsc(handler);
                _state = State.Ground;
                _oscSeenSt = false;
                return;
            }

            // ESC followed by something other than '\' — abort OSC, re-enter Escape, then re-feed.
            _oscSeenSt = false;
            _state = State.Ground;
            _oscLength = 0;
            // Re-feed the ESC into ground; then handle current byte fresh.
            HandleGround(0x1B, handler);
            FeedByte(b, handler);
            return;
        }

        if (_oscLength < OscBufferCap)
        {
            _oscBuffer[_oscLength++] = b;
        }
        // else: silently truncate. Real consumers shouldn't hit 1KB OSC strings often.
    }

    private void DispatchOsc(IVtParserHandler handler)
    {
        // OSC body: "<command>;<data>"
        var sep = 0;
        while (sep < _oscLength && _oscBuffer[sep] != 0x3B /* ';' */)
        {
            sep++;
        }

        var command = -1;
        if (sep > 0)
        {
            command = 0;
            for (var i = 0; i < sep; i++)
            {
                var c = _oscBuffer[i];
                if (c is >= 0x30 and <= 0x39)
                {
                    command = command * 10 + (c - 0x30);
                }
                else
                {
                    command = -1;
                    break;
                }
            }
        }

        var dataStart = sep < _oscLength ? sep + 1 : _oscLength;
        var dataLength = _oscLength - dataStart;
        handler.OnOscDispatch(command, _oscBuffer.AsSpan(dataStart, dataLength));

        _oscLength = 0;
    }

    private void CollectIntermediate(byte b)
    {
        if (_intermediateCount < MaxIntermediates)
        {
            _intermediates[_intermediateCount++] = b;
        }
    }

    private void FinishParam()
    {
        if (_paramCount < MaxParams)
        {
            _params[_paramCount++] = _currentParam;
        }

        _currentParam = -1;
    }
}
