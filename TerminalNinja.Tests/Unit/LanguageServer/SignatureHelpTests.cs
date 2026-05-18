using TerminalNinja.Shell.Language.Services;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Tests.Unit.LanguageServer;

/// <summary>
/// Pins the SignatureHelp surface: walk-left algorithm finds the enclosing
/// call, splits Detail into parameters, tracks active param via top-level
/// comma counting, falls back to scope NFuncs for user-defined names.
/// </summary>
public class SignatureHelpTests
{
    private static SignatureHelp? At(string source, int line, int character)
        => LanguageService.GetSignatureHelp(source, new Position(line, character));

    private static SignatureHelp? At(string source, int line, int character,
        IReadOnlyDictionary<string, NValue> scope)
        => LanguageService.GetSignatureHelp(source, new Position(line, character), scope);

    // ─── basic resolution ───────────────────────────────────────────────────

    [Test]
    public async Task NotInsideAnyCall_ReturnsNull()
    {
        await Assert.That(At("let x = 1", 0, 9)).IsNull();
    }

    [Test]
    public async Task CursorInsideBuiltinCall_ReturnsSignature()
    {
        // `where(` — cursor right after the open paren.
        var sig = At("where(", 0, 6);
        await Assert.That(sig).IsNotNull();
        await Assert.That(sig!.Label).Contains("where");
        await Assert.That(sig.Parameters.Length).IsGreaterThanOrEqualTo(2);
        await Assert.That(sig.ActiveParameter).IsEqualTo(0);
    }

    [Test]
    public async Task UnknownCallable_ReturnsNull()
    {
        await Assert.That(At("unknownThing(", 0, 13)).IsNull();
    }

    [Test]
    public async Task ModuleMemberCall_Resolves()
    {
        var sig = At("fs.ls(", 0, 6);
        await Assert.That(sig).IsNotNull();
        await Assert.That(sig!.Label).Contains("ls");
    }

    // ─── active parameter tracking ──────────────────────────────────────────

    [Test]
    public async Task AfterFirstComma_ActiveParamIsOne()
    {
        // `select(xs, ` — cursor past the comma. Active param = 1.
        var sig = At("select(xs, ", 0, 11);
        await Assert.That(sig).IsNotNull();
        await Assert.That(sig!.ActiveParameter).IsEqualTo(1);
    }

    [Test]
    public async Task CursorPastClosingNestedCall_ResolvesToOuterWithCorrectActiveParam()
    {
        // `select(xs, where(ys, p => p)` — cursor at end (past the inner `)`).
        // The inner where(...) is balanced, so we're back inside select at its
        // 2nd argument. The comma inside the now-closed where must NOT advance
        // select's active parameter.
        var sig = At("select(xs, where(ys, p => p)", 0, 28);
        await Assert.That(sig).IsNotNull();
        await Assert.That(sig!.Label).Contains("select");
        await Assert.That(sig.ActiveParameter).IsEqualTo(1);
    }

    [Test]
    public async Task CursorInsideNestedCall_ResolvesToInnerCall()
    {
        // Same source but cursor before the inner `)` — we're inside `where(ys, p => p`.
        var sig = At("select(xs, where(ys, p => p)", 0, 27);
        await Assert.That(sig).IsNotNull();
        await Assert.That(sig!.Label).Contains("where");
        await Assert.That(sig.ActiveParameter).IsEqualTo(1);
    }

    [Test]
    public async Task CommaInsideStringLiteral_Ignored()
    {
        // The comma inside "a,b" is inside a string and doesn't advance the active param.
        var sig = At("println(\"a,b\", ", 0, 15);
        await Assert.That(sig).IsNotNull();
        await Assert.That(sig!.ActiveParameter).IsEqualTo(1);
    }

    // ─── scope-bound user functions ─────────────────────────────────────────

    [Test]
    public async Task UserNFunc_FallsBackToSyntheticSignature()
    {
        var scope = new Dictionary<string, NValue>
        {
            ["double"] = new NFunc(args => args[0], 1),
        };
        var sig = At("double(", 0, 7, scope);
        await Assert.That(sig).IsNotNull();
        await Assert.That(sig!.Label).Contains("double");
        await Assert.That(sig.Parameters.Length).IsEqualTo(1);
        await Assert.That(sig.Documentation!).Contains("user-defined");
    }

    [Test]
    public async Task UserNonCallable_ReturnsNull()
    {
        // A scope binding that isn't callable shouldn't produce a signature.
        var scope = new Dictionary<string, NValue>
        {
            ["x"] = new NInt(42),
        };
        await Assert.That(At("x(", 0, 2, scope)).IsNull();
    }

    // ─── parameter substring ranges ─────────────────────────────────────────

    [Test]
    public async Task ParameterLabel_StartAndLengthIndexIntoSignatureLabel()
    {
        var sig = At("where(", 0, 6);
        await Assert.That(sig).IsNotNull();
        var first = sig!.Parameters[0];
        // Sanity: the substring at [Start, Start+Length) of Label equals the parameter's Label.
        await Assert.That(sig.Label.Substring(first.LabelStart, first.LabelLength)).IsEqualTo(first.Label);
    }
}
