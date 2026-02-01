using System.Runtime.InteropServices;

namespace TerminalNinja.Input;

/// <summary>
/// Reads and parses input events from the terminal (keyboard, mouse, resize).
/// Uses platform-specific backends for optimal performance and feature support.
/// </summary>
public sealed class InputReader : IDisposable
{
    private readonly IInputBackend _backend;
    private bool _disposed;

    /// <summary>
    /// Initializes a new InputReader using the appropriate platform-specific backend.
    /// </summary>
    public InputReader()
    {
        _backend = CreatePlatformBackend();
    }

    /// <summary>
    /// Initializes a new InputReader with a custom backend (for testing).
    /// </summary>
    /// <param name="backend">The backend to use.</param>
    internal InputReader(IInputBackend backend)
    {
        _backend = backend;
    }

    /// <summary>
    /// Enables mouse tracking in the terminal.
    /// </summary>
    public void EnableMouseTracking()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _backend.EnableMouseTracking();
    }

    /// <summary>
    /// Disables mouse tracking in the terminal.
    /// </summary>
    public void DisableMouseTracking()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _backend.DisableMouseTracking();
    }

    /// <summary>
    /// Tries to read input events without blocking.
    /// Returns null if no input is available.
    /// </summary>
    public IReadOnlyList<InputEvent>? TryRead()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _backend.TryRead();
    }

    /// <summary>
    /// Reads input events, blocking until at least one is available.
    /// </summary>
    public IReadOnlyList<InputEvent> Read()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _backend.Read();
    }

    /// <summary>
    /// Disposes resources and disables mouse tracking.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _backend.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// Creates the appropriate backend for the current platform.
    /// </summary>
    private static IInputBackend CreatePlatformBackend()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new Platform.Windows.WindowsInputBackend();
        }

        // Unix/Linux/macOS
        return new Platform.Unix.UnixInputBackend();
    }
}
