namespace TerminalNinja.App;

/// <summary>
/// A <see cref="SynchronizationContext"/> that routes continuations through a
/// <see cref="Dispatcher"/>, so code after an <c>await</c> that started on the UI thread
/// (e.g. after <c>Window.ShowDialogAsync</c>) resumes on the UI thread instead of a
/// thread-pool thread. Installed automatically by <see cref="Application.Run"/>; hosts that
/// drive <see cref="Application.ProcessTick"/> themselves call
/// <see cref="Application.InstallSynchronizationContext"/> on their loop thread.
/// </summary>
public sealed class DispatcherSynchronizationContext : SynchronizationContext
{
    private readonly Dispatcher _dispatcher;

    public DispatcherSynchronizationContext(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
    }

    /// <inheritdoc />
    public override void Post(SendOrPostCallback d, object? state) =>
        _dispatcher.Post(() => d(state));

    /// <inheritdoc />
    public override SynchronizationContext CreateCopy() =>
        new DispatcherSynchronizationContext(_dispatcher);
}
