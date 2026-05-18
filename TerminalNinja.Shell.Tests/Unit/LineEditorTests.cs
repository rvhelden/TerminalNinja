using TerminalNinja.Shell.Language.Services;
using TerminalNinja.Shell.Repl;

namespace TerminalNinja.Shell.Tests.Unit;

/// <summary>
/// Drives <see cref="LineEditor"/> with a fake key reader so we can assert on
/// the rendered output and the returned line without a real terminal.
/// </summary>
public class LineEditorTests
{
    private sealed class FakeKeyReader : IKeyReader
    {
        private readonly Queue<ConsoleKeyInfo> _keys;
        public FakeKeyReader(params ConsoleKeyInfo[] keys) { _keys = new(keys); }
        public ConsoleKeyInfo ReadKey()
        {
            if (_keys.Count == 0) throw new InvalidOperationException("FakeKeyReader exhausted");
            return _keys.Dequeue();
        }
    }

    private static ConsoleKeyInfo Char(char c, ConsoleKey key = ConsoleKey.A, ConsoleModifiers mods = 0)
        => new(c, key, (mods & ConsoleModifiers.Shift) != 0, (mods & ConsoleModifiers.Alt) != 0, (mods & ConsoleModifiers.Control) != 0);

    private static ConsoleKeyInfo Special(ConsoleKey key, ConsoleModifiers mods = 0)
        => new('\0', key, (mods & ConsoleModifiers.Shift) != 0, (mods & ConsoleModifiers.Alt) != 0, (mods & ConsoleModifiers.Control) != 0);

    private static LineEditor MakeEditor(StringWriter output,
        Func<string, int, IReadOnlyList<CompletionItem>>? completer,
        params ConsoleKeyInfo[] keys)
        => new LineEditor(new FakeKeyReader(keys), output, completer);

    // ─── basic input ────────────────────────────────────────────────────────

    [Test]
    public async Task TypeChars_AndEnter_ReturnsEnteredLine()
    {
        var output = new StringWriter();
        var editor = MakeEditor(output, null,
            Char('h'), Char('i'), Special(ConsoleKey.Enter));
        var r = editor.ReadLine("> ");
        await Assert.That(r.Result).IsEqualTo(LineEditor.ReadResult.EnteredLine);
        await Assert.That(r.Text).IsEqualTo("hi");
    }

    [Test]
    public async Task Backspace_DeletesLastChar()
    {
        var output = new StringWriter();
        var editor = MakeEditor(output, null,
            Char('a'), Char('b'), Char('c'),
            Special(ConsoleKey.Backspace),
            Special(ConsoleKey.Enter));
        var r = editor.ReadLine("> ");
        await Assert.That(r.Text).IsEqualTo("ab");
    }

    [Test]
    public async Task Backspace_OnEmptyBuffer_NoOp()
    {
        var output = new StringWriter();
        var editor = MakeEditor(output, null,
            Special(ConsoleKey.Backspace),
            Char('x'),
            Special(ConsoleKey.Enter));
        var r = editor.ReadLine("> ");
        await Assert.That(r.Text).IsEqualTo("x");
    }

    [Test]
    public async Task CtrlD_OnEmptyBuffer_ReturnsEof()
    {
        var output = new StringWriter();
        var editor = MakeEditor(output, null,
            Special(ConsoleKey.D, ConsoleModifiers.Control));
        var r = editor.ReadLine("> ");
        await Assert.That(r.Result).IsEqualTo(LineEditor.ReadResult.Eof);
    }

    [Test]
    public async Task CtrlD_OnNonEmptyBuffer_Ignored()
    {
        var output = new StringWriter();
        var editor = MakeEditor(output, null,
            Char('a'),
            Special(ConsoleKey.D, ConsoleModifiers.Control),
            Char('b'),
            Special(ConsoleKey.Enter));
        var r = editor.ReadLine("> ");
        await Assert.That(r.Text).IsEqualTo("ab");
    }

    [Test]
    public async Task CtrlC_AbandonsLine()
    {
        var output = new StringWriter();
        var editor = MakeEditor(output, null,
            Char('x'), Char('y'),
            Char('c', ConsoleKey.C, ConsoleModifiers.Control));
        var r = editor.ReadLine("> ");
        await Assert.That(r.Result).IsEqualTo(LineEditor.ReadResult.Aborted);
    }

