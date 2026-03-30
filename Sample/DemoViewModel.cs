using System.Collections.ObjectModel;
using System.Diagnostics;
using TerminalNinja.Commands;
using TerminalNinja.Controls;
using TerminalNinja.Primitives;
using TerminalNinja.Styling;
using TerminalNinja.Themes;
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

    /// <summary>
    /// Command for the Dialog button. Shows a modal confirm dialog.
    /// </summary>
    public ICommand ShowDialogCommand => field ??= new RelayCommand(OnShowDialog);

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

    /// <summary>
    /// Progress value that cycles from 0 to 100 over time.
    /// Demonstrates the ProgressBar control with data binding.
    /// </summary>
    public double ProgressValue
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
            UpdateProgress();
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

    private void UpdateProgress()
    {
        // Cycle progress from 0 to 100 and back
        ProgressValue = (ProgressValue + 2) % 102;
        if (ProgressValue > 100) ProgressValue = 0;
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

    private async void OnShowDialog()
    {
        ClickCount++;
        LogEntries.Add(new LogEntry { Time = DateTime.Now.ToString("HH:mm:ss"), Message = "Opening dialog..." });

        // Resolve theme colors for the dialog, with sensible fallbacks
        var app = TerminalNinja.App.Application.Current;
        var dialogBg = ResolveThemeColor(app, ThemeResourceKeys.DialogBackgroundColor, new Color(37, 37, 38));
        var dialogFg = ResolveThemeColor(app, ThemeResourceKeys.DialogForegroundColor, new Color(212, 212, 212));
        var dialogBorder = ResolveThemeColor(app, ThemeResourceKeys.DialogBorderColor, new Color(86, 156, 214));
        var accentColor = ResolveThemeColor(app, ThemeResourceKeys.AccentColor, Color.Cyan);
        var buttonBg = ResolveThemeColor(app, ThemeResourceKeys.ButtonBackgroundColor, new Color(60, 60, 60));
        var buttonFg = ResolveThemeColor(app, ThemeResourceKeys.ButtonForegroundColor, new Color(212, 212, 212));
        var buttonFocus = ResolveThemeColor(app, ThemeResourceKeys.ButtonFocusColor, Color.Cyan);

        // Build the dialog content programmatically
        var okButton = new Button
        {
            Text = "OK",
            Width = Size.Absolute(12),
            Height = Size.Absolute(3),
            Foreground = buttonFg,
            Background = buttonBg,
            FocusColor = buttonFocus,
            HoverColor = Color.Green,
            TabIndex = 0
        };
        StackPanel.SetSizeMode(okButton, ChildSizeMode.Auto);

        var cancelButton = new Button
        {
            Text = "Cancel",
            Width = Size.Absolute(12),
            Height = Size.Absolute(3),
            Foreground = buttonFg,
            Background = buttonBg,
            FocusColor = buttonFocus,
            HoverColor = Color.Red,
            TabIndex = 1
        };
        StackPanel.SetSizeMode(cancelButton, ChildSizeMode.Auto);

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal };
        StackPanel.SetSizeMode(buttonPanel, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(buttonPanel, 3);
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);

        var messageText = new TextBlock
        {
            Text = "Are you sure you want to proceed?\n\nThis is a modal dialog demo.\nThe background is dimmed and input\nis restricted to this window.",
            Foreground = dialogFg,
            Background = dialogBg,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Padding = new Thickness(2, 1, 2, 1)
        };
        StackPanel.SetSizeMode(messageText, ChildSizeMode.Stretch);

        var titleText = new TextBlock
        {
            Text = " Confirm Action",
            Foreground = accentColor,
            Background = dialogBg
        };
        StackPanel.SetSizeMode(titleText, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(titleText, 1);

        var contentPanel = new StackPanel { Orientation = Orientation.Vertical };
        contentPanel.Children.Add(titleText);
        contentPanel.Children.Add(messageText);
        contentPanel.Children.Add(buttonPanel);

        var dialogWindow = new Window
        {
            Width = Size.Absolute(44),
            Height = Size.Absolute(14),
            Content = new Border
            {
                Background = dialogBg,
                BorderBrush = dialogFg,
                BorderStyle = BorderStyle.Rounded(dialogBorder),
                Child = contentPanel
            }
        };

        // Wire buttons to set DialogResult
        okButton.Click += () => dialogWindow.DialogResult = true;
        cancelButton.Click += () => dialogWindow.DialogResult = false;

        // Show modal and await result
        var result = await dialogWindow.ShowDialogAsync();

        // Handle the result
        var resultText = result switch
        {
            true => "Confirmed",
            false => "Cancelled",
            null => "Dismissed"
        };

        StatusText = $"Dialog result: {resultText}";
        LogEntries.Add(new LogEntry
        {
            Time = DateTime.Now.ToString("HH:mm:ss"),
            Message = $"Dialog closed: {resultText}"
        });
    }

    /// <summary>
    /// Resolves a theme color resource from the application's resource dictionary.
    /// Returns the fallback color if the resource is not found.
    /// </summary>
    private static Color ResolveThemeColor(TerminalNinja.App.Application? app, string key, Color fallback)
    {
        if (app != null && app.Resources.TryGetValue(key, out var value) && value is Color color)
        {
            return color;
        }
        return fallback;
    }
}
