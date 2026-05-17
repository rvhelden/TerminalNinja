using System.Text;
using TerminalNinja.Shell.Ast;
using TerminalNinja.Shell.Lexer;
using TerminalNinja.Shell.Parser;

namespace TerminalNinja.Shell.Repl;

/// <summary>
/// Accumulates REPL input lines until a complete parse can be produced. Distinguishes
/// "more input needed" (unterminated braces, in-progress lambda) from real syntax
/// errors so the prompt can switch between primary and continuation modes.
/// </summary>
public sealed class LineAccumulator
{
    private readonly StringBuilder _buffer = new();

    /// <summary>True when no input has been accepted since the last reset.</summary>
    public bool IsEmpty => _buffer.Length == 0;

    /// <summary>Current accumulated source.</summary>
    public string Source => _buffer.ToString();

    /// <summary>Discard all accumulated input.</summary>
    public void Reset() => _buffer.Clear();

    /// <summary>
    /// Append <paramref name="line"/> (without trailing newline) and try to parse the
    /// accumulated source. The buffer is reset on a successful parse or a fatal error;
    /// on an incomplete-input signal it's retained so the caller can append more.
    /// </summary>
    public AccumulatorResult Feed(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (_buffer.Length > 0) _buffer.Append('\n');
        _buffer.Append(line);

        var source = _buffer.ToString();
        if (string.IsNullOrWhiteSpace(source))
        {
            _buffer.Clear();
            return AccumulatorResult.Empty();
        }

        try
        {
            var expr = NinjaParser.ParseExpression(source);
            _buffer.Clear();
            return AccumulatorResult.Complete(expr);
        }
        catch (LexerException ex) when (ex.IsIncomplete)
        {
            return AccumulatorResult.NeedMore();
        }
        catch (ParserException ex) when (ex.IsIncomplete)
        {
            return AccumulatorResult.NeedMore();
        }
        catch (LexerException ex)
        {
            _buffer.Clear();
            return AccumulatorResult.Error(ex.Message);
        }
        catch (ParserException ex)
        {
            _buffer.Clear();
            return AccumulatorResult.Error(ex.Message);
        }
    }
}

/// <summary>Kind of result a <see cref="LineAccumulator.Feed"/> produced.</summary>
public enum AccumulatorState
{
    /// <summary>An empty / whitespace-only line — caller should re-prompt with the primary prompt.</summary>
    Empty,
    /// <summary>The input parsed; <see cref="AccumulatorResult.Expression"/> is set.</summary>
    Complete,
    /// <summary>Lexer or parser asked for more input — caller should prompt with the continuation prompt.</summary>
    NeedMore,
    /// <summary>A non-recoverable syntax error — <see cref="AccumulatorResult.ErrorMessage"/> is set; buffer is reset.</summary>
    Error,
}

/// <summary>The outcome of one <see cref="LineAccumulator.Feed"/> call.</summary>
public readonly record struct AccumulatorResult(AccumulatorState State, Expr? Expression, string? ErrorMessage)
{
    /// <summary>Result for an empty input.</summary>
    public static AccumulatorResult Empty() => new(AccumulatorState.Empty, null, null);

    /// <summary>Result for a successful parse.</summary>
    public static AccumulatorResult Complete(Expr expr) => new(AccumulatorState.Complete, expr, null);

    /// <summary>Result for an incomplete input (need more lines).</summary>
    public static AccumulatorResult NeedMore() => new(AccumulatorState.NeedMore, null, null);

    /// <summary>Result for an unrecoverable syntax error.</summary>
    public static AccumulatorResult Error(string message) => new(AccumulatorState.Error, null, message);
}
