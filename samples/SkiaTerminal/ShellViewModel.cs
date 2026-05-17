using System.Threading.Tasks;
using TerminalNinja.Terminal;
using TerminalNinja.Xaml.Mvvm;

namespace SkiaTerminal;

/// <summary>
/// Backs <c>ShellLayout.xaml</c>: owns the <see cref="TerminalView"/> that the layout
/// hosts via <c>ContentControl</c>, exposes a status string for the header, and manages
/// the <see cref="ITerminalBackend"/> lifetime.
/// </summary>
public sealed class ShellViewModel : ViewModelBase, IDisposable
{
    public const int Rows = 24;
    public const int Cols = 120;

    private ITerminalBackend? _backend;
    private bool _disposed;

    public TerminalView Terminal { get; } = new(Rows, Cols);

    public string StatusText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "Starting shell…";

    public ShellViewModel()
    {
        try
        {
            _backend = TerminalBackend.Create(new TerminalBackendOptions(
                Shell: DefaultShell(),
                Arguments: Array.Empty<string>(),
                InitialCols: Cols,
                InitialRows: Rows));

            _backend.ProcessExited += code => StatusText = $"Shell exited (code {code}). Close the window to quit.";
            Terminal.Backend = _backend;

            _ = StartBackendAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Backend unavailable: {ex.Message}";
        }
    }

    private async Task StartBackendAsync()
    {
        try
        {
            if (_backend is null) return;
            await _backend.StartAsync().ConfigureAwait(false);
            StatusText = $"Connected to {DefaultShell()} (PID {_backend.ProcessId}). Window-close quits.";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to start shell: {ex.Message}";
        }
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
        if (_disposed) return;
        _disposed = true;

        if (_backend is not null)
        {
            Terminal.Backend = null;
            try { _backend.Dispose(); }
            catch { /* broken pipe after child exit is expected */ }
            _backend = null;
        }
    }
}
