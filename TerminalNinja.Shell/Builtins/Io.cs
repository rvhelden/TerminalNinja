using System.Collections.Immutable;
using TerminalNinja.Shell.Repl;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Builtins;

/// <summary>Console I/O builtins: <c>print</c>, <c>println</c>, <c>format_table</c>.</summary>
public static class Io
{
    /// <summary>Register IO builtins into <paramref name="b"/>.</summary>
    public static void Register(ImmutableDictionary<string, NValue>.Builder b)
    {
        b["print"] = new NFunc(args =>
        {
            if (args.Length != 1) throw new EvaluatorException($"print expects 1 argument, got {args.Length}");
            Console.Write(NValueOps.FormatForInterpolation(args[0]));
            return NUnit.Instance;
        }, 1);

        b["println"] = new NFunc(args =>
        {
            if (args.Length != 1) throw new EvaluatorException($"println expects 1 argument, got {args.Length}");
            Console.WriteLine(NValueOps.FormatForInterpolation(args[0]));
            return NUnit.Instance;
        }, 1);

        b["format_table"] = new NFunc(args =>
        {
            if (args.Length != 1) throw new EvaluatorException($"format_table expects 1 argument, got {args.Length}");
            if (args[0] is not NList list) throw new EvaluatorException("format_table expects a list");
            return new NString(Printer.FormatRecordTable(list));
        }, 1);
    }
}
