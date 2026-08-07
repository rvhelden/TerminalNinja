using TerminalNinja.App;

namespace TerminalNinja.Tests.Unit.App;

/// <summary>
/// Tests for <see cref="Dispatcher"/> — cross-thread work posted to the UI thread is drained
/// at the top of each frame, before input processing and rendering.
/// </summary>
public class DispatcherTests
{
    private static Application CreateHeadlessApp() =>
        new(new ApplicationOptions { Headless = true });

    [Test]
    public async Task Post_IsDrainedByProcessTick()
    {
        using var app = CreateHeadlessApp();
        var ran = false;

        app.Dispatcher.Post(() => ran = true);
        await Assert.That(app.Dispatcher.HasPendingWork).IsTrue();

        app.ProcessTick();

        await Assert.That(ran).IsTrue();
        await Assert.That(app.Dispatcher.HasPendingWork).IsFalse();
    }

    [Test]
    public async Task Post_FromDrainedAction_RunsNextTick_NotSameTick()
    {
        using var app = CreateHeadlessApp();
        var secondRan = false;

        app.Dispatcher.Post(() => app.Dispatcher.Post(() => secondRan = true));

        app.ProcessTick();
        await Assert.That(secondRan).IsFalse();

        app.ProcessTick();
        await Assert.That(secondRan).IsTrue();
    }

    [Test]
    public async Task Post_MarksUiInvalidated_SoNextTickRenders()
    {
        using var app = CreateHeadlessApp();
        app.RootControl = new TextBlock { Text = "hi" };
        app.ProcessTick(); // initial render clears the invalidated flag

        app.Dispatcher.Post(() => { });
        var rendered = app.ProcessTick();

        await Assert.That(rendered).IsTrue();
    }

    [Test]
    public async Task Post_ThrowingAction_RoutesToUnhandledException()
    {
        using var app = CreateHeadlessApp();
        Exception? captured = null;
        app.Dispatcher.UnhandledException += ex => captured = ex;

        app.Dispatcher.Post(() => throw new InvalidOperationException("boom"));
        app.ProcessTick();

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Message).IsEqualTo("boom");
    }

    [Test]
    public async Task Post_ThrowingAction_WithoutHandler_Propagates()
    {
        using var app = CreateHeadlessApp();
        app.Dispatcher.Post(() => throw new InvalidOperationException("boom"));

        await Assert.That(() => app.ProcessTick()).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task Post_FromBackgroundThread_RunsOnTick()
    {
        using var app = CreateHeadlessApp();
        var ran = false;

        await Task.Run(() => app.Dispatcher.Post(() => ran = true));
        app.ProcessTick();

        await Assert.That(ran).IsTrue();
    }

    [Test]
    public async Task SynchronizationContext_PostsThroughDispatcher()
    {
        using var app = CreateHeadlessApp();
        var context = new DispatcherSynchronizationContext(app.Dispatcher);
        var ran = false;

        context.Post(_ => ran = true, null);

        await Assert.That(ran).IsFalse();
        app.ProcessTick();
        await Assert.That(ran).IsTrue();
    }

    [Test]
    public async Task InstallSynchronizationContext_InstallsDispatcherContext()
    {
        using var app = CreateHeadlessApp();

        // Capture synchronously and restore before any await: awaiting while the dispatcher
        // context is installed would post the continuation to a dispatcher nobody drains
        // (deadlock), and a yielded await could restore the context on a different pool thread,
        // leaving this one poisoned for later tests.
        var previous = SynchronizationContext.Current;
        SynchronizationContext? observed;
        try
        {
            app.InstallSynchronizationContext();
            observed = SynchronizationContext.Current;
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        await Assert.That(observed).IsTypeOf<DispatcherSynchronizationContext>();
    }
}
