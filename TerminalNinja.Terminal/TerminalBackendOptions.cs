namespace TerminalNinja.Terminal;

/// <summary>
/// Configuration for spawning a child process inside a pseudo-terminal. Passed to
/// concrete <see cref="ITerminalBackend"/> implementations at construction.
/// </summary>
/// <param name="Shell">
/// Absolute path or name of the executable to run (e.g. <c>C:\Windows\System32\cmd.exe</c>,
/// <c>powershell</c>, <c>/bin/bash</c>, <c>/usr/bin/zsh</c>). Resolved through PATH if not
/// absolute.
/// </param>
/// <param name="Arguments">Command-line arguments passed to <paramref name="Shell"/>.</param>
/// <param name="InitialCols">Initial pseudo-terminal width in cells (must be ≥ 1).</param>
/// <param name="InitialRows">Initial pseudo-terminal height in cells (must be ≥ 1).</param>
/// <param name="WorkingDirectory">
/// Initial working directory for the child. <see langword="null"/> inherits the parent's CWD.
/// </param>
/// <param name="Environment">
/// Environment variables to set on the child. <see langword="null"/> inherits the parent's
/// environment unchanged. When provided, the entries are added on top of the parent's
/// environment (use a value of empty string to clear an inherited variable).
/// </param>
public sealed record TerminalBackendOptions(
    string Shell,
    IReadOnlyList<string> Arguments,
    int InitialCols,
    int InitialRows,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string>? Environment = null)
{
    /// <summary>
    /// Validates option values; throws <see cref="ArgumentException"/> /
    /// <see cref="ArgumentOutOfRangeException"/> on illegal combinations.
    /// </summary>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Shell);
        ArgumentNullException.ThrowIfNull(Arguments);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(InitialCols);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(InitialRows);
    }
}
