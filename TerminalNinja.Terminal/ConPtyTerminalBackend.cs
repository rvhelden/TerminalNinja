using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TerminalNinja.Terminal.Native;

namespace TerminalNinja.Terminal;

/// <summary>
/// Windows-native pseudo-terminal backend built on the ConPTY API (<c>CreatePseudoConsole</c>,
/// <c>ResizePseudoConsole</c>, <c>ClosePseudoConsole</c>) plus <c>CreateProcessW</c> with
/// <c>STARTUPINFOEXW</c> + <c>PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE_HANDLE</c>.
/// </summary>
/// <remarks>
/// <para>
/// Two pipes connect us to the child:
/// </para>
/// <list type="bullet">
/// <item><description>
/// Input pipe — we hold the write end (<c>_inputWriteHandle</c>); the pseudoconsole reads
/// from the other end. Bytes written here arrive at the child's stdin.
/// </description></item>
/// <item><description>
/// Output pipe — we hold the read end (<c>_outputReadHandle</c>); the pseudoconsole writes
/// to the other end. Bytes the child sends to stdout/stderr show up here. A dedicated read
/// thread loops on <c>ReadFile</c> and fires <see cref="DataReceived"/>.
/// </description></item>
/// </list>
/// <para>
/// A second background thread waits on the child process handle and fires
/// <see cref="ProcessExited"/> when it terminates.
/// </para>
/// </remarks>
public sealed class ConPtyTerminalBackend : ITerminalBackend
{
    private readonly TerminalBackendOptions _options;

    private IntPtr _pseudoConsole = IntPtr.Zero;
    private IntPtr _inputWriteHandle = IntPtr.Zero;
    private IntPtr _outputReadHandle = IntPtr.Zero;
    private IntPtr _processHandle = IntPtr.Zero;
    private IntPtr _threadHandle = IntPtr.Zero;
    private IntPtr _attributeList = IntPtr.Zero;

    private Thread? _readThread;
    private Thread? _exitWatcherThread;
    private CancellationTokenSource? _shutdownCts;

    private bool _disposed;
    private int _started;

    /// <inheritdoc />
    public event Action<ReadOnlyMemory<byte>>? DataReceived;

    /// <inheritdoc />
    public event Action<int>? ProcessExited;

    /// <inheritdoc />
    public bool IsRunning { get; private set; }

    /// <inheritdoc />
    public int ProcessId { get; private set; } = -1;

    /// <summary>Creates a backend with the given options. Does not spawn the child until <see cref="StartAsync"/>.</summary>
    public ConPtyTerminalBackend(TerminalBackendOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _options = options;
    }

