using TerminalNinja.Shell.Builtins;
using TerminalNinja.Shell.PowerShell;
using TerminalNinja.Shell.Repl;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell;

internal static class Program
{
    /// <summary>Display version embedded in the REPL banner.</summary>
    public const string Version = "0.0.0-mvp";

    private static int Main(string[] args)
    {
        if (args.Length == 1 && (args[0] == "--version" || args[0] == "-v"))
        {
            Console.WriteLine($"ninja v{Version}");
            return 0;
        }
        if (args.Length == 2 && (args[0] == "-c" || args[0] == "--command"))
        {
            return RunOneShot(args[1]);
        }
        if (args.Length == 1 && File.Exists(args[0]))
        {
            return RunScript(args[0]);
        }
        if (args.Length > 0)
        {
            Console.Error.WriteLine("usage: ninja                   (interactive REPL)");
            Console.Error.WriteLine("       ninja -c <expr>          (evaluate one expression)");
            Console.Error.WriteLine("       ninja <script.ninja>     (run a script file)");
            return 64;
        }
        var repl = new NinjaRepl(Console.In, Console.Out, Console.Error);
        return repl.Run();
    }

    private static int RunOneShot(string source)
    {
        var env = BuiltinRegistry.CreateDefaultEnv();
        if (PwshBridge.IsAvailable) env = PwshBridge.Install(env);
        try
        {
            var v = NinjaEvaluator.EvalScript(source, env).Value;
            var rendered = Printer.Format(v);
            if (!string.IsNullOrEmpty(rendered)) Console.WriteLine(rendered);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static int RunScript(string path)
    {
        var source = File.ReadAllText(path);
        return RunOneShot(source);
    }
}
