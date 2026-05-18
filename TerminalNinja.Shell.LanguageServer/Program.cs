namespace TerminalNinja.Shell.LanguageServer;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 1 && (args[0] == "--version" || args[0] == "-v"))
        {
            Console.WriteLine("ninja-lsp 0.0.1");
            return 0;
        }
        // `--stdio` is a no-op: stdio is the only transport we support, but
        // every LSP client (vscode-languageclient, helix, neovim) passes this
        // flag explicitly. Accept it for compatibility.
        if (args.Length > 1 || (args.Length == 1 && args[0] != "--stdio"))
        {
            Console.Error.WriteLine("usage: ninja-lsp [--stdio]   (LSP over stdio)");
            Console.Error.WriteLine("       ninja-lsp --version");
            return 64;
        }

        // Use binary stdin/stdout — the LSP base protocol is byte-framed,
        // not text-line oriented, so we bypass Console's text decoding.
        using var input = Console.OpenStandardInput();
        using var output = Console.OpenStandardOutput();

        var server = new LspServer();
        server.Run(input, output);
        return 0;
    }
}
