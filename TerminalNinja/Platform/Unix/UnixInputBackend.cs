namespace TerminalNinja.Platform.Unix;

/// <summary>
/// Unix/Linux/macOS input backend using System.Console.
/// Provides keyboard input. Mouse tracking is done via ANSI escape sequences.
/// </summary>
public sealed class UnixInputBackend : Input.IInputBackend
{
    private bool _disposed;
    private bool _mouseTrackingEnabled;

    public UnixInputBackend()
    {
        // No special initialization needed for Unix
        // We use System.Console which handles the platform details
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
        System.Console.Write("\u001b[?1003h\u001b[?1006h");

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
        System.Console.Write("\u001b[?1003l\u001b[?1006l");

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
