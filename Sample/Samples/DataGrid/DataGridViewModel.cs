using System.Collections.ObjectModel;
using TerminalNinja.Xaml.Mvvm;

namespace Sample.Samples.DataGrid;

public class DataGridViewModel : ViewModelBase
{
    public ObservableCollection<string> Items { get; } =
    [
        "Program.cs",
        "README.md",
        "appsettings.json",
        "Startup.cs",
        "Controllers/HomeController.cs",
        "Models/User.cs"
    ];
}
