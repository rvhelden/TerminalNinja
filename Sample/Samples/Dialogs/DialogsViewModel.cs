using TerminalNinja.Commands;
using TerminalNinja.Controls;
using TerminalNinja.Primitives;
using TerminalNinja.Styling;
using TerminalNinja.Themes;
using TerminalNinja.Xaml.Mvvm;

namespace Sample.Samples.Dialogs;

public class DialogsViewModel : ViewModelBase
{
    public string LastResult
    {
        get;
        set => SetProperty(ref field, value);
    } = "No dialog shown yet";

    public int DialogCount
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public ICommand ShowDialogCommand => field ??= new RelayCommand(OnShowDialog);

    private async void OnShowDialog()
    {
        DialogCount++;

        var app = TerminalNinja.App.Application.Current;
        var dialogBg = ResolveThemeColor(app, ThemeResourceKeys.DialogBackgroundColor, new Color(37, 37, 38));
        var dialogFg = ResolveThemeColor(app, ThemeResourceKeys.DialogForegroundColor, new Color(212, 212, 212));
        var dialogBorder = ResolveThemeColor(app, ThemeResourceKeys.DialogBorderColor, new Color(86, 156, 214));
        var accentColor = ResolveThemeColor(app, ThemeResourceKeys.AccentColor, Color.Cyan);
        var buttonBg = ResolveThemeColor(app, ThemeResourceKeys.ButtonBackgroundColor, new Color(60, 60, 60));
        var buttonFg = ResolveThemeColor(app, ThemeResourceKeys.ButtonForegroundColor, new Color(212, 212, 212));
        var buttonFocus = ResolveThemeColor(app, ThemeResourceKeys.ButtonFocusColor, Color.Cyan);

        var okButton = new Button
        {
            Text = "OK",
            Width = Size.Absolute(12),
            Height = Size.Absolute(3),
            Foreground = buttonFg,
            Background = buttonBg,
            FocusColor = buttonFocus,
            HoverColor = Color.Green,
            TabIndex = 0
        };
        StackPanel.SetSizeMode(okButton, ChildSizeMode.Auto);

        var cancelButton = new Button
        {
            Text = "Cancel",
            Width = Size.Absolute(12),
            Height = Size.Absolute(3),
            Foreground = buttonFg,
            Background = buttonBg,
            FocusColor = buttonFocus,
            HoverColor = Color.Red,
            TabIndex = 1
        };
        StackPanel.SetSizeMode(cancelButton, ChildSizeMode.Auto);

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal };
        StackPanel.SetSizeMode(buttonPanel, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(buttonPanel, 3);
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);

        var messageText = new TextBlock
        {
            Text = "Are you sure you want to proceed?\n\nThis is a modal dialog demo.\nThe background is dimmed and input\nis restricted to this window.",
            Foreground = dialogFg,
            Background = dialogBg,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Padding = new Thickness(2, 1, 2, 1)
        };
        StackPanel.SetSizeMode(messageText, ChildSizeMode.Stretch);

        var titleText = new TextBlock
        {
            Text = " Confirm Action",
            Foreground = accentColor,
            Background = dialogBg
        };
        StackPanel.SetSizeMode(titleText, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(titleText, 1);

        var contentPanel = new StackPanel { Orientation = Orientation.Vertical };
        contentPanel.Children.Add(titleText);
        contentPanel.Children.Add(messageText);
        contentPanel.Children.Add(buttonPanel);

        var dialogWindow = new Window
        {
            Width = Size.Absolute(44),
            Height = Size.Absolute(14),
            Content = new Border
            {
                Background = dialogBg,
                BorderBrush = dialogFg,
                BorderStyle = BorderStyle.Rounded(dialogBorder),
                Child = contentPanel
            }
        };

        okButton.Click += () => dialogWindow.DialogResult = true;
        cancelButton.Click += () => dialogWindow.DialogResult = false;

        var result = await dialogWindow.ShowDialogAsync();

        LastResult = result switch
        {
            true => $"Dialog #{DialogCount}: Confirmed",
            false => $"Dialog #{DialogCount}: Cancelled",
            null => $"Dialog #{DialogCount}: Dismissed"
        };
    }

    private static Color ResolveThemeColor(TerminalNinja.App.Application? app, string key, Color fallback)
    {
        if (app != null && app.Resources.TryGetValue(key, out var value) && value is Color color)
        {
            return color;
        }
        return fallback;
    }
}
