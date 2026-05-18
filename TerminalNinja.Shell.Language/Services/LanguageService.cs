using TerminalNinja.Shell.Lexer;
using TerminalNinja.Shell.Parser;

namespace TerminalNinja.Shell.Language.Services;

/// <summary>
/// Tooling-facing analysis surface for NinjaShell source. All members are pure
/// functions of source-and-position — no I/O, no URI tracking, no document
/// lifecycle. Both the LSP server and the in-process REPL consume this directly.
/// </summary>
/// <remarks>
/// Positions are 0-based (line and character), matching the Language Server
/// Protocol. The lexer and parser use 1-based positions for human-readable
/// error messages; this service performs the conversion at the boundary.
/// </remarks>
public static class LanguageService
{
    /// <summary>
    /// Compute the diagnostics for a source string. Currently surfaces lexer and
    /// parser errors as <see cref="DiagnosticSeverity.Error"/> diagnostics. Returns
    /// an empty list when the source parses cleanly.
    /// </summary>
    public static IReadOnlyList<Diagnostic> GetDiagnostics(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var diagnostics = new List<Diagnostic>();
        try
        {
            _ = NinjaParser.ParseScript(source);
        }
        catch (LexerException ex)
        {
            diagnostics.Add(SinglePointDiagnostic(ex.Line, ex.Column, ex.Message));
        }
        catch (ParserException ex)
        {
            diagnostics.Add(SinglePointDiagnostic(ex.Line, ex.Column, ex.Message));
        }
        return diagnostics;
    }

    private static Diagnostic SinglePointDiagnostic(int line1, int column1, string message)
    {
        // Lexer/Parser positions are 1-based; LSP wants 0-based.
        int line = Math.Max(line1 - 1, 0);
        int col = Math.Max(column1 - 1, 0);
        var start = new Position(line, col);
        var end = new Position(line, col + 1);
        return new Diagnostic(new Range(start, end), DiagnosticSeverity.Error, message);
    }
}
