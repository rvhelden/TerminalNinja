using TerminalNinja.Commands;
using TerminalNinja.Controls;
using TerminalNinja.Xaml.Mvvm;

namespace Sample.Samples.FolderPicker;

public class FolderPickerViewModel : ViewModelBase
{
    public string SelectedFolder
    {
        get;
        set => SetProperty(ref field, value);
    } = "  No folder selected";

    public ICommand OpenFolderPickerCommand => field ??= new RelayCommand(async () =>
    {
        var path = await TerminalNinja.Controls.FolderPicker.ShowAsync();
        SelectedFolder = path != null ? $"  Selected: {path}" : "  Cancelled";
    });
}