    // ─── tab completion ────────────────────────────────────────────────────

    [Test]
    public async Task Tab_UniqueMatch_InsertsRemainder()
    {
        // "let " (4 chars) — only one keyword starts with "le": `let`.
        // Tab should insert "t" to complete to "let".
        var output = new StringWriter();
        IReadOnlyList<CompletionItem> Completer(string line, int cursor)
            => LanguageService.GetCompletions(line, new Position(0, cursor));

        var editor = MakeEditor(output, Completer,
            Char('l'), Char('e'),
            Special(ConsoleKey.Tab),
            Special(ConsoleKey.Enter));
        var r = editor.ReadLine("> ");
        await Assert.That(r.Text).IsEqualTo("let");
    }

    [Test]
    public async Task Tab_MultipleMatches_InsertsCommonPrefix()
    {
        // Top-level "se" prefix matches "select" and "skip" — wait, "skip" starts with "sk".
        // Actually "se" matches only "select" — so let's try "s" which matches several:
        //   select, skip, sort, source, switch (keyword). Common prefix of all = "s".
        // No extension possible — should NOT change the buffer, but should print a list.
        var output = new StringWriter();
        IReadOnlyList<CompletionItem> Completer(string line, int cursor)
            => LanguageService.GetCompletions(line, new Position(0, cursor));

        var editor = MakeEditor(output, Completer,
            Char('s'),
            Special(ConsoleKey.Tab),
            Special(ConsoleKey.Enter));
        var r = editor.ReadLine("> ");
        await Assert.That(r.Text).IsEqualTo("s");
        // Output should contain at least one of the candidate labels.
        var rendered = output.ToString();
        await Assert.That(rendered.Contains("select") || rendered.Contains("sort") || rendered.Contains("source")).IsTrue();
    }

    [Test]
    public async Task Tab_ExtendableCommonPrefix_AppliesPrefixWithoutMenu()
    {
        // After "sel" the only completions are "select" — unique → completes fully.
        var output = new StringWriter();
        IReadOnlyList<CompletionItem> Completer(string line, int cursor)
            => LanguageService.GetCompletions(line, new Position(0, cursor));

        var editor = MakeEditor(output, Completer,
            Char('s'), Char('e'), Char('l'),
            Special(ConsoleKey.Tab),
            Special(ConsoleKey.Enter));
        var r = editor.ReadLine("> ");
        await Assert.That(r.Text).IsEqualTo("select");
    }

    [Test]
    public async Task Tab_ModuleMemberAccess_CompletesAfterDot()
    {
        // `obj.ty<TAB>` — only `type` starts with `ty`, so Tab inserts the full label.
        // (`obj.t<TAB>` would have two candidates — `type` and `to_rows` — common
        // prefix `t`, so no extension is possible.)
        var output = new StringWriter();
        IReadOnlyList<CompletionItem> Completer(string line, int cursor)
            => LanguageService.GetCompletions(line, new Position(0, cursor));

        var editor = MakeEditor(output, Completer,
            Char('o'), Char('b'), Char('j'), Char('.'), Char('t'), Char('y'),
            Special(ConsoleKey.Tab),
            Special(ConsoleKey.Enter));
        var r = editor.ReadLine("> ");
        await Assert.That(r.Text).IsEqualTo("obj.type");
    }

    [Test]
    public async Task Tab_NoMatches_NoChange()
    {
        var output = new StringWriter();
        IReadOnlyList<CompletionItem> Completer(string line, int cursor)
            => LanguageService.GetCompletions(line, new Position(0, cursor));

        var editor = MakeEditor(output, Completer,
            Char('z'), Char('z'), Char('z'),
            Special(ConsoleKey.Tab),
            Special(ConsoleKey.Enter));
        var r = editor.ReadLine("> ");
        await Assert.That(r.Text).IsEqualTo("zzz");
    }

    [Test]
    public async Task Tab_WithoutCompleter_NoOp()
    {
        var output = new StringWriter();
        var editor = MakeEditor(output, null,
            Char('a'), Special(ConsoleKey.Tab), Special(ConsoleKey.Enter));
        var r = editor.ReadLine("> ");
        await Assert.That(r.Text).IsEqualTo("a");
    }
}
