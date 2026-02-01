using System.ComponentModel;
using TerminalNinja.Core.Commands;
using TerminalNinja.Xaml.Mvvm;

namespace Sample;

/// <summary>
/// ViewModel for the XAML binding demo.
/// Demonstrates data binding with INotifyPropertyChanged and ICommand.
/// </summary>
public class DemoViewModel : ViewModelBase
{
    private string _headerText = "TerminalNinja MVVM Demo";
    private string _contentText = "Welcome to TerminalNinja with Data Binding!\n\nClick the buttons to see binding in action.\n\nThe UI updates automatically!";
    private string _statusText = "Ready";
    private int _clickCount = 0;
    
    // Commands
    private ICommand? _newCommand;
    private ICommand? _openCommand;
    private ICommand? _saveCommand;
    
    /// <summary>
    /// Header text displayed at the top.
    /// </summary>
    public string HeaderText
    {
        get => _headerText;
        set => SetProperty(ref _headerText, value);
    }
    
    /// <summary>
    /// Main content text.
    /// </summary>
    public string ContentText
    {
        get => _contentText;
        set => SetProperty(ref _contentText, value);
    }
    
    /// <summary>
    /// Status bar text.
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }
    
    /// <summary>
    /// Total number of button clicks.
    /// </summary>
    public int ClickCount
    {
        get => _clickCount;
        private set => SetProperty(ref _clickCount, value);
    }
    
    /// <summary>
    /// Command for the New button.
    /// </summary>
    public ICommand NewCommand => _newCommand ??= new RelayCommand(OnNew);
    
    /// <summary>
    /// Command for the Open button.
    /// </summary>
    public ICommand OpenCommand => _openCommand ??= new RelayCommand(OnOpen);
    
    /// <summary>
    /// Command for the Save button.
    /// </summary>
    public ICommand SaveCommand => _saveCommand ??= new RelayCommand(OnSave);
    
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
