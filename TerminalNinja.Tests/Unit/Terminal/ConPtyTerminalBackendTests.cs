using System.Text;
using TerminalNinja.Terminal;

namespace TerminalNinja.Tests.Unit.Terminal;

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

        // Use whoami.exe rather than cmd /c echo: cmd's /c short-circuit can race the
        // pseudoconsole's output buffer flush at exit, dropping the command output. whoami
        // is a plain console subsystem program that prints its result to stdout and exits
        // cleanly — the pseudoconsole reliably forwards the bytes.
        var options = new TerminalBackendOptions(
            Shell: Environment.ExpandEnvironmentVariables("%SystemRoot%\\System32\\whoami.exe"),
            Arguments: [],
            InitialCols: 80,
            InitialRows: 24);

        var captured = new MemoryStream();
        var dataLock = new object();
        var receivedNonInitData = new TaskCompletionSource();

        await using var backend = new ConPtyTerminalBackend(options);
        backend.DataReceived += data =>
        {
            lock (dataLock)
            {
                captured.Write(data.Span);

                // Anything past the initial CSI / OSC stream from ConPTY itself is real
                // program output. We signal once we see a printable letter (whoami's domain
                // / user name contains at least one alpha character).
                foreach (var b in data.Span)
                {
                    if ((b >= (byte)'a' && b <= (byte)'z') || (b >= (byte)'A' && b <= (byte)'Z'))
                    {
                        receivedNonInitData.TrySetResult();
                        break;
                    }
                }
            }
        };

        await backend.StartAsync();
        await Assert.That(backend.IsRunning).IsTrue();
        await Assert.That(backend.ProcessId).IsGreaterThan(0);

        await Task.WhenAny(receivedNonInitData.Task, Task.Delay(TestTimeoutMs));
        await Assert.That(receivedNonInitData.Task.IsCompleted).IsTrue();

        var text = Encoding.UTF8.GetString(captured.ToArray());
        // whoami output should contain at least one letter — typically the domain or user name.
        await Assert.That(text.Length).IsGreaterThan(0);
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
