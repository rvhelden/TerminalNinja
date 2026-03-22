using TerminalNinja.Commands;

namespace TerminalNinja.Controls;

/// <summary>
/// Abstract base class for button controls that support command binding and click events.
/// Corresponds to WPF's System.Windows.Controls.Primitives.ButtonBase.
/// </summary>
public abstract class ButtonBase : ContentControl
{
    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(ButtonBase),
            new PropertyMetadata(null, OnCommandChanged));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(ButtonBase),
            new PropertyMetadata(null, OnCommandParameterChanged));

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var button = (ButtonBase)d;
        if (e.OldValue is ICommand oldCmd)
        {
            oldCmd.CanExecuteChanged -= button.OnCanExecuteChanged;
        }

        if (e.NewValue is ICommand newCmd)
        {
            newCmd.CanExecuteChanged += button.OnCanExecuteChanged;
        }

        button.UpdateCanExecute();
    }

    private static void OnCommandParameterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ButtonBase)d).UpdateCanExecute();
    }

    /// <summary>Gets or sets the command to execute when the button is clicked.</summary>
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>Gets or sets the parameter to pass to the Command.</summary>
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    /// <summary>Event raised when the button is clicked.</summary>
    public event Action? Click;

    private void OnCanExecuteChanged(object? sender, EventArgs e) => UpdateCanExecute();

    /// <summary>
    /// Updates the IsEnabled state based on Command.CanExecute.
    /// </summary>
    protected virtual void UpdateCanExecute()
    {
        IsEnabled = Command?.CanExecute(CommandParameter) ?? true;
    }

    /// <summary>
    /// Raises the Click event and executes the Command if available and enabled.
    /// </summary>
    protected void RaiseClick()
    {
        if (!IsEnabled)
        {
            return;
        }

        // Execute command if available
        if (Command?.CanExecute(CommandParameter) == true)
        {
            Command.Execute(CommandParameter);
        }

        // Also raise Click event
        Click?.Invoke();
    }
}
