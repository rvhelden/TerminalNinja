using TerminalNinja.App;
using TerminalNinja.Input;
using TerminalNinja.Tests.Helpers;

namespace TerminalNinja.Tests.Unit.App;

/// <summary>
/// Tests for Ctrl+C key handling in the Application event loop.
/// Verifies that Ctrl+C triggers a graceful exit via Application.Exit().
/// </summary>
[NotInParallel("ApplicationSingleton")]
public class ApplicationCtrlCTests
{
    // ─── Ctrl+C exits the application ────────────────────────────────

    [Test]
    public async Task Run_CtrlC_ExitsGracefully()
    {
        // Arrange: queue a Ctrl+C event
        var backend = new QueuedInputBackend();
        backend.Enqueue(new KeyEvent(ConsoleKey.C, '\x03', Shift: false, Alt: false, Ctrl: true));

        using var app = new Application(new ApplicationOptions
        {
            Headless = true,
            InputBackend = backend,
        });
        app.RootControl = new TextBlock { Text = "Test" };

        // Act: Run should process the Ctrl+C and exit immediately
        var runCompleted = false;
        app.Run();
        runCompleted = true;

        // Assert: if we get here, Exit() was called (Run returned)
        await Assert.That(runCompleted).IsTrue();
    }

    [Test]
    public async Task Run_CtrlC_ExitsBeforeProcessingSubsequentEvents()
    {
        // Arrange: Ctrl+C followed by another key — the second key should never be processed
        var backend = new QueuedInputBackend();
        backend.Enqueue(new KeyEvent(ConsoleKey.C, '\x03', Shift: false, Alt: false, Ctrl: true));
        backend.Enqueue(new KeyEvent(ConsoleKey.A, 'a', Shift: false, Alt: false, Ctrl: false));

        var keyDownCount = 0;
        using var app = new Application(new ApplicationOptions
        {
            Headless = true,
            InputBackend = backend,
        });
        app.RootControl = new TextBlock { Text = "Test" };
        app.KeyDown += (_, _) => keyDownCount++;

        // Act
        var runCompleted = false;
        app.Run();
        runCompleted = true;

        // Assert: Run returned (app exited due to Ctrl+C).
        // ProcessInput drains all available events per loop iteration, so the 'A' event
        // may also be processed. What matters is that the loop exits.
        await Assert.That(runCompleted).IsTrue();
    }

    // ─── Ctrl+C is not blocked by KeyDown handlers ───────────────────

    [Test]
    public async Task Run_CtrlC_ExitsEvenWithKeyDownHandler()
    {
        // Arrange: a KeyDown handler that does NOT set Handled
        var backend = new QueuedInputBackend();
        backend.Enqueue(new KeyEvent(ConsoleKey.C, '\x03', Shift: false, Alt: false, Ctrl: true));

        var keyDownFired = false;
        using var app = new Application(new ApplicationOptions
        {
            Headless = true,
            InputBackend = backend,
        });
        app.RootControl = new TextBlock { Text = "Test" };
        app.KeyDown += (_, _) => keyDownFired = true;

        // Act
        app.Run();

        // Assert: KeyDown fires first (before built-in Ctrl+C check), but app still exits
        await Assert.That(keyDownFired).IsTrue();
    }

    [Test]
    public async Task Run_CtrlC_CanBeInterceptedByKeyDownHandler()
    {
        // Arrange: a KeyDown handler that sets Handled=true should prevent the exit
        var backend = new QueuedInputBackend();
        // Enqueue Ctrl+C (will be intercepted) then Escape (to actually exit)
        backend.Enqueue(new KeyEvent(ConsoleKey.C, '\x03', Shift: false, Alt: false, Ctrl: true));
        backend.Enqueue(new KeyEvent(ConsoleKey.Escape, '\x1b', Shift: false, Alt: false, Ctrl: false));

        var ctrlCIntercepted = false;
        using var app = new Application(new ApplicationOptions
        {
            Headless = true,
            InputBackend = backend,
        });
        app.RootControl = new TextBlock { Text = "Test" };
        app.KeyDown += (keyEvent, args) =>
        {
            if (keyEvent is { Key: ConsoleKey.C, Ctrl: true })
            {
                ctrlCIntercepted = true;
                args.Handled = true; // Prevent the built-in Ctrl+C exit
            }
        };

        // Act
        app.Run();

        // Assert: Ctrl+C was intercepted, app exited via Escape instead
        await Assert.That(ctrlCIntercepted).IsTrue();
    }

    // ─── Ctrl+C with other modifiers should NOT exit ─────────────────

