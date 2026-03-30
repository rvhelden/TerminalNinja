namespace TerminalNinja.Platform.Unix;

/// <summary>
/// Unix/Linux/macOS input backend using System.Console.
/// Provides keyboard input. Mouse tracking is done via ANSI escape sequences.
/// </summary>
public sealed class UnixInputBackend : Input.IInputBackend
{
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

        var keyInfo = System.Console.ReadKey(intercept: true);
        var keyEvent = ConvertKeyInfo(keyInfo);

        return keyEvent != null ? [keyEvent] : null;
    }

    public IReadOnlyList<Input.InputEvent> Read()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Block until a key is available
        var keyInfo = System.Console.ReadKey(intercept: true);
        var keyEvent = ConvertKeyInfo(keyInfo);

        return keyEvent != null ? [keyEvent] : [];
    }

    public void EnableMouseTracking()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_mouseTrackingEnabled)
        {
            return;
        }

        // Enable ANSI mouse tracking
        // SGR mode (1006) for extended coordinates + Any-event tracking (1003)
        System.Console.Write("\e[?1003h\e[?1006h");

        _mouseTrackingEnabled = true;
    }

    public void DisableMouseTracking()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_mouseTrackingEnabled)
        {
            return;
        }

        // Disable ANSI mouse tracking
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

    private Input.KeyEvent? ConvertKeyInfo(ConsoleKeyInfo keyInfo)
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
