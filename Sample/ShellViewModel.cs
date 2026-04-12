using System.Diagnostics;
using TerminalNinja.Controls;
using TerminalNinja.Xaml;
using TerminalNinja.Xaml.Mvvm;
using Sample.Samples.MainMenu;
using Sample.Samples.ProgressBars;
using Sample.Samples.Dialogs;
using Sample.Samples.Buttons;
using Sample.Samples.DataBinding;
using Sample.Samples.Lists;
using Sample.Samples.TextInput;
using Sample.Samples.ListView;
using ComboBoxSample = Sample.Samples.ComboBox;

namespace Sample;

public class ShellViewModel : ViewModelBase, IDisposable
{
    // ─── Navigation ─────────────────────────────────────────────────

    private MainMenuViewModel? _mainMenuViewModel;

    public UIElement? CurrentScreen
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool IsOnMainMenu { get; private set; } = true;

    public void NavigateToMainMenu()
    {
        DisposeCurrentScreen();

        // Reuse the same ViewModel instance so SelectedSample persists across navigations
        _mainMenuViewModel ??= new MainMenuViewModel();
        var screen = TerminalXaml.Load<Border>(XamlLayouts.MainMenuScreen, _mainMenuViewModel);
        WireAndSetScreen(screen);
        IsOnMainMenu = true;
        StatusText = "Select a sample and press Enter";
    }

    public void NavigateToSelectedSample()
    {
        var selected = _mainMenuViewModel?.SelectedSample;
        if (selected != null)
        {
            NavigateToSample(selected);
        }
    }

    public void NavigateToSample(string sampleName)
    {
        DisposeCurrentScreen();

        UIElement screen = sampleName switch
        {
            "Button" => TerminalXaml.Load<Border>(XamlLayouts.ButtonsScreen, new ButtonsViewModel()),
            "CheckBox" => TerminalXaml.Load<Border>(XamlLayouts.CheckBoxScreen),
            "RadioButton" => TerminalXaml.Load<Border>(XamlLayouts.RadioButtonScreen),
            "ComboBox" => TerminalXaml.Load<Border>(XamlLayouts.ComboBoxScreen, new ComboBoxSample.ComboBoxViewModel()),
            "TextBox" => TerminalXaml.Load<Border>(XamlLayouts.TextInputScreen, new TextInputViewModel()),
            "ListBox" => TerminalXaml.Load<Border>(XamlLayouts.ListsScreen, new ListsViewModel()),
            "ListView" => TerminalXaml.Load<Border>(XamlLayouts.ListViewScreen, new ListViewViewModel()),
            "TreeView" => TerminalXaml.Load<Border>(XamlLayouts.TreeViewScreen),
            "TabControl" => TerminalXaml.Load<Border>(XamlLayouts.TabControlScreen),
            "ScrollViewer" => TerminalXaml.Load<Border>(XamlLayouts.ScrollViewerScreen),
            "ProgressBar" => TerminalXaml.Load<Border>(XamlLayouts.ProgressBarsScreen, new ProgressBarsViewModel()),
            "Grid Layout" => TerminalXaml.Load<Border>(XamlLayouts.GridLayoutScreen),
            "StackPanel Layout" => TerminalXaml.Load<Border>(XamlLayouts.StackLayoutScreen),
            "Data Binding" => TerminalXaml.Load<Border>(XamlLayouts.DataBindingScreen, new DataBindingViewModel()),
            "Dialogs" => TerminalXaml.Load<Border>(XamlLayouts.DialogsScreen, new DialogsViewModel()),
            _ => throw new ArgumentException($"Unknown sample: {sampleName}")
        };

        WireAndSetScreen(screen);
        IsOnMainMenu = false;
        StatusText = $"{sampleName} — Press ESC to return";
    }

    private void WireAndSetScreen(UIElement screen)
    {
        var app = TerminalNinja.App.Application.Current;
        app?.WireInvalidation(screen);
        CurrentScreen = screen;

        // Move focus into the new screen's first focusable element
        if (app != null)
        {
            app.FocusManager.ClearFocus();
            var bounds = new TerminalNinja.Primitives.Rect(0, 0, app.Renderer.Width, app.Renderer.Height);
            app.FocusManager.FocusNext(screen, bounds);
        }
    }

    private void DisposeCurrentScreen()
    {
        if (CurrentScreen is FrameworkElement { DataContext: IDisposable disposable })
        {
            disposable.Dispose();
        }
    }

    // ─── Performance Monitoring ─────────────────────────────────────

    public string StatusText
    {
        get;
        set => SetProperty(ref field, value);
    } = "Ready";

    public DateTime CurrentTime
    {
        get;
        set => SetProperty(ref field, value);
    } = DateTime.Now;

    public double MemoryUsageMB
    {
        get;
        set => SetProperty(ref field, value);
    }

    public double CpuUsagePercent
    {
        get;
        set => SetProperty(ref field, value);
    }

    public int CurrentFps
    {
        get;
        set => SetProperty(ref field, value);
    }

    public int TargetFps
    {
        get;
        set => SetProperty(ref field, value);
    }

    public double TimeToFirstRenderMs
    {
        get;
        set => SetProperty(ref field, value);
    }

    private readonly Process? _currentProcess;
    private DateTime _lastCpuTime = DateTime.UtcNow;
    private TimeSpan _lastTotalProcessorTime;
    private readonly Timer _perfTimer;

    public ShellViewModel()
    {
        _currentProcess = Process.GetCurrentProcess();
        _lastTotalProcessorTime = _currentProcess.TotalProcessorTime;

        _perfTimer = new Timer(_ =>
        {
            CurrentTime = DateTime.Now;
            UpdatePerformanceStats();
        }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
    }

    private void UpdatePerformanceStats()
    {
        if (_currentProcess == null)
        {
            return;
        }

        try
        {
            _currentProcess.Refresh();
            MemoryUsageMB = _currentProcess.WorkingSet64 / (1024.0 * 1024.0);

            var currentTime = DateTime.UtcNow;
            var currentTotalProcessorTime = _currentProcess.TotalProcessorTime;
            var cpuUsedMs = (currentTotalProcessorTime - _lastTotalProcessorTime).TotalMilliseconds;
            var totalMsPassed = (currentTime - _lastCpuTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
            CpuUsagePercent = cpuUsageTotal * 100.0;

            _lastCpuTime = currentTime;
            _lastTotalProcessorTime = currentTotalProcessorTime;

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

    public void Dispose()
    {
        _perfTimer.Dispose();
        DisposeCurrentScreen();
        _currentProcess?.Dispose();
        GC.SuppressFinalize(this);
    }
}
