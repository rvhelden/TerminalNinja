using System.Collections.ObjectModel;
using TerminalNinja.Xaml.Mvvm;

namespace Sample.Samples.ListView;

public class ListViewViewModel : ViewModelBase
{
    public ObservableCollection<string> Files { get; } =
    [
        "Program.cs",
        "README.md",
        "appsettings.json",
        "Startup.cs",
        "Controllers/HomeController.cs",
        "Models/User.cs",
        "Views/Index.cshtml"
    ];
}
