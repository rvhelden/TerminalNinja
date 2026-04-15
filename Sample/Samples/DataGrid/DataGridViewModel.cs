using System.Collections.ObjectModel;
using TerminalNinja.Aot;
using TerminalNinja.Xaml.Mvvm;

namespace Sample.Samples.DataGrid;

public class DataGridViewModel : ViewModelBase
{
    public ObservableCollection<Employee> Employees { get; } =
    [
        new(1, "Alice", "Engineering", true),
        new(2, "Bob", "Marketing", false),
        new(3, "Charlie", "Design", true),
        new(4, "Diana", "Engineering", true),
        new(5, "Edward", "Sales", false),
        new(6, "Fiona", "Design", true),
        new(7, "George", "Marketing", false),
        new(8, "Hannah", "Engineering", true),
        new(9, "Ivan", "Sales", false),
        new(10, "Julia", "Design", true),
    ];
}

[BindableObject]
public record Employee(int Id, string Name, string Department, bool Active);
