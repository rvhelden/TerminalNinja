namespace TerminalNinja.App;

/// <summary>
/// Watches a directory for .xaml file changes and invokes a callback with debouncing.
/// Used by <see cref="Application.EnableHotReload"/> for live XAML editing during development.
/// </summary>
public sealed class HotReloadWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly Action<string> _onFileChanged;
    private readonly Timer _debounceTimer;
    private string? _pendingFile;

    /// <summary>
    /// Creates a hot reload watcher for the specified directory.
    /// </summary>
    /// <param name="directory">Root directory to watch (recursively) for .xaml changes.</param>
    /// <param name="onFileChanged">Callback with the full path of the changed file.</param>
    public HotReloadWatcher(string directory, Action<string> onFileChanged)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(onFileChanged);

        _onFileChanged = onFileChanged;
        _debounceTimer = new Timer(OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);

        _watcher = new FileSystemWatcher(directory)
        {
            Filter = "*.xaml",
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime
        };

        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
    }

    /// <summary>Starts watching for file changes.</summary>
    public void Start()
    {
        _watcher.EnableRaisingEvents = true;
    }

    /// <summary>Stops watching.</summary>
    public void Stop()
    {
        _watcher.EnableRaisingEvents = false;
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        // Debounce: editors often write multiple times (save + metadata update)
        _pendingFile = e.FullPath;
        _debounceTimer.Change(300, Timeout.Infinite);
    }

    private void OnDebounceElapsed(object? state)
    {
        var file = _pendingFile;
        if (file != null)
        {
            _pendingFile = null;
            try
            {
                _onFileChanged(file);
            }
            catch
            {
                // Swallow errors — the Application layer handles error reporting
            }
        }
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _debounceTimer.Dispose();
    }
}
