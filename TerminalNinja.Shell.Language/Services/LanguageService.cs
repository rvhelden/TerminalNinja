using TerminalNinja.Shell.Ast;
using TerminalNinja.Shell.Lexer;
using TerminalNinja.Shell.Parser;
using TerminalNinja.Shell.Values;

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

    /// <summary>
    /// Compute outline-shaped symbols for a source string. Top-level
    /// <c>let NAME = VALUE</c> statements become Variable (or Function, when the
    /// bound value is a lambda) symbols; top-level <c>source("path")</c> forms
    /// become Module symbols. Source that fails to parse returns an empty list
    /// — the LSP client also gets diagnostics, so we don't surface partial
    /// outlines from a half-parsed tree.
    /// </summary>
    public static IReadOnlyList<DocumentSymbol> GetDocumentSymbols(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var symbols = new List<DocumentSymbol>();
        try
        {
            var forms = NinjaParser.ParseScript(source);
            foreach (var form in forms)
            {
                var sym = TryBuildSymbol(form);
                if (sym is not null) symbols.Add(sym);
            }
        }
        catch (LexerException)  { return Array.Empty<DocumentSymbol>(); }
        catch (ParserException) { return Array.Empty<DocumentSymbol>(); }

        return symbols;
    }

    private static DocumentSymbol? TryBuildSymbol(Expr form) => form switch
    {
        LetStatement ls => new DocumentSymbol(
            Name: ls.Name,
            Detail: ls.Value is Lambda lam ? $"({string.Join(", ", lam.Parameters)}) =>" : null,
            Kind: ls.Value is Lambda ? SymbolKind.Function : SymbolKind.Variable,
            Range: SpanToRange(ls.Span),
            SelectionRange: SpanToRange(ls.Span),
            Children: Array.Empty<DocumentSymbol>()),
        SourceStatement src => new DocumentSymbol(
            Name: BuildSourceName(src),
            Detail: null,
            Kind: SymbolKind.Module,
            Range: SpanToRange(src.Span),
            SelectionRange: SpanToRange(src.Span),
            Children: Array.Empty<DocumentSymbol>()),
        _ => null,
    };

    private static string BuildSourceName(SourceStatement src)
    {
        if (src.Path is Lit { Value: NString s }) return $"source(\"{s.Value}\")";
        return "source(...)";
    }

    private static Range SpanToRange(Span s)
    {
        // 1-based source positions → 0-based LSP positions.
        var start = new Position(Math.Max(s.StartLine - 1, 0), Math.Max(s.StartColumn - 1, 0));
        var end = new Position(Math.Max(s.EndLine - 1, 0), Math.Max(s.EndColumn - 1, 0));
        return new Range(start, end);
    }
}
