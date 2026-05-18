namespace TerminalNinja.Shell.Parser;

/// <summary>
/// One syntactic error collected during a recovering parse. Mirrors the
/// information <see cref="ParserException"/> carries, but is a value type
/// callers can accumulate across statements without unwinding the stack.
/// </summary>
public sealed record ParseDiagnostic(
    int Line,
    int Column,
    int Length,
    string Message,
    bool IsIncomplete);
