namespace TerminalNinja.Platform.Unix;

/// <summary>
/// Unix/Linux/macOS input backend using System.Console.
/// Keyboard comes from <c>Console.ReadKey</c>; mouse reports arrive in-band as ANSI escape
/// sequences (enabled via DECSET 1003/1006) and are reassembled by <see cref="AnsiMouseParser"/>
/// from the characters ReadKey delivers — ReadKey itself cannot parse them, and without the
/// parser the coordinate digits decode as phantom key presses.
/// </summary>
public sealed class UnixInputBackend : Input.IInputBackend
{
    private const char Escape = '\x1b';

    private bool _disposed;
    private bool _mouseTrackingEnabled;
    private readonly bool _previousTreatControlCAsInput;
    private readonly bool _controlCConfigured;

    public UnixInputBackend()
    {
        // Treat Ctrl+C as normal keyboard input instead of letting the OS deliver
        // SIGINT. Without this, pressing Ctrl+C kills the process immediately and
        // the terminal is left in a broken state (hidden cursor, raw mode, etc.)
        // because TerminalGuard.Dispose() never runs.
        //
        // The property may throw IOException when no real console handle is
        // available (e.g. headless CI, redirected I/O). In that case we simply
        // skip the configuration — there's no terminal to protect anyway.
        try
        {
            _previousTreatControlCAsInput = System.Console.TreatControlCAsInput;
            System.Console.TreatControlCAsInput = true;
            _controlCConfigured = true;
        }
        catch (IOException)
        {
            _controlCConfigured = false;
        }
    }

    public IReadOnlyList<Input.InputEvent>? TryRead()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!System.Console.KeyAvailable)
        {
            return null;
        }

        var events = new List<Input.InputEvent>();
        while (System.Console.KeyAvailable)
        {
            ReadOne(events);
        }

        return events.Count > 0 ? events : null;
    }

    public IReadOnlyList<Input.InputEvent> Read()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var events = new List<Input.InputEvent>();
        while (events.Count == 0)
        {
            // The first ReadKey inside blocks until input arrives; a swallowed
            // malformed mouse report loops for the next real event.
            ReadOne(events);
        }

        return events;
    }

    /// <summary>
    /// Reads one logical input: a key press, or a complete mouse report reassembled from the
    /// escape sequence ReadKey splits into characters.
    /// </summary>
    private void ReadOne(List<Input.InputEvent> events)
    {
        var keyInfo = System.Console.ReadKey(intercept: true);

        // A bare Escape press arrives with nothing behind it; an escape *sequence* arrives as a
        // burst, so the tail is already buffered and KeyAvailable is true. (ReadKey has already
        // consumed the sequences it knows — arrows, F-keys — before we ever see them; what
        // reaches us as ESC-plus-buffered-input is a sequence it does not understand.)
        if (keyInfo.KeyChar != Escape || !System.Console.KeyAvailable)
        {
            events.Add(ConvertKeyInfo(keyInfo));
            return;
        }

        ReadEscapeSequence(keyInfo, events);
    }

    /// <summary>
    /// Attempts to reassemble a mouse report from the characters following an ESC. Anything that
    /// turns out not to be a mouse report (e.g. Alt+key, which many terminals send as ESC+key) is
    /// replayed as the individual key events it always was.
    /// </summary>
    private void ReadEscapeSequence(ConsoleKeyInfo escape, List<Input.InputEvent> events)
    {
        var parser = new AnsiMouseParser();
        var consumed = new List<ConsoleKeyInfo>();

        while (WaitForBufferedKey())
        {
            var keyInfo = System.Console.ReadKey(intercept: true);
            consumed.Add(keyInfo);

            switch (parser.Feed(keyInfo.KeyChar))
            {
                case AnsiMouseParser.Status.Pending:
                    continue;

                case AnsiMouseParser.Status.Matched:
                    if (parser.Result is { } mouse)
                    {
                        events.Add(mouse);
                    }

                    return;

                case AnsiMouseParser.Status.Failed:
                    Replay(escape, consumed, events);
                    return;
            }
        }

        // The buffer ran dry mid-sequence: not a recognisable report after all.
        Replay(escape, consumed, events);
    }

    /// <summary>
    /// A report is normally buffered whole, but give a torn one a few milliseconds to finish
    /// arriving rather than misreading its tail as key presses.
    /// </summary>
    private static bool WaitForBufferedKey()
    {
        for (var i = 0; i < 8; i++)
        {
            if (System.Console.KeyAvailable)
            {
                return true;
            }

            Thread.Sleep(1);
        }

        return System.Console.KeyAvailable;
    }

    private void Replay(ConsoleKeyInfo escape, List<ConsoleKeyInfo> consumed, List<Input.InputEvent> events)
    {
        events.Add(ConvertKeyInfo(escape));
        foreach (var keyInfo in consumed)
        {
            events.Add(ConvertKeyInfo(keyInfo));
        }
    }

    public void EnableMouseTracking()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_mouseTrackingEnabled)
        {
            return;
        }

        // SGR extended coordinates (1006) + any-event tracking (1003). The reports come back
        // in-band on stdin and are reassembled by AnsiMouseParser — see ReadOne.
        System.Console.Write("\e[?1006h\e[?1003h");
        _mouseTrackingEnabled = true;
    }

    public void DisableMouseTracking()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_mouseTrackingEnabled)
        {
            return;
        }

        System.Console.Write("\e[?1003l\e[?1006l");
        _mouseTrackingEnabled = false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_mouseTrackingEnabled)
        {
            DisableMouseTracking();
        }

        // Restore the original Ctrl+C behavior so subsequent console usage
        // (or a debugger session) isn't affected.
        if (_controlCConfigured)
        {
            try
            {
                System.Console.TreatControlCAsInput = _previousTreatControlCAsInput;
            }
            catch (IOException)
            {
                // Console handle may have become invalid — nothing to restore.
            }
        }

        _disposed = true;
    }

    private Input.KeyEvent ConvertKeyInfo(ConsoleKeyInfo keyInfo)
    {
        var key = keyInfo.Key;
        var keyChar = keyInfo.KeyChar;
        var modifiers = keyInfo.Modifiers;

        var shift = modifiers.HasFlag(ConsoleModifiers.Shift);
        var alt = modifiers.HasFlag(ConsoleModifiers.Alt);
        var ctrl = modifiers.HasFlag(ConsoleModifiers.Control);

        return new Input.KeyEvent(key, keyChar, shift, alt, ctrl);
    }
}
