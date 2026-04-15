using System.Collections.ObjectModel;
using TerminalNinja.Xaml.Mvvm;

namespace Sample.Samples.DataGrid;

public class DataGridViewModel : ViewModelBase
{
    public ObservableCollection<Item> Items { get; } =
    [
        new(1, "Alice"),
        new(2, "Bob"),
        new(3, "Charlie"),
        new(4, "Diana"),
        new(5, "Edward"),
        new(6, "Fiona"),
        new(7, "George"),
        new(8, "Hannah"),
        new(9, "Ivan"),
        new(10, "Julia"),
    ];
}

public record Item(int Id, string Name);