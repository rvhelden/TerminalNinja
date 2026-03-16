using System.ComponentModel;
using TerminalNinja.Commands;
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

    public DemoViewModel()
    {
        _ = new Timer(_ =>
        {
            CurrentTime = DateTime.Now;
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }
    
    private void OnNew()
    {
        ClickCount++;
        StatusText = $"New clicked! (Total: {ClickCount})";
        ContentText = "Creating a new document...\n\nData binding automatically updates the UI\nwhen properties change!";
        HeaderText = $"New Document - {DateTime.Now:HH:mm:ss}";
    }
    
    private void OnOpen()
    {
        ClickCount++;
        StatusText = $"Open clicked! (Total: {ClickCount})";
        ContentText = "Opening a document...\n\nNotice how all bound properties\nupdate in real-time!";
        HeaderText = $"Open File - {DateTime.Now:HH:mm:ss}";
    }
    
    private void OnSave()
    {
        ClickCount++;
        StatusText = $"Save clicked! (Total: {ClickCount})";
        ContentText = "Saving document...\n\nThe ICommand pattern works perfectly\nwith data binding!";
        HeaderText = $"Saved - {DateTime.Now:HH:mm:ss}";
    }
}
