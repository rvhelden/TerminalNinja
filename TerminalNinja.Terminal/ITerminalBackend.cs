using System;
using System.Threading;
using System.Threading.Tasks;

namespace TerminalNinja.Terminal;

/// <summary>
/// Bidirectional byte channel to a child process running inside a pseudo-terminal.
/// Implementations cover platform-specific PTY plumbing — ConPTY on Windows, <c>forkpty</c>
/// on Unix, the Docker Engine API for <c>docker exec</c> — and expose a uniform contract
/// so the rest of the terminal emulator (ANSI parser, screen buffer, the eventual
/// <c>TerminalView</c> control) doesn't have to care which one it's talking to.
/// </summary>
/// <remarks>
/// <para>
/// Threading: implementations may raise <see cref="DataReceived"/> and
/// <see cref="ProcessExited"/> on a background thread. Subscribers that touch UI state
/// must marshal to the UI thread themselves.
/// </para>
/// <para>
/// Disposal: callers should call <see cref="CloseAsync"/> for a clean shutdown that gives
/// the child a chance to flush, then <see cref="IAsyncDisposable.DisposeAsync"/> to release
/// resources. <see cref="IDisposable.Dispose"/> is best-effort synchronous teardown.
/// </para>
/// </remarks>
public interface ITerminalBackend : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Raised when the child writes bytes to its stdout/stderr (the PTY master sees them).
    /// The memory passed to the handler is valid only for the duration of the call;
    /// implementations are free to reuse the underlying buffer afterward.
    /// </summary>
    event Action<ReadOnlyMemory<byte>>? DataReceived;

    /// <summary>
    /// Raised once when the child process exits. The argument is the exit code (0 on
    /// success). Subscribers may receive this on a background thread.
    /// </summary>
    event Action<int>? ProcessExited;

    /// <summary>
    /// <see langword="true"/> while the child is alive and the backend has not been closed.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// The platform process ID of the child shell, or <c>-1</c> if no child has been spawned
    /// (e.g. before <see cref="StartAsync"/> completes, or for backends that don't expose one).
    /// </summary>
    int ProcessId { get; }

    /// <summary>
    /// Spawns the child process with the configured shell and starts the read loop. Must be
    /// awaited before <see cref="WriteAsync"/> / <see cref="ResizeAsync"/> are called.
    /// </summary>
    ValueTask StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends bytes to the child's stdin (typed characters, control sequences, paste data).
    /// </summary>
    ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies the child that the terminal window size changed. Maps to
    /// <c>ResizePseudoConsole</c> on Windows and <c>TIOCSWINSZ</c> via <c>ioctl</c> on Unix;
    /// the child sees a <c>SIGWINCH</c> signal so it can re-layout.
    /// </summary>
    ValueTask ResizeAsync(int cols, int rows, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives the child a chance to flush + exit cleanly. After the returned task completes,
    /// <see cref="IsRunning"/> is <see langword="false"/> and further <see cref="WriteAsync"/>
    /// calls throw <see cref="ObjectDisposedException"/>.
    /// </summary>
    ValueTask CloseAsync(CancellationToken cancellationToken = default);
}
