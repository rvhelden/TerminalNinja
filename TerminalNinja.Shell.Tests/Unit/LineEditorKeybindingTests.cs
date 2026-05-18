using TerminalNinja.Shell.Config;
using TerminalNinja.Shell.Repl;

namespace TerminalNinja.Shell.Tests.Unit;

public class LineEditorKeybindingTests
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

    [Test]
    public async Task NoConfig_Bound_FlowsThroughExistingHandlers()
    {
        var config = NinjaConfig.Empty();
        var output = new StringWriter();
        var editor = new LineEditor(
            new FakeKeyReader(Char('a'), Special(ConsoleKey.Enter)),
            output, null, config);
        var r = editor.ReadLine("> ");
        await Assert.That(r.Result).IsEqualTo(LineEditor.ReadResult.EnteredLine);
        await Assert.That(r.Text).IsEqualTo("a");
    }

    [Test]
    public async Task BindCtrlL_Clear_ClearsBuffer()
    {
        var config = NinjaConfig.Empty();
        config.BindKey("Ctrl+L", "clear");
        var output = new StringWriter();
        var editor = new LineEditor(
            new FakeKeyReader(
                Char('a'), Char('b'), Char('c'),
                Special(ConsoleKey.L, ConsoleModifiers.Control),
                Char('z'), Special(ConsoleKey.Enter)),
            output, null, config);
        var r = editor.ReadLine("> ");
        // Buffer accumulated "abc", clear should reset to "", then "z" added.
        await Assert.That(r.Text).IsEqualTo("z");
        // The clear action emits the ANSI "clear screen + cursor home" escape.
        await Assert.That(output.ToString().Contains("\x1b[2J")).IsTrue();
    }

    [Test]
    public async Task BindCtrlS_Submit_ReturnsEnteredLine()
    {
        var config = NinjaConfig.Empty();
        config.BindKey("Ctrl+S", "submit");
        var output = new StringWriter();
        var editor = new LineEditor(
            new FakeKeyReader(
                Char('h'), Char('i'),
                Special(ConsoleKey.S, ConsoleModifiers.Control)),
            output, null, config);
        var r = editor.ReadLine("> ");
        await Assert.That(r.Result).IsEqualTo(LineEditor.ReadResult.EnteredLine);
        await Assert.That(r.Text).IsEqualTo("hi");
    }

    [Test]
    public async Task BindCtrlQ_Abort_ReturnsAborted()
    {
        var config = NinjaConfig.Empty();
        config.BindKey("Ctrl+Q", "abort");
        var output = new StringWriter();
        var editor = new LineEditor(
            new FakeKeyReader(
                Char('x'),
                Special(ConsoleKey.Q, ConsoleModifiers.Control)),
            output, null, config);
        var r = editor.ReadLine("> ");
        await Assert.That(r.Result).IsEqualTo(LineEditor.ReadResult.Aborted);
    }

    [Test]
    public async Task UnknownChord_FlowsThroughToCharHandler()
    {
        // No bindings, but Ctrl+A is pressed. With no binding, the editor's
        // existing "any other control key — ignore" path applies. Buffer stays empty.
        var config = NinjaConfig.Empty();
        var output = new StringWriter();
        var editor = new LineEditor(
            new FakeKeyReader(
                Special(ConsoleKey.A, ConsoleModifiers.Control),
                Char('b'), Special(ConsoleKey.Enter)),
            output, null, config);
        var r = editor.ReadLine("> ");
        await Assert.That(r.Text).IsEqualTo("b");
    }

    [Test]
    public async Task ShiftOnly_FlowsThroughAsCharInput()
    {
        // Shift+letter is normal capital-letter typing; the chord layer must NOT
        // intercept it because that would block plain typing of uppercase letters.
        var config = NinjaConfig.Empty();
        // Even if a (misguided) user binds Shift+A, we treat it as char input.
        config.BindKey("Shift+A", "clear");
        var output = new StringWriter();
        var editor = new LineEditor(
            new FakeKeyReader(
                Char('A', ConsoleKey.A, ConsoleModifiers.Shift),
                Special(ConsoleKey.Enter)),
            output, null, config);
        var r = editor.ReadLine("> ");
        await Assert.That(r.Text).IsEqualTo("A");
    }
}
