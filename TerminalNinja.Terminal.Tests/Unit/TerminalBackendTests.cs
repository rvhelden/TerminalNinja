using TerminalNinja.Terminal;

namespace TerminalNinja.Terminal.Tests.Unit;

/// <summary>
/// Tests for the ITerminalBackend contract, the factory, and the in-memory
/// NullTerminalBackend. ConPTY / Unix PTY implementations have their own integration
/// tests that need a real OS PTY to drive — those live in a separate suite.
/// </summary>
public class TerminalBackendTests
{
    private static TerminalBackendOptions ValidOptions(int cols = 80, int rows = 24) =>
        new(Shell: "cmd.exe", Arguments: [], InitialCols: cols, InitialRows: rows);

    [Test]
    public async Task Options_Validate_RejectsBlankShell()
    {
        var opts = new TerminalBackendOptions(Shell: "  ", Arguments: [], InitialCols: 80, InitialRows: 24);
        await Assert.That(() => opts.Validate()).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Options_Validate_RejectsZeroCols()
    {
        var opts = new TerminalBackendOptions(Shell: "sh", Arguments: [], InitialCols: 0, InitialRows: 24);
        await Assert.That(() => opts.Validate()).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Options_Validate_RejectsNegativeRows()
    {
        var opts = new TerminalBackendOptions(Shell: "sh", Arguments: [], InitialCols: 80, InitialRows: -1);
        await Assert.That(() => opts.Validate()).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Factory_Create_PicksPlatformSpecificBackend()
    {
        using var backend = TerminalBackend.Create(ValidOptions());

        if (OperatingSystem.IsWindows())
        {
            await Assert.That(backend).IsTypeOf<ConPtyTerminalBackend>();
        }
        else
        {
            await Assert.That(backend).IsTypeOf<UnixTerminalBackend>();
        }
    }

    [Test]
    public async Task NullBackend_WriteAsync_RecordsBytes()
    {
        await using var backend = new NullTerminalBackend();
        await backend.StartAsync();

        await backend.WriteAsync(new ReadOnlyMemory<byte>([0x68, 0x69])); // "hi"

        await Assert.That(backend.WrittenBytes.Count).IsEqualTo(2);
        await Assert.That(backend.WrittenBytes[0]).IsEqualTo((byte)0x68);
        await Assert.That(backend.WrittenBytes[1]).IsEqualTo((byte)0x69);
    }

    [Test]
    public async Task NullBackend_ResizeAsync_RecordsHistory()
    {
        await using var backend = new NullTerminalBackend();
        await backend.StartAsync();

        await backend.ResizeAsync(120, 40);
        await backend.ResizeAsync(80, 24);

        await Assert.That(backend.ResizeHistory.Count).IsEqualTo(2);
        await Assert.That(backend.ResizeHistory[0]).IsEqualTo((120, 40));
        await Assert.That(backend.ResizeHistory[1]).IsEqualTo((80, 24));
    }

    [Test]
    public async Task NullBackend_SimulateDataReceived_FiresEvent()
    {
        await using var backend = new NullTerminalBackend();
        await backend.StartAsync();

        var collected = new List<byte[]>();
        backend.DataReceived += data => collected.Add(data.ToArray());

        backend.SimulateDataReceived(new ReadOnlyMemory<byte>([0x41, 0x42]));
        backend.SimulateDataReceived(new ReadOnlyMemory<byte>([0x43]));

        await Assert.That(collected.Count).IsEqualTo(2);
        await Assert.That(collected[0]).IsEquivalentTo(new byte[] { 0x41, 0x42 });
        await Assert.That(collected[1]).IsEquivalentTo(new byte[] { 0x43 });
    }

    [Test]
    public async Task NullBackend_SimulateProcessExit_FiresEventAndFlipsRunning()
    {
        await using var backend = new NullTerminalBackend();
        await backend.StartAsync();
        await Assert.That(backend.IsRunning).IsTrue();

        var exitCodes = new List<int>();
        backend.ProcessExited += code => exitCodes.Add(code);

        backend.SimulateProcessExit(7);

        await Assert.That(exitCodes.Count).IsEqualTo(1);
        await Assert.That(exitCodes[0]).IsEqualTo(7);
        await Assert.That(backend.IsRunning).IsFalse();
    }

    [Test]
    public async Task NullBackend_AfterDispose_RejectsCalls()
    {
        var backend = new NullTerminalBackend();
        await backend.StartAsync();
        backend.Dispose();

        await Assert.That(() => backend.WriteAsync(ReadOnlyMemory<byte>.Empty).AsTask())
            .ThrowsExactly<ObjectDisposedException>();
    }

    [Test]
    public async Task ConPtyBackend_OnNonWindows_StartAsyncThrowsPlatformNotSupported()
    {
        // The ConPTY backend's real implementation needs kernel32.dll. On non-Windows
        // platforms StartAsync should fail fast with a clear PlatformNotSupportedException
        // rather than a DllNotFoundException when the first P/Invoke fires.
        if (OperatingSystem.IsWindows())
        {
            // On Windows this case doesn't apply; we have full ConPTY tests elsewhere.
            return;
        }

        using var backend = new ConPtyTerminalBackend(ValidOptions());
        await Assert.That(() => backend.StartAsync().AsTask())
            .ThrowsExactly<PlatformNotSupportedException>();
    }

    [Test]
    public async Task UnixBackend_StartAsync_Throws_NotImplementedForNow()
    {
        // POSIX implementation still pending; pinned here so the follow-up commit's
        // behaviour shift fails the test loudly.
        using var backend = new UnixTerminalBackend(ValidOptions());
        await Assert.That(() => backend.StartAsync().AsTask())
            .ThrowsExactly<NotImplementedException>();
    }
}
