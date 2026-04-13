using TerminalNinja.Commands;
using TerminalNinja.Controls;
using TerminalNinja.Xaml.Mvvm;

namespace Sample.Samples.FilePicker;

public class FilePickerViewModel : ViewModelBase
{
    public string SelectedFile
    {
        get;
        set => SetProperty(ref field, value);
    } = "  No file selected";

    public ICommand OpenFilePickerCommand => field ??= new RelayCommand(async () =>
    {
        var path = await TerminalNinja.Controls.FilePicker.ShowAsync();
        SelectedFile = path != null ? $"  Selected: {path}" : "  Cancelled";
    });
}