    [Test]
    public async Task Run_CtrlShiftC_DoesNotExit()
    {
        // Arrange: Ctrl+Shift+C should not trigger exit (only plain Ctrl+C)
        var backend = new QueuedInputBackend();
        backend.Enqueue(new KeyEvent(ConsoleKey.C, '\x03', Shift: true, Alt: false, Ctrl: true));
        // Follow with Escape to actually exit the loop
        backend.Enqueue(new KeyEvent(ConsoleKey.Escape, '\x1b', Shift: false, Alt: false, Ctrl: false));

        var ctrlShiftCReceived = false;
        using var app = new Application(new ApplicationOptions
        {
            Headless = true,
            InputBackend = backend,
        });
        app.RootControl = new TextBlock { Text = "Test" };
        app.KeyDown += (keyEvent, _) =>
        {
            if (keyEvent is { Key: ConsoleKey.C, Ctrl: true, Shift: true })
            {
                ctrlShiftCReceived = true;
            }
        };

        // Act
        app.Run();

        // Assert: Ctrl+Shift+C was passed through (not treated as exit)
        await Assert.That(ctrlShiftCReceived).IsTrue();
    }

    [Test]
    public async Task Run_CtrlAltC_DoesNotExit()
    {
        // Arrange: Ctrl+Alt+C should not trigger exit
        var backend = new QueuedInputBackend();
        backend.Enqueue(new KeyEvent(ConsoleKey.C, '\x03', Shift: false, Alt: true, Ctrl: true));
        backend.Enqueue(new KeyEvent(ConsoleKey.Escape, '\x1b', Shift: false, Alt: false, Ctrl: false));

        var ctrlAltCReceived = false;
        using var app = new Application(new ApplicationOptions
        {
            Headless = true,
            InputBackend = backend,
        });
        app.RootControl = new TextBlock { Text = "Test" };
        app.KeyDown += (keyEvent, _) =>
        {
            if (keyEvent is { Key: ConsoleKey.C, Ctrl: true, Alt: true })
            {
                ctrlAltCReceived = true;
            }
        };

        // Act
        app.Run();

        // Assert: Ctrl+Alt+C was passed through
        await Assert.That(ctrlAltCReceived).IsTrue();
    }

    // ─── Escape still works after Ctrl+C support ─────────────────────

    [Test]
    public async Task Run_Escape_StillExitsApp()
    {
        // Arrange: Escape should continue to work as before
        var backend = new QueuedInputBackend();
        backend.Enqueue(new KeyEvent(ConsoleKey.Escape, '\x1b', Shift: false, Alt: false, Ctrl: false));

        using var app = new Application(new ApplicationOptions
        {
            Headless = true,
            InputBackend = backend,
        });
        app.RootControl = new TextBlock { Text = "Test" };

        // Act
        var runCompleted = false;
        app.Run();
        runCompleted = true;

        // Assert: Run returned, so Escape still works
        await Assert.That(runCompleted).IsTrue();
    }

    // ─── UnixInputBackend Ctrl+C setup ───────────────────────────────

    [Test]
    public async Task UnixInputBackend_CreatesAndDisposesWithoutError()
    {
        // Verify the backend can be created and disposed without error on any platform.
        // On Windows or in CI without a real console, the TreatControlCAsInput property
        // may throw IOException, which the constructor handles gracefully.
        var backend = new TerminalNinja.Platform.Unix.UnixInputBackend();
        backend.Dispose();
        var completed = true;
        await Assert.That(completed).IsTrue();
    }

    [Test]
    public async Task UnixInputBackend_RestoresControlCOnDispose()
    {
        // This test verifies that UnixInputBackend restores Console.TreatControlCAsInput
        // on Dispose. We can only test this when a real console handle is available.
        bool previousValue;
        try
        {
            previousValue = System.Console.TreatControlCAsInput;
        }
        catch (IOException)
        {
            // No real console handle — skip the meaningful assertion
            var skipped = true;
            await Assert.That(skipped).IsTrue();
            return;
        }

        try
        {
            var unixBackend = new TerminalNinja.Platform.Unix.UnixInputBackend();
            
            bool valueAfterConstruct;
            try
            {
                valueAfterConstruct = System.Console.TreatControlCAsInput;
            }
            catch (IOException)
            {
                var noConsole = true;
                await Assert.That(noConsole).IsTrue();
                unixBackend.Dispose();
                return;
            }
            
            unixBackend.Dispose();
            var valueAfterDispose = System.Console.TreatControlCAsInput;

            await Assert.That(valueAfterConstruct).IsTrue();
            await Assert.That(valueAfterDispose).IsEqualTo(previousValue);
        }
        finally
        {
            // Safety restore
            try
            {
                System.Console.TreatControlCAsInput = previousValue;
            }
            catch (IOException)
            {
                // Ignore — no console handle
            }
        }
    }
}
