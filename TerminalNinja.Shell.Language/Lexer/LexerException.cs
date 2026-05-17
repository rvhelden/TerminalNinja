namespace TerminalNinja.Shell.Lexer;

/// <summary>Thrown when the lexer cannot continue. <see cref="IsIncomplete"/> distinguishes a partial input (REPL should keep reading) from a genuine syntax error.</summary>
public sealed class LexerException : Exception
{
    /// <summary>True when the input ended in the middle of a multi-character construct (string, interpolation, <c>pwsh</c> block) and the REPL should ask for more input rather than report an error.</summary>
    public bool IsIncomplete { get; }

    /// <summary>1-based line number where the problem was detected.</summary>
    public int Line { get; }

    /// <summary>1-based column number where the problem was detected.</summary>
    public int Column { get; }

    /// <summary>Create an exception for a lex-time failure.</summary>
    public LexerException(string message, int line, int column, bool isIncomplete)
        : base($"({line}:{column}) {message}")
    {
        Line = line;
        Column = column;
        IsIncomplete = isIncomplete;
    }
}
