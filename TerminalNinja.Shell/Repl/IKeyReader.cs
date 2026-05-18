namespace TerminalNinja.Shell.Repl;

/// <summary>
/// Reads one key at a time from the user. Abstracted from <see cref="Console.ReadKey(bool)"/>
/// so the <see cref="LineEditor"/> can be unit-tested without a real terminal.
/// </summary>
public interface IKeyReader
{
    /// <summary>Block until a key press is available and return it.</summary>
    ConsoleKeyInfo ReadKey();
}

/// <summary>Production <see cref="IKeyReader"/> that reads from the actual console.</summary>
public sealed class ConsoleKeyReader : IKeyReader
{
    /// <inheritdoc />
    public ConsoleKeyInfo ReadKey() => Console.ReadKey(intercept: true);
}
