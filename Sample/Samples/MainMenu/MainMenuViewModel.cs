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
        "Bar Chart",
        "Line Chart",
        "Trace Chart",
        "Flame Graph",
        "Node Graph",
        "DataGrid",
        "ColorPicker",
        "Image",
        "FilePicker",
        "FolderPicker",
        "NumberPicker",
        "DatePicker",
        "TimePicker",
        "DateTimePicker",
        "Grid Layout",
        "StackPanel Layout",
        "Dock Layout",
        "UniformGrid Layout",
        "Wrap Layout",
        "Data Binding",
        "Dialogs",
        "Terminal"
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
