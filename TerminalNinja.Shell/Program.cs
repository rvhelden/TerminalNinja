namespace TerminalNinja.Shell;

internal static class Program
{
    public const string Version = "0.0.0-mvp";

    private static int Main(string[] args)
    {
        Console.WriteLine($"ninja v{Version}");
        Console.WriteLine("REPL not yet implemented — wiring lands in Phase 6.");
        return 0;
    }
}
