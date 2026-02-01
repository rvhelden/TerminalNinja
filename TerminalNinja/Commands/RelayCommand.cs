namespace TerminalNinja.Commands;

/// <summary>
/// A command that relays its execution to delegate methods.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;
    
    /// <summary>
    /// Creates a new command with no parameter.
    /// </summary>
    public RelayCommand(Action execute) : this(_ => execute(), null)
    {
    }
    
    /// <summary>
    /// Creates a new command with no parameter and a canExecute predicate.
    /// </summary>
    public RelayCommand(Action execute, Func<bool> canExecute) 
        : this(_ => execute(), _ => canExecute())
    {
    }
    
    /// <summary>
    /// Creates a new command.
    /// </summary>
    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }
    
    /// <inheritdoc />
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    
    /// <inheritdoc />
    public void Execute(object? parameter) => _execute(parameter);
    
    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;
    
    /// <summary>
    /// Raises the CanExecuteChanged event.
    /// </summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
