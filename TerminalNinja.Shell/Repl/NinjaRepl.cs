using TerminalNinja.Shell.Builtins;
using TerminalNinja.Shell.PowerShell;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Repl;

/// <summary>
/// Interactive read-eval-print loop. Maintains a persistent <see cref="Env"/>
/// across lines so top-level <c>let</c> bindings stick. Switches between the
/// primary prompt (<c>ninja&gt;</c>) and the continuation prompt (<c>....&gt;</c>)
/// based on the line accumulator's state.
/// </summary>
public sealed class NinjaRepl
{
    /// <summary>Primary prompt printed when accepting a fresh expression.</summary>
    public const string PrimaryPrompt = "ninja> ";

    /// <summary>Continuation prompt printed while accumulating an incomplete input.</summary>
    public const string ContinuationPrompt = "....> ";

    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly TextWriter _error;
    private readonly LineAccumulator _accumulator = new();
    private Env _env;

    /// <summary>Create a REPL bound to the given streams.</summary>
    public NinjaRepl(TextReader input, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        _input = input;
        _output = output;
        _error = error;
        _env = BuiltinRegistry.CreateDefaultEnv();
        if (PwshBridge.IsAvailable) _env = PwshBridge.Install(_env);
    }

    /// <summary>Run the REPL until <paramref name="exitOnEof"/> is true and stdin reaches EOF.</summary>
    public int Run(bool exitOnEof = true)
    {
        _output.WriteLine($"ninja v{Program.Version} — type 'exit' or Ctrl+D to quit");
        if (!PwshBridge.IsAvailable)
            _output.WriteLine("(pwsh not on PATH — `pwsh { ... }` blocks will fail)");

        while (true)
        {
            _output.Write(_accumulator.IsEmpty ? PrimaryPrompt : ContinuationPrompt);
            var line = _input.ReadLine();
            if (line == null)
            {
                if (!exitOnEof) return 0;
                _output.WriteLine();
                return 0;
            }
            if (_accumulator.IsEmpty && line.Trim() is "exit" or "quit")
                return 0;

            var result = _accumulator.Feed(line);
            switch (result.State)
            {
                case AccumulatorState.Empty:
                    continue;
                case AccumulatorState.NeedMore:
                    continue;
                case AccumulatorState.Error:
                    _error.WriteLine($"syntax error: {result.ErrorMessage}");
                    continue;
                case AccumulatorState.Complete:
                    ExecuteAndPrint(result.Expression!);
                    continue;
            }
        }
    }

    private void ExecuteAndPrint(Ast.Expr expr)
    {
        try
        {
            var evaluated = NinjaEvaluator.EvalTop(expr, _env);
            _env = evaluated.Env;
            var rendered = Printer.Format(evaluated.Value);
            if (!string.IsNullOrEmpty(rendered)) _output.WriteLine(rendered);
        }
        catch (EvaluatorException ex)
        {
            _error.WriteLine($"runtime error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _error.WriteLine($"internal error: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
