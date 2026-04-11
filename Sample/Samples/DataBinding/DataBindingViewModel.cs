using System.Collections.ObjectModel;
using TerminalNinja.Commands;
using TerminalNinja.Primitives;
using TerminalNinja.Xaml.Mvvm;

namespace Sample.Samples.DataBinding;

public class DataBindingViewModel : ViewModelBase, IDisposable
{
    public string HeaderText
    {
        get;
        set => SetProperty(ref field, value);
    } = "Data Binding Demo";

    public string ContentText
    {
        get;
        set => SetProperty(ref field, value);
    } = "Click buttons to see one-way binding in action.\nSelect an item to see two-way binding.";

    public int ClickCount
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public Color BackgroundColor
    {
        get;
        set
        {
            if (value.Equals(field)) return;
            field = value;
            OnPropertyChanged();
        }
    } = Color.FromOklch(Oklch.FromColor(Color.Green) with { H = 120 });

    public ObservableCollection<string> Items { get; } =
    [
        "One-Way",
        "Two-Way",
        "Converter",
        "RelativeSource"
    ];

    public string? SelectedItem
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                SelectionText = value != null ? $"Selected: {value}" : "No selection";
            }
        }
    }

    public string SelectionText
    {
        get;
        set => SetProperty(ref field, value);
    } = "No selection";

    public ICommand UpdateHeaderCommand => field ??= new RelayCommand(() =>
    {
        ClickCount++;
        HeaderText = $"Updated at {DateTime.Now:HH:mm:ss} (#{ClickCount})";
    });

    public ICommand UpdateContentCommand => field ??= new RelayCommand(() =>
    {
        ClickCount++;
        ContentText = $"Content updated!\nClick count: {ClickCount}\nTime: {DateTime.Now:HH:mm:ss}";
    });

    private readonly Timer _colorTimer;

    public DataBindingViewModel()
    {
        _colorTimer = new Timer(_ =>
        {
            BackgroundColor = Color.FromOklch(Oklch.FromColor(Color.Green) with { H = DateTime.Now.Millisecond / 10d % 360 });
        }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(16));
    }

    public void Dispose()
    {
        _colorTimer.Dispose();
        GC.SuppressFinalize(this);
    }
}
