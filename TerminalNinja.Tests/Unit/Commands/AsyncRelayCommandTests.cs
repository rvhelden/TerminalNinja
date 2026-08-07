using TerminalNinja.Commands;

namespace TerminalNinja.Tests.Unit.Commands;

public class AsyncRelayCommandTests
{
    [Test]
    public async Task CanExecute_WhileRunning_IsFalse()
    {
        var tcs = new TaskCompletionSource();
        var command = new AsyncRelayCommand(() => tcs.Task);

        command.Execute(null);

        await Assert.That(command.IsExecuting).IsTrue();
        await Assert.That(command.CanExecute(null)).IsFalse();

        tcs.SetResult();
        await WaitForCompletion(command);
        await Assert.That(command.CanExecute(null)).IsTrue();
    }

    [Test]
    public async Task Execute_WhileRunning_IsIgnored()
    {
        var tcs = new TaskCompletionSource();
        var executions = 0;
        var command = new AsyncRelayCommand(() =>
        {
            executions++;
            return tcs.Task;
        });

        command.Execute(null);
        command.Execute(null);

        await Assert.That(executions).IsEqualTo(1);
        tcs.SetResult();
        await WaitForCompletion(command);
    }

    [Test]
    public async Task Execute_RaisesCanExecuteChanged_OnStartAndCompletion()
    {
        var tcs = new TaskCompletionSource();
        var command = new AsyncRelayCommand(() => tcs.Task);
        var raised = 0;
        command.CanExecuteChanged += (_, _) => raised++;

        command.Execute(null);
        await Assert.That(raised).IsEqualTo(1);

        tcs.SetResult();
        await WaitForCompletion(command);
        await Assert.That(raised).IsEqualTo(2);
    }

    [Test]
    public async Task FaultedTask_ReachesExceptionHandler()
    {
        Exception? captured = null;
        var command = new AsyncRelayCommand(
            () => Task.FromException(new InvalidOperationException("boom")),
            onException: ex => captured = ex);

        command.Execute(null);
        await WaitForCompletion(command);

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Message).IsEqualTo("boom");
    }

    [Test]
    public async Task SynchronousThrow_ReachesExceptionHandler_AndResets()
    {
        Exception? captured = null;
        var command = new AsyncRelayCommand(
            () => throw new InvalidOperationException("early"),
            onException: ex => captured = ex);

        command.Execute(null);

        await Assert.That(captured!.Message).IsEqualTo("early");
        await Assert.That(command.IsExecuting).IsFalse();
    }

    [Test]
    public async Task CanExecutePredicate_IsRespected()
    {
        var command = new AsyncRelayCommand(() => Task.CompletedTask, canExecute: () => false);
        await Assert.That(command.CanExecute(null)).IsFalse();
    }

    private static async Task WaitForCompletion(AsyncRelayCommand command)
    {
        // Completion is scheduled on the default task scheduler in tests
        // (no SynchronizationContext installed), so poll briefly.
        for (var i = 0; i < 200 && command.IsExecuting; i++)
        {
            await Task.Delay(10);
        }

        await Assert.That(command.IsExecuting).IsFalse();
    }
}
