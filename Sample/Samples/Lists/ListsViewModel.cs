using System.Collections.ObjectModel;
using TerminalNinja.Commands;
using TerminalNinja.Xaml.Mvvm;

namespace Sample.Samples.Lists;

public class ListsViewModel : ViewModelBase
{
    private int _itemCounter;

    public ObservableCollection<string> MenuItems { get; } =
    [
        "Dashboard",
        "Messages",
        "Settings",
        "Profile",
        "Help"
    ];

    public string? SelectedMenuItem
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

    public ObservableCollection<LogEntry> LogEntries { get; } =
    [
        new() { Time = DateTime.Now.ToString("HH:mm:ss"), Message = "Lists sample opened" }
    ];

    public ICommand AddItemCommand => field ??= new RelayCommand(() =>
    {
        _itemCounter++;
        var name = $"Item {_itemCounter}";
        MenuItems.Add(name);
        LogEntries.Add(new LogEntry { Time = DateTime.Now.ToString("HH:mm:ss"), Message = $"Added '{name}'" });
    });

    public ICommand RemoveItemCommand => field ??= new RelayCommand(() =>
    {
        if (SelectedMenuItem != null)
        {
            var name = SelectedMenuItem;
            MenuItems.Remove(name);
            LogEntries.Add(new LogEntry { Time = DateTime.Now.ToString("HH:mm:ss"), Message = $"Removed '{name}'" });
            SelectedMenuItem = null;
        }
        else if (MenuItems.Count > 0)
        {
            var name = MenuItems[^1];
            MenuItems.RemoveAt(MenuItems.Count - 1);
            LogEntries.Add(new LogEntry { Time = DateTime.Now.ToString("HH:mm:ss"), Message = $"Removed '{name}'" });
        }
    });
}