    /// <inheritdoc />
    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("ConPtyTerminalBackend requires Windows.");
        }

        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
        {
            throw new InvalidOperationException("StartAsync may only be called once per backend instance.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            CreatePipes();
            CreatePseudoConsole();
            BuildAttributeList();
            SpawnChild();

            // The pseudoconsole now owns one end of each pipe — release our copies of those.
            // Keeping them open prevents the child's read side from ever seeing EOF when we
            // close OUR write side later, which leaks pipe state on shutdown.
            ReleaseInheritedPipeEnds();

            _shutdownCts = new CancellationTokenSource();
            IsRunning = true;
            StartReadThread();
            StartExitWatcherThread();
        }
        catch
        {
            CleanupHandles();
            IsRunning = false;
            throw;
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsRunning || _inputWriteHandle == IntPtr.Zero || data.IsEmpty)
        {
            return ValueTask.CompletedTask;
        }

        // WriteFile on a pipe can block when the pipe is full. Push to the thread pool so the
        // caller's UI / render thread never waits. The data span is copied into a pooled array
        // before being handed to the worker — the caller's buffer is invalid after WriteAsync
        // returns.
        var copy = ArrayPool<byte>.Shared.Rent(data.Length);
        data.Span.CopyTo(copy);
        var length = data.Length;

        return new ValueTask(Task.Run(() =>
        {
            try
            {
                WriteToHandle(_inputWriteHandle, copy, length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(copy);
            }
        }, cancellationToken));
    }

    /// <inheritdoc />
    public ValueTask ResizeAsync(int cols, int rows, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsRunning || _pseudoConsole == IntPtr.Zero)
        {
            return ValueTask.CompletedTask;
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cols);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);

        var size = new ConPtyNative.COORD { X = ClampShort(cols), Y = ClampShort(rows) };
        var hr = ConPtyNative.ResizePseudoConsole(_pseudoConsole, size);
        if (hr != 0)
        {
            throw new Win32Exception(hr, $"ResizePseudoConsole failed with HRESULT 0x{hr:X8}.");
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRunning)
        {
            return ValueTask.CompletedTask;
        }

        IsRunning = false;

        // Signal background threads.
        _shutdownCts?.Cancel();

        // Terminate the child before tearing down the pseudoconsole. Interactive shells
        // (cmd, powershell, bash) never exit on their own when we shut down — they're sitting
        // at a prompt waiting for user input. Without TerminateProcess the read thread stays
        // blocked in ReadFile (the child still holds the pipe's write end via the conpty)
        // and the exit watcher stays blocked in WaitForSingleObject(INFINITE). The Join()
        // calls in DisposeAsync then time out and CleanupHandles closes the process handle
        // out from under the watcher thread.
        //
        // Microsoft's ConPTY guidance is explicit: "ClosePseudoConsole must not be called
        // before all client processes have exited". So we kill first, close second.
        TerminateChildIfAlive();

        if (_pseudoConsole != IntPtr.Zero)
        {
            ConPtyNative.ClosePseudoConsole(_pseudoConsole);
            _pseudoConsole = IntPtr.Zero;
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            IsRunning = false;
            _shutdownCts?.Cancel();

            // Kill the child first so the read thread's ReadFile sees EOF and the exit
            // watcher's WaitForSingleObject returns. Without this, CleanupHandles would
            // close the process / pipe handles while background threads are still using
            // them. See the matching comment in CloseAsync.
            TerminateChildIfAlive();

            if (_pseudoConsole != IntPtr.Zero)
            {
                ConPtyNative.ClosePseudoConsole(_pseudoConsole);
                _pseudoConsole = IntPtr.Zero;
            }

            CleanupHandles();
        }
        finally
        {
            _shutdownCts?.Dispose();
            _shutdownCts = null;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);

        // Give the background threads a moment to notice cancellation. They unblock on the
        // pseudoconsole close above; a short join here keeps Dispose deterministic.
        _readThread?.Join(TimeSpan.FromSeconds(1));
        _exitWatcherThread?.Join(TimeSpan.FromSeconds(1));

        Dispose();
    }

    // ─── Internals ───────────────────────────────────────────────────────

    private void CreatePipes()
    {
        var sa = new ConPtyNative.SECURITY_ATTRIBUTES
        {
            nLength = (uint)Marshal.SizeOf<ConPtyNative.SECURITY_ATTRIBUTES>(),
            lpSecurityDescriptor = IntPtr.Zero,
            bInheritHandle = 0, // we'll mark only the pseudoconsole's ends as inheritable
        };

        if (ConPtyNative.CreatePipe(out var inputReadSide, out var inputWriteSide, ref sa, 0) == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreatePipe (input) failed.");
        }

        if (ConPtyNative.CreatePipe(out var outputReadSide, out var outputWriteSide, ref sa, 0) == 0)
        {
            ConPtyNative.CloseHandle(inputReadSide);
            ConPtyNative.CloseHandle(inputWriteSide);
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreatePipe (output) failed.");
        }

        _inputWriteHandle = inputWriteSide;       // our write end → child stdin
        _outputReadHandle = outputReadSide;       // our read end → child stdout/stderr
        _pseudoConsole = IntPtr.Zero;
        _inheritedInputRead = inputReadSide;       // PTY end (input read)
        _inheritedOutputWrite = outputWriteSide;   // PTY end (output write)
    }

    private IntPtr _inheritedInputRead;
    private IntPtr _inheritedOutputWrite;

    private void CreatePseudoConsole()
    {
        var size = new ConPtyNative.COORD
        {
            X = ClampShort(_options.InitialCols),
            Y = ClampShort(_options.InitialRows),
        };

        var hr = ConPtyNative.CreatePseudoConsole(size, _inheritedInputRead, _inheritedOutputWrite, 0, out var hPC);
        if (hr != 0)
        {
            throw new Win32Exception(hr, $"CreatePseudoConsole failed with HRESULT 0x{hr:X8}.");
        }

        _pseudoConsole = hPC;
    }

    private void BuildAttributeList()
    {
        // First call computes the buffer size needed for one attribute. It returns FALSE with
        // GetLastError == ERROR_INSUFFICIENT_BUFFER and fills lpSize.
        IntPtr size = IntPtr.Zero;
        ConPtyNative.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);

        _attributeList = Marshal.AllocHGlobal(size);
        if (ConPtyNative.InitializeProcThreadAttributeList(_attributeList, 1, 0, ref size) == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "InitializeProcThreadAttributeList failed.");
        }

        // PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE_HANDLE takes the HPCON value directly as
        // lpValue (HPCON is a void* opaque handle — passing &handle would point at our
        // managed slot, which becomes invalid as soon as CreateProcessW returns). The
        // size argument is sizeof(HPCON), i.e. IntPtr.Size.
        if (ConPtyNative.UpdateProcThreadAttribute(
                _attributeList,
                0,
                ConPtyNative.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE_HANDLE,
                _pseudoConsole,
                IntPtr.Size,
                IntPtr.Zero,
                IntPtr.Zero) == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "UpdateProcThreadAttribute failed.");
        }
    }

    private void SpawnChild()
    {
        // STARTF_USESTDHANDLES + INVALID_HANDLE_VALUE for hStdInput/Output/Error is the
        // documented "I'm specifying stdio explicitly, but with no inherited handles" pattern.
        // Without it, even with PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE_HANDLE the kernel still
        // hands the child our parent's stdio handles for non-console APIs (WriteFile etc.),
        // so cmd.exe's prompt and `cmd /c <anything>` output leak to the dotnet console
        // instead of flowing through the pseudoconsole's output pipe. The pseudoconsole
        // attribute then overwrites these sentinel handles with the conhost pipes for both
        // console and file-handle stdio.
        var startupInfo = new ConPtyNative.STARTUPINFOEXW
        {
            StartupInfo = new ConPtyNative.STARTUPINFOW
            {
                cb = (uint)Marshal.SizeOf<ConPtyNative.STARTUPINFOEXW>(),
                dwFlags = ConPtyNative.STARTF_USESTDHANDLES,
                hStdInput = ConPtyNative.INVALID_HANDLE_VALUE,
                hStdOutput = ConPtyNative.INVALID_HANDLE_VALUE,
                hStdError = ConPtyNative.INVALID_HANDLE_VALUE,
            },
            lpAttributeList = _attributeList
        };

        var commandLine = BuildCommandLine(_options.Shell, _options.Arguments);

        // Matches the official Microsoft ConPTY sample
        // (microsoft/terminal samples/ConPTY/EchoCon): lpApplicationName = NULL so
        // CreateProcessW parses the shell out of the command line, bInheritHandles = FALSE
        // since the proc-thread attribute list itself routes the pseudoconsole handles into
        // the child — there's nothing to inherit through the parent's handle table.
        if (ConPtyNative.CreateProcessW(
                lpApplicationName: null,
                lpCommandLine: commandLine,
                lpProcessAttributes: IntPtr.Zero,
                lpThreadAttributes: IntPtr.Zero,
                bInheritHandles: 0,
                dwCreationFlags: ConPtyNative.EXTENDED_STARTUPINFO_PRESENT,
                lpEnvironment: IntPtr.Zero,
                lpCurrentDirectory: _options.WorkingDirectory,
                lpStartupInfo: ref startupInfo,
                lpProcessInformation: out var pi) == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                $"CreateProcessW failed for shell '{_options.Shell}'.");
        }

        _processHandle = pi.hProcess;
        _threadHandle = pi.hThread;
        ProcessId = (int)pi.dwProcessId;
    }

    private void ReleaseInheritedPipeEnds()
    {
        // The pseudoconsole has duplicated these handles; ours are no longer needed.
        if (_inheritedInputRead != IntPtr.Zero)
        {
            ConPtyNative.CloseHandle(_inheritedInputRead);
            _inheritedInputRead = IntPtr.Zero;
        }

        if (_inheritedOutputWrite != IntPtr.Zero)
        {
            ConPtyNative.CloseHandle(_inheritedOutputWrite);
            _inheritedOutputWrite = IntPtr.Zero;
        }
    }

    private void StartReadThread()
    {
        _readThread = new Thread(ReadLoop)
        {
            IsBackground = true,
            Name = "TerminalNinja.ConPty.Read",
        };
        _readThread.Start();
    }

    private void StartExitWatcherThread()
    {
        _exitWatcherThread = new Thread(ExitWatcherLoop)
        {
            IsBackground = true,
            Name = "TerminalNinja.ConPty.ExitWatch",
        };
        _exitWatcherThread.Start();
    }

    private void ReadLoop()
    {
        const int BufferSize = 4096;
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            var handle = _outputReadHandle;
            while (!_disposed && handle != IntPtr.Zero)
            {
                int ok;
                uint bytesRead;
                unsafe
                {
                    fixed (byte* p = buffer)
                    {
                        ok = ConPtyNative.ReadFile(handle, (IntPtr)p, (uint)buffer.Length, out bytesRead, IntPtr.Zero);
                    }
                }

                if (ok == 0)
                {
                    // ReadFile returns FALSE with ERROR_BROKEN_PIPE when the write end has
                    // closed (child exited or we tore down). Anything else is unexpected,
                    // but we still exit the loop cleanly.
                    break;
                }

                if (bytesRead == 0)
                {
                    break;
                }

                var handler = DataReceived;
                if (handler is not null)
                {
                    handler(new ReadOnlyMemory<byte>(buffer, 0, (int)bytesRead));
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private void ExitWatcherLoop()
    {
        var handle = _processHandle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        // Wait until the child exits.
        ConPtyNative.WaitForSingleObject(handle, ConPtyNative.INFINITE);

        ConPtyNative.GetExitCodeProcess(handle, out var exitCode);
        IsRunning = false;
        ProcessExited?.Invoke((int)exitCode);
    }

    private static void WriteToHandle(IntPtr handle, byte[] buffer, int length)
    {
        var offset = 0;
        while (offset < length)
        {
            int ok;
            uint written;
            unsafe
            {
                fixed (byte* p = &buffer[offset])
                {
                    ok = ConPtyNative.WriteFile(handle, (IntPtr)p, (uint)(length - offset), out written, IntPtr.Zero);
                }
            }

            if (ok == 0 || written == 0)
            {
                // Broken pipe or aborted — stop and let the next ProcessExited surface the failure.
                return;
            }

            offset += (int)written;
        }
    }

    /// <summary>
    /// If <see cref="_processHandle"/> still refers to a live process, ends it with
    /// <c>TerminateProcess</c> and waits briefly for the kernel to mark it as exited.
    /// Safe to call when the process has already exited naturally — the initial
    /// <c>WaitForSingleObject</c> with a zero timeout returns <c>WAIT_OBJECT_0</c> and we
    /// take the no-op path.
    /// </summary>
    private void TerminateChildIfAlive()
    {
        if (_processHandle == IntPtr.Zero)
        {
            return;
        }

        var waitResult = ConPtyNative.WaitForSingleObject(_processHandle, 0);
        if (waitResult == ConPtyNative.WAIT_OBJECT_0)
        {
            // Already exited — natural exit path (cmd /c exit 7, whoami, etc.).
            return;
        }

        // Ignore the BOOL — if TerminateProcess fails the child is already dead or in an
        // unrecoverable state. Either way, we still want to proceed with teardown.
        _ = ConPtyNative.TerminateProcess(_processHandle, 1);

        // Give the kernel a brief moment to actually transition the process to "exited"
        // so the exit watcher's WaitForSingleObject(INFINITE) returns before we close the
        // handle. 1s is well over the empirically measured ~10–50 ms it takes for
        // TerminateProcess to settle.
        ConPtyNative.WaitForSingleObject(_processHandle, 1000);
    }

    private void CleanupHandles()
    {
        // Order matters: close pipe handles first so the read thread unblocks. The
        // pseudoconsole is closed earlier in Dispose / CloseAsync.
        if (_outputReadHandle != IntPtr.Zero)
        {
            ConPtyNative.CloseHandle(_outputReadHandle);
            _outputReadHandle = IntPtr.Zero;
        }

        if (_inputWriteHandle != IntPtr.Zero)
        {
            ConPtyNative.CloseHandle(_inputWriteHandle);
            _inputWriteHandle = IntPtr.Zero;
        }

        if (_inheritedInputRead != IntPtr.Zero)
        {
            ConPtyNative.CloseHandle(_inheritedInputRead);
            _inheritedInputRead = IntPtr.Zero;
        }

        if (_inheritedOutputWrite != IntPtr.Zero)
        {
            ConPtyNative.CloseHandle(_inheritedOutputWrite);
            _inheritedOutputWrite = IntPtr.Zero;
        }

        if (_threadHandle != IntPtr.Zero)
        {
            ConPtyNative.CloseHandle(_threadHandle);
            _threadHandle = IntPtr.Zero;
        }

        if (_processHandle != IntPtr.Zero)
        {
            ConPtyNative.CloseHandle(_processHandle);
            _processHandle = IntPtr.Zero;
        }

        if (_attributeList != IntPtr.Zero)
        {
            ConPtyNative.DeleteProcThreadAttributeList(_attributeList);
            Marshal.FreeHGlobal(_attributeList);
            _attributeList = IntPtr.Zero;
        }
    }

    private static short ClampShort(int value) => value switch
    {
        > short.MaxValue => short.MaxValue,
        < 1 => 1,
        _ => (short)value,
    };

    /// <summary>
    /// Builds a Win32 command line: quoted shell path followed by quoted arguments.
    /// Follows the CommandLineToArgvW unquoting rules — backslashes before a quote are escaped,
    /// other backslashes pass through.
    /// </summary>
    private static string BuildCommandLine(string shell, IReadOnlyList<string> arguments)
    {
        var sb = new StringBuilder();
        AppendQuoted(sb, shell);
        for (var i = 0; i < arguments.Count; i++)
        {
            sb.Append(' ');
            AppendQuoted(sb, arguments[i]);
        }

        return sb.ToString();
    }

    private static void AppendQuoted(StringBuilder sb, string arg)
    {
        var needsQuotes = arg.Length == 0 || arg.IndexOfAny([' ', '\t', '"']) >= 0;
        if (!needsQuotes)
        {
            sb.Append(arg);
            return;
        }

        sb.Append('"');
        var backslashes = 0;
        foreach (var ch in arg)
        {
            if (ch == '\\')
            {
                backslashes++;
                continue;
            }

            if (ch == '"')
            {
                // Escape all the backslashes immediately preceding the quote, plus the quote.
                sb.Append('\\', backslashes * 2 + 1);
                sb.Append('"');
                backslashes = 0;
                continue;
            }

            sb.Append('\\', backslashes);
            sb.Append(ch);
            backslashes = 0;
        }

        // Trailing backslashes — double them so the closing quote isn't escaped.
        sb.Append('\\', backslashes * 2);
        sb.Append('"');
    }
}
