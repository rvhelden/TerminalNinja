using TerminalNinja.Shell.Language.Services;

namespace TerminalNinja.Shell.LanguageServer.Tests.Unit;

public class DocumentSymbolTests
{
    [Test]
    public async Task GetDocumentSymbols_EmptySource_ReturnsEmpty()
    {
        await Assert.That(LanguageService.GetDocumentSymbols("")).IsEmpty();
    }

    [Test]
    public async Task GetDocumentSymbols_BadSource_ReturnsEmpty()
    {
        await Assert.That(LanguageService.GetDocumentSymbols("@illegal")).IsEmpty();
    }

    [Test]
    public async Task GetDocumentSymbols_LetStatement_ProducesVariableSymbol()
    {
        var symbols = LanguageService.GetDocumentSymbols("let answer = 42");
        await Assert.That(symbols.Count).IsEqualTo(1);
        await Assert.That(symbols[0].Name).IsEqualTo("answer");
        await Assert.That(symbols[0].Kind).IsEqualTo(SymbolKind.Variable);
    }

    [Test]
    public async Task GetDocumentSymbols_TopLevelLambda_ProducesFunctionSymbol()
    {
        var symbols = LanguageService.GetDocumentSymbols("let add = (a, b) => a + b");
        await Assert.That(symbols.Count).IsEqualTo(1);
        await Assert.That(symbols[0].Name).IsEqualTo("add");
        await Assert.That(symbols[0].Kind).IsEqualTo(SymbolKind.Function);
        // Detail should sketch the signature.
        await Assert.That(symbols[0].Detail).IsEqualTo("(a, b) =>");
    }

    [Test]
    public async Task GetDocumentSymbols_SourceStatement_ProducesModuleSymbol()
    {
        var symbols = LanguageService.GetDocumentSymbols("source(\"init.ninja\")");
        await Assert.That(symbols.Count).IsEqualTo(1);
        await Assert.That(symbols[0].Kind).IsEqualTo(SymbolKind.Module);
        await Assert.That(symbols[0].Name).Contains("init.ninja");
    }

    [Test]
    public async Task GetDocumentSymbols_MultipleTopLevelForms_AllSymbolised()
    {
        var script =
            "let x = 1\n" +
            "let f = n => n * 2\n" +
            "source(\"helper.ninja\")\n" +
            "let y = 2\n" +
            "f(y)";
        var symbols = LanguageService.GetDocumentSymbols(script);
        await Assert.That(symbols.Count).IsEqualTo(4);
        await Assert.That(symbols[0].Kind).IsEqualTo(SymbolKind.Variable);
        await Assert.That(symbols[1].Kind).IsEqualTo(SymbolKind.Function);
        await Assert.That(symbols[2].Kind).IsEqualTo(SymbolKind.Module);
        await Assert.That(symbols[3].Kind).IsEqualTo(SymbolKind.Variable);
    }

    [Test]
    public async Task GetDocumentSymbols_BareExpression_NotASymbol()
    {
        // Bare expressions at the top level (`1 + 2`) aren't outline-worthy.
        var symbols = LanguageService.GetDocumentSymbols("1 + 2");
        await Assert.That(symbols).IsEmpty();
    }

    [Test]
    public async Task GetDocumentSymbols_RangeIsZeroBased_AndCoversForm()
    {
        var symbols = LanguageService.GetDocumentSymbols("let x = 42");
        var r = symbols[0].Range;
        await Assert.That(r.Start.Line).IsEqualTo(0);
        await Assert.That(r.Start.Character).IsEqualTo(0);
        await Assert.That(r.End.Character).IsGreaterThan(r.Start.Character);
    }

    [Test]
    public async Task GetDocumentSymbols_BodyLambdaNested_DoesNotLeakSymbols()
    {
        // A top-level let whose value is a lambda containing inner let-in
        // expressions should still produce only one outline entry. The inner
        // let-in is wrapped in parens so it sits at brace depth > 0 — that
        // keeps the IsTopLevelLetStatement lookahead from being confused by
        // the inner `in` keyword.
        var script = "let outer = x => (let inner = x * 2 in inner + 1)";
        var symbols = LanguageService.GetDocumentSymbols(script);
        await Assert.That(symbols.Count).IsEqualTo(1);
        await Assert.That(symbols[0].Name).IsEqualTo("outer");
    }
}
