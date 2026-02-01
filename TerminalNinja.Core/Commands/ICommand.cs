namespace TerminalNinja.Core.Commands;

/// <summary>
/// Represents a command that can be executed.
/// </summary>
public interface ICommand
{
    /// <summary>
    /// Determines whether the command can execute in its current state.
    /// </summary>
    /// <param name="parameter">Optional parameter for the command.</param>
    /// <returns>true if the command can execute; otherwise, false.</returns>
    bool CanExecute(object? parameter);
    
    /// <summary>
    /// Executes the command.
    /// </summary>
    /// <param name="parameter">Optional parameter for the command.</param>
    void Execute(object? parameter);
    
    /// <summary>
    /// Occurs when changes occur that affect whether the command should execute.
    /// </summary>
    event EventHandler? CanExecuteChanged;
}
