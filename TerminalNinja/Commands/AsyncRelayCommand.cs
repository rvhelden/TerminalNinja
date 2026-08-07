namespace TerminalNinja.Commands;

/// <summary>
/// A command that relays its execution to an asynchronous delegate.
/// The command reports <see cref="CanExecute"/> = false while the task is running, so a
/// bound Button disables itself for the duration. Completion (and the resulting
/// <see cref="CanExecuteChanged"/>) is marshalled back through the
/// <see cref="SynchronizationContext"/> current at Execute time — on the UI thread that is the
/// <see cref="App.DispatcherSynchronizationContext"/> installed by <see cref="App.Application.Run"/>,
/// so bound state is only touched on the UI thread.
/// </summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Func<object?, bool>? _canExecute;
    private readonly Action<Exception>? _onException;
    private bool _isExecuting;

    /// <summary>
    /// Creates a new async command with no parameter.
    /// </summary>
    /// <param name="execute">The asynchronous work to run.</param>
    /// <param name="canExecute">Optional predicate gating execution.</param>
    /// <param name="onException">
    /// Invoked (on the UI thread when a Dispatcher is available) when the task faults.
    /// Without a handler the exception is rethrown on the UI thread, ending the event loop —
    /// a silently swallowed failure is worse than a visible crash.
    /// </param>
    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null, Action<Exception>? onException = null)
        : this(_ => execute(), canExecute is null ? null : _ => canExecute(), onException)
    {
        ArgumentNullException.ThrowIfNull(execute);
    }

    /// <summary>
    /// Creates a new async command.
    /// </summary>
    public AsyncRelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null, Action<Exception>? onException = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
        _onException = onException;
    }

    /// <summary>
    /// Gets whether the asynchronous work is currently running.
    /// </summary>
    public bool IsExecuting => _isExecuting;

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);

    /// <inheritdoc />
    public void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _isExecuting = true;
        RaiseCanExecuteChanged();

        Task task;
        try
        {
            task = _execute(parameter);
        }
        catch (Exception ex)
        {
            // Synchronous throw before the first await.
            Complete(ex);
            return;
        }

        var completionScheduler = SynchronizationContext.Current is not null
            ? TaskScheduler.FromCurrentSynchronizationContext()
            : TaskScheduler.Default;

        task.ContinueWith(
            t => Complete(t.Exception?.GetBaseException()),
            CancellationToken.None,
            TaskContinuationOptions.None,
            completionScheduler);
    }

    private void Complete(Exception? exception)
    {
        _isExecuting = false;
        RaiseCanExecuteChanged();

        if (exception is null)
        {
            return;
        }

        if (_onException is not null)
        {
            _onException(exception);
        }
        else
        {
            throw exception;
        }
    }

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Raises the CanExecuteChanged event.
    /// </summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
