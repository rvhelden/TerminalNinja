namespace TerminalNinja.Terminal;

/// <summary>
/// POSIX pseudo-terminal backend using <c>forkpty(3)</c> (or <c>posix_openpt</c> +
/// <c>grantpt</c>/<c>unlockpt</c> + <c>ptsname_r</c> + <c>fork</c>+<c>execve</c>) followed
/// by <c>ioctl(TIOCSWINSZ)</c> for resize notifications and a background read loop on the
/// master fd.
/// </summary>
/// <remarks>
/// <para>
/// Step 11 lands the contract skeleton; the libc P/Invoke surface, fd plumbing, and
/// child spawn arrive in a subsequent commit.
/// </para>
/// </remarks>
public sealed class UnixTerminalBackend : ITerminalBackend
{
    private readonly TerminalBackendOptions _options;
    private bool _disposed;

    /// <inheritdoc />
    public event Action<ReadOnlyMemory<byte>>? DataReceived;

    /// <inheritdoc />
    public event Action<int>? ProcessExited;

    /// <inheritdoc />
    public bool IsRunning { get; private set; }

    /// <inheritdoc />
    public int ProcessId { get; private set; } = -1;

    /// <summary>Creates a backend with the given options. Does not spawn the child until <see cref="StartAsync"/>.</summary>
    public UnixTerminalBackend(TerminalBackendOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _options = options;
    }

    /// <inheritdoc />
    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _ = DataReceived;
        _ = ProcessExited;
        _ = _options;
        throw new NotImplementedException("POSIX PTY implementation lands in a follow-up commit.");
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _ = data;
        throw new NotImplementedException("POSIX PTY implementation lands in a follow-up commit.");
    }

    /// <inheritdoc />
    public ValueTask ResizeAsync(int cols, int rows, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _ = cols;
        _ = rows;
        throw new NotImplementedException("POSIX PTY implementation lands in a follow-up commit.");
    }

    /// <inheritdoc />
    public ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        IsRunning = false;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        IsRunning = false;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
        Dispose();
    }
}
