using TerminalNinja.Commands;

namespace TerminalNinja.Controls;

/// <summary>
/// Abstract base class for button controls that support command binding and click events.
/// Corresponds to WPF's System.Windows.Controls.Primitives.ButtonBase.
/// </summary>
public abstract class ButtonBase : ContentControl
{
    private ICommand? _command;

    /// <summary>Gets or sets the command to execute when the button is clicked.</summary>
    public ICommand? Command
    {
        get => _command;
        set
        {
            if (_command != null)
                _command.CanExecuteChanged -= OnCanExecuteChanged;
            SetProperty(ref _command, value, invalidate: false);
            if (_command != null)
                _command.CanExecuteChanged += OnCanExecuteChanged;
            UpdateCanExecute();
        }
    }

    private object? _commandParameter;
    /// <summary>Gets or sets the parameter to pass to the Command.</summary>
    public object? CommandParameter
    {
        get => _commandParameter;
        set
        {
            SetProperty(ref _commandParameter, value, invalidate: false);
            UpdateCanExecute();
        }
    }

    /// <summary>Event raised when the button is clicked.</summary>
    public event Action? Click;

    private void OnCanExecuteChanged(object? sender, EventArgs e) => UpdateCanExecute();

    /// <summary>
    /// Updates the IsEnabled state based on Command.CanExecute.
    /// </summary>
    protected virtual void UpdateCanExecute()
    {
        IsEnabled = _command?.CanExecute(_commandParameter) ?? true;
    }

    /// <summary>
    /// Raises the Click event and executes the Command if available and enabled.
    /// </summary>
    protected void RaiseClick()
    {
        if (!IsEnabled) return;

        // Execute command if available
        if (Command?.CanExecute(CommandParameter) == true)
            Command.Execute(CommandParameter);

        // Also raise Click event
        Click?.Invoke();
    }
}
