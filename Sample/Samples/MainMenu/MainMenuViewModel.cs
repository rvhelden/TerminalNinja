using System.Collections.ObjectModel;
using TerminalNinja.Xaml.Mvvm;

namespace Sample.Samples.MainMenu;

public class MainMenuViewModel : ViewModelBase
{
    public ObservableCollection<string> Samples { get; } =
    [
        "Progress Bars",
        "Dialogs",
        "Buttons",
        "Data Binding",
        "Lists",
        "Grid Layout",
        "StackPanel Layout",
        "ScrollViewer",
        "Text Input",
        "Form Controls"
    ];

    public string? SelectedSample
    {
        get;
        set => SetProperty(ref field, value);
    }

    public MainMenuViewModel()
    {
        SelectedSample = Samples[0];
    }
}
