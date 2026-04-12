using System.Collections.ObjectModel;
using TerminalNinja.Xaml.Mvvm;

namespace Sample.Samples.MainMenu;

public class MainMenuViewModel : ViewModelBase
{
    public ObservableCollection<string> Samples { get; } =
    [
        "Button",
        "CheckBox",
        "RadioButton",
        "ComboBox",
        "TextBox",
        "ListBox",
        "ListView",
        "TreeView",
        "TabControl",
        "ScrollViewer",
        "ProgressBar",
        "Grid Layout",
        "StackPanel Layout",
        "Data Binding",
        "Dialogs"
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
