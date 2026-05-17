using System.Threading.Tasks;
using TerminalNinja.Terminal;
using TerminalNinja.Xaml.Mvvm;

namespace Sample.Samples.Terminal;

/// <summary>
/// Demo for <see cref="TerminalView"/>: spawns the platform shell inside a PTY and
/// pipes it into a 24×80 embedded terminal. Keystrokes routed to the focused
/// <see cref="TerminalView"/> are encoded by <see cref="KeyEventEncoder"/> and written
/// to the backend; bytes from the child redraw the screen on the render thread.
/// </summary>
public sealed class TerminalSampleViewModel : ViewModelBase, IDisposable
{
    private const int Rows = 24;
    private const int Cols = 80;

    private ITerminalBackend? _backend;
    private bool _disposed;

    public TerminalView Terminal { get; }

    public string StatusMessage
    {
        get;
        private set => SetProperty(ref field, value);
    } = "Starting shell…";

    public TerminalSampleViewModel()
    {
        Terminal = new TerminalView(Rows, Cols);

        try
        {
            var options = new TerminalBackendOptions(
                Shell: DefaultShell(),
                Arguments: Array.Empty<string>(),
                InitialCols: Cols,
                InitialRows: Rows);

            _backend = TerminalBackend.Create(options);
            _backend.ProcessExited += OnProcessExited;
            Terminal.Backend = _backend;

            _ = StartBackendAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Backend unavailable: {ex.Message}";
        }
    }

    private async Task StartBackendAsync()
    {
        try
        {
            if (_backend is null)
            {
                return;
            }

            await _backend.StartAsync().ConfigureAwait(false);
            StatusMessage = $"Connected to {DefaultShell()} (PID {_backend.ProcessId}). Type to interact — ESC returns to menu.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to start shell: {ex.Message}";
        }
    }

    private void OnProcessExited(int exitCode)
    {
        StatusMessage = $"Shell exited (code {exitCode}). Press ESC to return.";
    }

    private static string DefaultShell()
    {
        if (OperatingSystem.IsWindows())
        {
            return Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
        }

        return Environment.GetEnvironmentVariable("SHELL") ?? "/bin/bash";
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_backend is not null)
        {
            _backend.ProcessExited -= OnProcessExited;
            Terminal.Backend = null;

            try
            {
                _backend.Dispose();
            }
            catch
            {
                // Swallow — broken-pipe after child exit is expected.
            }

            _backend = null;
        }
    }
}
