namespace TerminalNinja.Shell.Runtime;

/// <summary>Runtime evaluation error — type mismatch, unbound name, no-matching-switch-arm, etc.</summary>
public sealed class EvaluatorException : Exception
{
    /// <summary>Create an evaluator exception with the given message.</summary>
    public EvaluatorException(string message) : base(message) { }

    /// <summary>Create an evaluator exception with a message and inner exception.</summary>
    public EvaluatorException(string message, Exception inner) : base(message, inner) { }
}
