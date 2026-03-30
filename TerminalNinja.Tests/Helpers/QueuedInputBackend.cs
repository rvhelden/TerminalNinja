using TerminalNinja.Input;

namespace TerminalNinja.Tests.Helpers;

/// <summary>
/// Test input backend that returns events from a pre-loaded queue.
/// After the queue is exhausted, TryRead returns null and Read blocks forever.
/// Useful for simulating specific key sequences in Application.Run() tests.
/// </summary>
public sealed class QueuedInputBackend : IInputBackend
{
    private readonly Queue<IReadOnlyList<InputEvent>> _events = new();
    private bool _disposed;

    /// <summary>
    /// Enqueues a single input event to be returned by the next TryRead/Read call.
    /// </summary>
    public void Enqueue(InputEvent inputEvent)
    {
        _events.Enqueue([inputEvent]);
    }

    /// <summary>
    /// Enqueues a batch of input events to be returned by the next TryRead/Read call.
    /// </summary>
    public void EnqueueBatch(IReadOnlyList<InputEvent> events)
    {
        _events.Enqueue(events);
    }

    public IReadOnlyList<InputEvent>? TryRead()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _events.Count > 0 ? _events.Dequeue() : null;
    }

    public IReadOnlyList<InputEvent> Read()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _events.Count > 0 ? _events.Dequeue() : [];
    }

    public void EnableMouseTracking() { }

    public void DisableMouseTracking() { }

    public void Dispose()
    {
        _disposed = true;
    }
}
