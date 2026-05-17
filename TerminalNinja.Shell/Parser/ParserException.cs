namespace TerminalNinja.Shell.Parser;

/// <summary>Thrown when the parser can't make progress on a token stream.</summary>
public sealed class ParserException : Exception
{
    /// <summary>True when the input ended before the current expression could complete and the REPL should keep reading.</summary>
    public bool IsIncomplete { get; }

    /// <summary>1-based line of the offending token.</summary>
    public int Line { get; }

    /// <summary>1-based column of the offending token.</summary>
    public int Column { get; }

    /// <summary>Create a parser exception.</summary>
    public ParserException(string message, int line, int column, bool isIncomplete)
        : base($"({line}:{column}) {message}")
    {
        Line = line;
        Column = column;
        IsIncomplete = isIncomplete;
    }
}
