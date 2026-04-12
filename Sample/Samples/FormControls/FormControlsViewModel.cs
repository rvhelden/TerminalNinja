using System.Collections.ObjectModel;
using TerminalNinja.Xaml.Mvvm;

namespace Sample.Samples.FormControls;

public class FormControlsViewModel : ViewModelBase
{
    public bool IsNotificationsEnabled
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                UpdateSummary();
        }
    } = true;

    public bool IsDarkMode
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                UpdateSummary();
        }
    } = true;

    public bool IsAutoSave
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                UpdateSummary();
        }
    }

    public ObservableCollection<string> Colors { get; } =
    [
        "Red",
        "Green",
        "Blue",
        "Yellow",
        "Cyan",
        "Magenta"
    ];

    public string? SelectedColor
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                StatusText = value != null ? $"  Selected color: {value}" : "  No color selected";
                UpdateSummary();
            }
        }
    }

    public string StatusText
    {
        get;
        set => SetProperty(ref field, value);
    } = "  No color selected";

    public string SummaryText
    {
        get;
        set => SetProperty(ref field, value);
    } = "";

    public FormControlsViewModel()
    {
        SelectedColor = "Blue";
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var parts = new List<string>();
        if (IsNotificationsEnabled) parts.Add("Notifications");
        if (IsDarkMode) parts.Add("Dark");
        if (IsAutoSave) parts.Add("AutoSave");
        var flags = parts.Count > 0 ? string.Join(", ", parts) : "None";
        SummaryText = $"  Flags: {flags} | Color: {SelectedColor ?? "None"}";
    }
}
