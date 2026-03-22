using System.Collections.ObjectModel;
using System.Diagnostics;
using TerminalNinja.Commands;
using TerminalNinja.DependencySystem;
using TerminalNinja.Primitives;
using TerminalNinja.Xaml.Mvvm;

namespace Sample;

/// <summary>
/// ViewModel for the XAML binding demo.
/// Demonstrates data binding with INotifyPropertyChanged and ICommand.
/// </summary>
public class DemoViewModel : ViewModelBase
{
    /// <summary>
    /// Header text displayed at the top.
    /// </summary>
    public string HeaderText
    {
        get;
        set => SetProperty(ref field, value);
    } = "TerminalNinja MVVM Demo";

    /// <summary>
    /// Main content text.
    /// </summary>
    public string ContentText
    {
        get;
        set => SetProperty(ref field, value);
    } =
        "Welcome to TerminalNinja with Data Binding!\n\nClick the buttons to see binding in action.\n\nThe UI updates automatically!";

    /// <summary>
    /// Status bar text.
    /// </summary>
    public string StatusText
    {
        get;
        set => SetProperty(ref field, value);
    } = "Ready";

    /// <summary>
    /// Total number of button clicks.
    /// </summary>
    public int ClickCount
    {
        get;
        private set => SetProperty(ref field, value);
    } = 0;

    /// <summary>
    /// Command for the New button.
    /// </summary>
    public ICommand NewCommand => field ??= new RelayCommand(OnNew);
    
    public ICommand GCCollect => field ??= new RelayCommand(OnGCCollect);

    /// <summary>
    /// Command for the Open button.
    /// </summary>
    public ICommand OpenCommand => field ??= new RelayCommand(OnOpen);
    
    /// <summary>
    /// Command for the Save button.
    /// </summary>
    public ICommand SaveCommand => field ??= new RelayCommand(OnSave);

    public DateTime CurrentTime
    {
        get;
        set => SetProperty(ref field, value);
    } = DateTime.Now;

    /// <summary>
    /// Memory usage in MB.
    /// </summary>
    public double MemoryUsageMB
    {
        get;
        set => SetProperty(ref field, value);
    }

    /// <summary>
    /// CPU usage percentage (approximate).
    /// </summary>
    public double CpuUsagePercent
    {
        get;
        set => SetProperty(ref field, value);
    }

    /// <summary>
    /// Current frames per second (actual).
    /// </summary>
    public int CurrentFps
    {
        get;
        set => SetProperty(ref field, value);
    }
    
    /// <summary>
    /// Target frames per second.
    /// </summary>
    public int TargetFps
    {
        get;
        set => SetProperty(ref field, value);
    }
    
    /// <summary>
    /// Time to first render in milliseconds.
    /// </summary>
    public double TimeToFirstRenderMs
    {
        get;
        set => SetProperty(ref field, value);
    }

    private readonly Process? _currentProcess;
    private DateTime _lastCpuTime = DateTime.UtcNow;
    private TimeSpan _lastTotalProcessorTime;
    private readonly Timer _timer;
    private readonly Timer _timer2;

    /// <summary>
    /// Items for the ListBox demo.
    /// </summary>
    public ObservableCollection<string> MenuItems { get; } =
    [
        "Dashboard",
        "Messages",
        "Settings",
        "Profile",
        "Help"
    ];

    /// <summary>
    /// Activity log entries displayed in the ActivityLogControl.
    /// </summary>
    public ObservableCollection<LogEntry> LogEntries { get; } =
    [
        new LogEntry { Time = DateTime.Now.ToString("HH:mm:ss"), Message = "Application started" },
        new LogEntry { Time = DateTime.Now.ToString("HH:mm:ss"), Message = "XAML layout loaded" },
        new LogEntry { Time = DateTime.Now.ToString("HH:mm:ss"), Message = "Data binding initialized" }
    ];

    /// <summary>
    /// Currently selected menu item.
    /// </summary>
    public string? SelectedMenuItem
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                StatusText = value != null ? $"Selected: {value}" : "No selection";
            }
        }
    }

    public DemoViewModel()
    {
        // Initialize performance monitoring
        _currentProcess = Process.GetCurrentProcess();
        _lastTotalProcessorTime = _currentProcess.TotalProcessorTime;

        // Timer for time and performance stats
        _timer = new Timer(_ =>
        {
            UpdateBackgroundColor();
        }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(1));
        
        // Timer for time and performance stats
        _timer2 = new Timer(_ =>
        {
            CurrentTime = DateTime.Now;
            UpdatePerformanceStats();
        }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
    }

    public Color BackgroundColor
    {
        get;
        set
        {
            if (value.Equals(field))
            {
                return;
            }

            field = value;
            OnPropertyChanged();
        }
    } = Color.FromOklch(Oklch.FromColor(Color.Green) with { H = 120 });

    private void UpdateBackgroundColor()
    {
        BackgroundColor = Color.FromOklch(Oklch.FromColor(Color.Green) with { H = DateTime.Now.Millisecond / 10d % 360 });
    }

    private void UpdatePerformanceStats()
    {
        if (_currentProcess == null)
        {
            return;
        }

        try
        {
            // Update memory usage
            _currentProcess.Refresh();
            MemoryUsageMB = _currentProcess.WorkingSet64 / (1024.0 * 1024.0);

            // Calculate CPU usage
            var currentTime = DateTime.UtcNow;
            var currentTotalProcessorTime = _currentProcess.TotalProcessorTime;

            var cpuUsedMs = (currentTotalProcessorTime - _lastTotalProcessorTime).TotalMilliseconds;
            var totalMsPassed = (currentTime - _lastCpuTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);

            CpuUsagePercent = cpuUsageTotal * 100.0;

            _lastCpuTime = currentTime;
            _lastTotalProcessorTime = currentTotalProcessorTime;
            
            // Update FPS and time-to-first-render from Application singleton
            var app = TerminalNinja.App.Application.Current;
            if (app != null)
            {
                CurrentFps = app.CurrentFps;
                TargetFps = app.TargetFps;
                
                if (app.TimeToFirstRender.HasValue)
                {
                    TimeToFirstRenderMs = app.TimeToFirstRender.Value.TotalMilliseconds;
                }
            }
        }
        catch
        {
            // Ignore errors in performance monitoring
        }
    }
    
    private void OnNew()
    {
        ClickCount++;
        StatusText = $"New clicked! (Total: {ClickCount})";
        ContentText = "Creating a new document...\n\nData binding automatically updates the UI\nwhen properties change!";
        HeaderText = $"New Document - {DateTime.Now:HH:mm:ss}";
        LogEntries.Add(new LogEntry { Time = DateTime.Now.ToString("HH:mm:ss"), Message = "New document created" });
    }

    private static void OnGCCollect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
    
    private void OnOpen()
    {
        ClickCount++;
        StatusText = $"Open clicked! (Total: {ClickCount})";
        ContentText = "Opening a document...\n\nNotice how all bound properties\nupdate in real-time!";
        HeaderText = $"Open File - {DateTime.Now:HH:mm:ss}";
        LogEntries.Add(new LogEntry { Time = DateTime.Now.ToString("HH:mm:ss"), Message = "File opened" });
    }
    
    private void OnSave()
    {
        ClickCount++;
        StatusText = $"Save clicked! (Total: {ClickCount})";
        ContentText = "Saving document...\n\nThe ICommand pattern works perfectly\nwith data binding!";
        HeaderText = $"Saved - {DateTime.Now:HH:mm:ss}";
        LogEntries.Add(new LogEntry { Time = DateTime.Now.ToString("HH:mm:ss"), Message = "Document saved" });
    }
}
