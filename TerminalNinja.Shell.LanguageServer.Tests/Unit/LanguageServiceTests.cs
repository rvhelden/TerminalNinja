using TerminalNinja.Shell.Language.Services;

namespace TerminalNinja.Shell.LanguageServer.Tests.Unit;

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

    [Test]
    public async Task GetDiagnostics_BadToken_RangeCoversWholeToken()
    {
        // `let foo bar` — after `let foo` the parser expects `=` but gets `bar`.
        // The diagnostic range should span the whole `bar` identifier (length 3),
        // not just the first character — that's the point of carrying token
        // length through to the diagnostic.
        var source = "let foo bar";
        var d = LanguageService.GetDiagnostics(source);
        await Assert.That(d.Count).IsEqualTo(1);
        int width = d[0].Range.End.Character - d[0].Range.Start.Character;
        await Assert.That(width).IsEqualTo("bar".Length);
    }

    // ─── multi-error recovery ───────────────────────────────────────────────

    [Test]
    public async Task GetDiagnostics_TwoErrorsOnDifferentLines_BothReported()
    {
        // Two separate broken statements — the parser must sync past the first
        // and report the second too. Pre-T4 only the first one surfaced.
        var source = "let foo bar\nlet baz qux";
        var d = LanguageService.GetDiagnostics(source);
        await Assert.That(d.Count).IsEqualTo(2);
        await Assert.That(d[0].Range.Start.Line).IsEqualTo(0);
        await Assert.That(d[1].Range.Start.Line).IsEqualTo(1);
    }

    [Test]
    public async Task GetDiagnostics_GoodStatementBetweenErrors_StillReported()
    {
        // Bad → good → bad. The middle form parses cleanly (the recovering
        // parser doesn't poison anything downstream) and both bad forms
        // surface as separate diagnostics. Lex-clean source — only the
        // parser should reject.
        var source = "let x =\nlet y = 1\nlet z =";
        var d = LanguageService.GetDiagnostics(source);
        await Assert.That(d.Count).IsEqualTo(2);
        // Two distinct error positions, not the same one reported twice.
        await Assert.That(d[0].Range.Start.Line).IsNotEqualTo(d[1].Range.Start.Line);
    }

    [Test]
    public async Task GetDiagnostics_LexerError_StillSingleDiagnostic()
    {
        // Lexer can't recover — it throws once and we're done. Even if there's
        // more bad syntax after, only the lex error gets reported.
        var d = LanguageService.GetDiagnostics("@bad1\n@bad2");
        await Assert.That(d.Count).IsEqualTo(1);
    }
}
