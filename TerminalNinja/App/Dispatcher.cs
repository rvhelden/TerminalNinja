using System.Collections.Concurrent;

namespace TerminalNinja.App;

/// <summary>
/// Marshals work onto the UI (render) thread.
/// <para>
/// Nothing in the framework locks the visual tree, so mutating bound state from a timer or an
/// await continuation can tear a frame mid-render. Work posted here is drained by
/// <see cref="Application"/> at the top of each frame — before input processing and rendering —
/// which is the one moment the tree is known not to be rendering.
/// </para>
/// </summary>
public sealed class Dispatcher
{
    private readonly ConcurrentQueue<Action> _queue = new();
    private readonly Action _wake;

    internal Dispatcher(Action wake)
    {
        _wake = wake;
    }

    /// <summary>
    /// Raised when an action posted via <see cref="Post"/> throws during the frame drain.
    /// When no handler is attached the exception propagates and ends the event loop.
    /// </summary>
    public event Action<Exception>? UnhandledException;

    /// <summary>
    /// Gets whether any posted work is waiting to be drained.
    /// </summary>
    public bool HasPendingWork => !_queue.IsEmpty;

    /// <summary>
    /// Queues work to run on the UI thread before the next frame and wakes the event loop.
    /// Safe to call from any thread.
    /// </summary>
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _queue.Enqueue(action);
        _wake();
    }

    /// <summary>
    /// Runs everything queued so far. Returns true when anything ran, so the caller can repaint.
    /// Only drains what was present on entry: an action that posts more work cannot starve the frame.
    /// </summary>
    internal bool Drain()
    {
        var pending = _queue.Count;
        var ranAny = false;

        for (var i = 0; i < pending && _queue.TryDequeue(out var action); i++)
        {
            ranAny = true;

            try
            {
                action();
            }
            catch (Exception ex) when (UnhandledException is not null)
            {
                UnhandledException.Invoke(ex);
            }
        }

        return ranAny;
    }
}
