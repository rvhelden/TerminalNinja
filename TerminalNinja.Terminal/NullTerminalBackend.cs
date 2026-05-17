namespace TerminalNinja.Terminal;

/// <summary>
/// In-memory <see cref="ITerminalBackend"/> for tests and demos that don't want to spawn
/// a real shell. Records every byte written via <see cref="WriteAsync"/> in
/// <see cref="WrittenBytes"/>, lets tests inject "child output" via
/// <see cref="SimulateDataReceived"/>, and lets them simulate process exit via
/// <see cref="SimulateProcessExit"/>.
/// </summary>
public sealed class NullTerminalBackend : ITerminalBackend
{
    private readonly List<byte> _writtenBytes = [];
    private readonly List<(int Cols, int Rows)> _resizes = [];
    private bool _disposed;

    /// <inheritdoc />
    public event Action<ReadOnlyMemory<byte>>? DataReceived;

    /// <inheritdoc />
    public event Action<int>? ProcessExited;

    /// <inheritdoc />
    public bool IsRunning { get; private set; }

    /// <inheritdoc />
    public int ProcessId => -1;

    /// <summary>Bytes written via <see cref="WriteAsync"/> in order received.</summary>
    public IReadOnlyList<byte> WrittenBytes => _writtenBytes;

    /// <summary>Every <see cref="ResizeAsync"/> call recorded in order.</summary>
    public IReadOnlyList<(int Cols, int Rows)> ResizeHistory => _resizes;

    /// <inheritdoc />
    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        IsRunning = true;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _writtenBytes.AddRange(data.ToArray());
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ResizeAsync(int cols, int rows, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _resizes.Add((cols, rows));
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Test hook: pretend the child wrote <paramref name="data"/> to its stdout. Fires
    /// <see cref="DataReceived"/> synchronously.
    /// </summary>
    public void SimulateDataReceived(ReadOnlyMemory<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        DataReceived?.Invoke(data);
    }

    /// <summary>
    /// Test hook: pretend the child exited with the given code. Fires
    /// <see cref="ProcessExited"/> synchronously and flips <see cref="IsRunning"/> to false.
    /// </summary>
    public void SimulateProcessExit(int exitCode)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        IsRunning = false;
        ProcessExited?.Invoke(exitCode);
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
