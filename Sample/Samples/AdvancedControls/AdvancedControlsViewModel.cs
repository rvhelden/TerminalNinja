using System.Collections.ObjectModel;
using TerminalNinja.Xaml.Mvvm;

namespace Sample.Samples.AdvancedControls;

public class AdvancedControlsViewModel : ViewModelBase
{
    public ObservableCollection<string> Files { get; } =
    [
        "Program.cs",
        "README.md",
        "appsettings.json",
        "Startup.cs",
        "Controllers/HomeController.cs"
    ];
}
