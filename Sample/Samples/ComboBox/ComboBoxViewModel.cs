using System.Collections.ObjectModel;
using TerminalNinja.Xaml.Mvvm;

namespace Sample.Samples.ComboBox;

public class ComboBoxViewModel : ViewModelBase
{
    public ObservableCollection<string> Fonts { get; } =
    [
        "JetBrains Mono",
        "Cascadia Code",
        "Fira Code",
        "Source Code Pro",
        "Consolas"
    ];

    public string? SelectedFont
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                FontStatus = $"  Selected: {value ?? "None"}";
        }
    }

    public string FontStatus
    {
        get;
        set => SetProperty(ref field, value);
    } = "  Selected: None";
}
