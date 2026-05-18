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

    /// <summary>Length of the offending token in characters; defaults to 1 for end-of-input cases where no token is available.</summary>
    public int Length { get; }

    /// <summary>Create a parser exception with a single-point location.</summary>
    public ParserException(string message, int line, int column, bool isIncomplete)
        : this(message, line, column, length: 1, isIncomplete) { }

    /// <summary>Create a parser exception that knows the offending token's length.</summary>
    public ParserException(string message, int line, int column, int length, bool isIncomplete)
        : base($"({line}:{column}) {message}")
    {
        Line = line;
        Column = column;
        Length = length < 1 ? 1 : length;
        IsIncomplete = isIncomplete;
    }
}
