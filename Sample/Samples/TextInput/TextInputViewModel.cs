using TerminalNinja.Xaml.Mvvm;

namespace Sample.Samples.TextInput;

public class TextInputViewModel : ViewModelBase
{
    public string InputText
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                StatusText = $"  Characters: {value?.Length ?? 0}";
                OutputText = $"Echo: {value}";
            }
        }
    } = "";

    public string StatusText
    {
        get;
        set => SetProperty(ref field, value);
    } = "  Characters: 0";

    public string OutputText
    {
        get;
        set => SetProperty(ref field, value);
    } = "Echo: ";

    public string NotesText
    {
        get;
        set => SetProperty(ref field, value);
    } = "";
}
