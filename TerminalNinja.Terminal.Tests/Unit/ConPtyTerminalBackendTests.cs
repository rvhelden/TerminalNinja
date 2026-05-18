using System.Text;
using TerminalNinja.Terminal;

namespace TerminalNinja.Terminal.Tests.Unit;

/// <summary>
/// Windows-only integration tests for <see cref="ConPtyTerminalBackend"/>. Each test spawns
/// a real <c>cmd.exe</c> with a short-running command (<c>/c echo ...</c> or <c>/c exit</c>)
/// and asserts on the bytes ConPTY reports back. Skipped on non-Windows OSes.
/// </summary>
/// <remarks>
/// These tests are time-sensitive — ConPTY adds its own initialization output (a sequence
/// of clear-and-position escapes) before the child's stdout. We poll DataReceived for a
/// short window and look for the expected substring rather than asserting a strict format.
/// </remarks>
public class ConPtyTerminalBackendTests
{
    private const int TestTimeoutMs = 5000;
    private const int InitialWaitMs = 500;

    private static TerminalBackendOptions EchoOptions(string output) =>
        new(
            Shell: Environment.ExpandEnvironmentVariables("%SystemRoot%\\System32\\cmd.exe"),
            Arguments: ["/c", "echo " + output],
            InitialCols: 80,
            InitialRows: 24);

    [Test]
    public async Task StartAsync_OnWindows_SpawnsChildAndReceivesOutput()
    {
        if (!OperatingSystem.IsWindows())
        {
            // No ConPTY on this platform — Windows-only test.
            return;
        }

        // Spawn `cmd /c echo <marker>` and verify the marker bytes arrive via DataReceived.
        // The marker is a fixed string we control, so a true regression where cmd inherits
        // parent stdio (output going to the test runner's console instead of the
        // pseudoconsole's pipe) fails this test — looking only for "any letter" used to pass
        // even in that broken state because ConPTY's own init sequence
        // (e.g. ESC[?9001h ESC[?1004h) contains letters.
        const string Marker = "MAGIC_PTY_HOOKUP_OK";
        var options = new TerminalBackendOptions(
            Shell: Environment.ExpandEnvironmentVariables("%SystemRoot%\\System32\\cmd.exe"),
            Arguments: ["/c", "echo " + Marker],
            InitialCols: 80,
            InitialRows: 24);

        var captured = new MemoryStream();
        var dataLock = new object();
        var sawMarker = new TaskCompletionSource();

        await using var backend = new ConPtyTerminalBackend(options);
        backend.DataReceived += data =>
        {
            lock (dataLock)
            {
                captured.Write(data.Span);
                if (Encoding.UTF8.GetString(captured.ToArray()).Contains(Marker, StringComparison.Ordinal))
                {
                    sawMarker.TrySetResult();
                }
            }
        };

        await backend.StartAsync();
        await Assert.That(backend.IsRunning).IsTrue();
        await Assert.That(backend.ProcessId).IsGreaterThan(0);

        await Task.WhenAny(sawMarker.Task, Task.Delay(TestTimeoutMs));
        await Assert.That(sawMarker.Task.IsCompleted).IsTrue();
    }

    [Test]
    public async Task StartAsync_TwiceOnSameInstance_Throws()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        await using var backend = new ConPtyTerminalBackend(EchoOptions("anything"));
        await backend.StartAsync();

        await Assert.That(() => backend.StartAsync().AsTask())
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task ProcessExited_FiresWithExitCode()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // cmd /c exit 7 — should exit with code 7.
        var options = new TerminalBackendOptions(
            Shell: Environment.ExpandEnvironmentVariables("%SystemRoot%\\System32\\cmd.exe"),
            Arguments: ["/c", "exit 7"],
            InitialCols: 80,
            InitialRows: 24);

        var exitCodeTcs = new TaskCompletionSource<int>();

        await using var backend = new ConPtyTerminalBackend(options);
        backend.ProcessExited += code => exitCodeTcs.TrySetResult(code);

        await backend.StartAsync();

        var completed = await Task.WhenAny(exitCodeTcs.Task, Task.Delay(TestTimeoutMs));
        await Assert.That(exitCodeTcs.Task.IsCompleted).IsTrue();
        await Assert.That(exitCodeTcs.Task.Result).IsEqualTo(7);
        await Assert.That(backend.IsRunning).IsFalse();
        _ = completed;
    }

    [Test]
    public async Task ResizeAsync_DoesNotThrow()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        await using var backend = new ConPtyTerminalBackend(EchoOptions("resize"));
        await backend.StartAsync();

        await backend.ResizeAsync(100, 30);
        await backend.ResizeAsync(80, 24);

        // No exception is the success condition. Give the child a moment to settle so
        // ProcessExited fires before DisposeAsync waits.
        await Task.Delay(InitialWaitMs);
    }

    [Test]
    public async Task Dispose_BeforeChildExits_TerminatesCleanly()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Plain cmd.exe runs an interactive prompt indefinitely. We Dispose before it would
        // close on its own; the assertion is that disposal completes quickly and IsRunning
        // flips to false.
        var options = new TerminalBackendOptions(
            Shell: Environment.ExpandEnvironmentVariables("%SystemRoot%\\System32\\cmd.exe"),
            Arguments: [],
            InitialCols: 80,
            InitialRows: 24);

        var backend = new ConPtyTerminalBackend(options);
        try
        {
            await backend.StartAsync();
            await Assert.That(backend.IsRunning).IsTrue();
        }
        finally
        {
            await backend.DisposeAsync();
        }

        await Assert.That(backend.IsRunning).IsFalse();
    }
}
