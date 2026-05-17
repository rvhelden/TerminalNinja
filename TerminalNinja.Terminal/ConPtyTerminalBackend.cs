namespace TerminalNinja.Terminal;

/// <summary>
/// Windows-native pseudo-terminal backend built on the ConPTY API
/// (<c>CreatePseudoConsole</c> / <c>ResizePseudoConsole</c> / <c>ClosePseudoConsole</c>
/// from <c>kernel32.dll</c>, plus <c>CreateProcessW</c> with
/// <c>STARTUPINFOEX</c>+<c>PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE_HANDLE</c>).
/// </summary>
/// <remarks>
/// <para>
/// Step 11 lands the contract skeleton; the ConPTY P/Invoke surface, pipe plumbing, and
/// process spawn arrive in a subsequent commit. The constructor is wired so factory tests
/// can verify the platform-selection logic without actually starting a process.
/// </para>
/// </remarks>
public sealed class ConPtyTerminalBackend : ITerminalBackend
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
    public ConPtyTerminalBackend(TerminalBackendOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _options = options;
    }

    /// <inheritdoc />
    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // Suppress unused-event warnings until the implementation lands.
        _ = DataReceived;
        _ = ProcessExited;
        _ = _options;
        throw new NotImplementedException("ConPTY implementation lands in a follow-up commit.");
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _ = data;
        throw new NotImplementedException("ConPTY implementation lands in a follow-up commit.");
    }

    /// <inheritdoc />
    public ValueTask ResizeAsync(int cols, int rows, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _ = cols;
        _ = rows;
        throw new NotImplementedException("ConPTY implementation lands in a follow-up commit.");
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
