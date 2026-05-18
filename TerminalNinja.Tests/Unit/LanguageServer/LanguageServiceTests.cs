using TerminalNinja.Shell.Language.Services;

namespace TerminalNinja.Tests.Unit.LanguageServer;

/// <summary>
/// LanguageService is the shared analysis surface — both the LSP server and the
/// in-process REPL consume it. These tests cover the diagnostic-shaped surface
/// (the only one in PR 1); completion/hover land in later PRs.
/// </summary>
public class LanguageServiceTests
{
    [Test]
    public async Task GetDiagnostics_EmptySource_ReturnsEmptyList()
    {
        await Assert.That(LanguageService.GetDiagnostics("")).IsEmpty();
    }

    [Test]
    public async Task GetDiagnostics_CleanSource_ReturnsEmptyList()
    {
        var d = LanguageService.GetDiagnostics("let x = 42\nx + 1");
        await Assert.That(d).IsEmpty();
    }

    [Test]
    public async Task GetDiagnostics_LexerError_ProducesOneErrorDiagnostic()
    {
        var d = LanguageService.GetDiagnostics("@illegal");
        await Assert.That(d.Count).IsEqualTo(1);
        await Assert.That(d[0].Severity).IsEqualTo(DiagnosticSeverity.Error);
    }

    [Test]
    public async Task GetDiagnostics_ParserError_ProducesOneErrorDiagnostic()
    {
        // `let x =` is incomplete — but the lexer doesn't catch that; the parser does.
        // Use a definitively-bad parse: a trailing operator.
        var d = LanguageService.GetDiagnostics("let x = 1 + ;");
        await Assert.That(d.Count).IsEqualTo(1);
        await Assert.That(d[0].Severity).IsEqualTo(DiagnosticSeverity.Error);
    }

    [Test]
    public async Task GetDiagnostics_Positions_AreZeroBased()
    {
        // Source places the error on line 3 (third line). Lexer/parser report 1-based
        // line 3, column N; LanguageService converts to 0-based line 2.
        var source = "let x = 1\nlet y = 2\n@illegal";
        var d = LanguageService.GetDiagnostics(source);
        await Assert.That(d.Count).IsEqualTo(1);
        await Assert.That(d[0].Range.Start.Line).IsEqualTo(2);
    }

    [Test]
    public async Task GetDiagnostics_RangeIsNonEmpty()
    {
        var d = LanguageService.GetDiagnostics("@illegal");
        await Assert.That(d[0].Range.End.Character).IsGreaterThan(d[0].Range.Start.Character);
    }
}
