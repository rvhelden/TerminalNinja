using TerminalNinja.Commands;
using TerminalNinja.Xaml.Mvvm;

namespace Sample.Samples.Buttons;

public class ButtonsViewModel : ViewModelBase
{
    public int ClickCount
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public string LastAction
    {
        get;
        set => SetProperty(ref field, value);
    } = "No button clicked yet";

    public ICommand NewCommand => field ??= new RelayCommand(() =>
    {
        ClickCount++;
        LastAction = $"New clicked (total: {ClickCount})";
    });

    public ICommand OpenCommand => field ??= new RelayCommand(() =>
    {
        ClickCount++;
        LastAction = $"Open clicked (total: {ClickCount})";
    });

    public ICommand SaveCommand => field ??= new RelayCommand(() =>
    {
        ClickCount++;
        LastAction = $"Save clicked (total: {ClickCount})";
    });

    public ICommand DeleteCommand => field ??= new RelayCommand(() =>
    {
        ClickCount++;
        LastAction = $"Delete clicked (total: {ClickCount})";
    });

    public ICommand GcCollectCommand => field ??= new RelayCommand(() =>
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        ClickCount++;
        LastAction = $"GC Collect triggered (total: {ClickCount})";
    });
}
