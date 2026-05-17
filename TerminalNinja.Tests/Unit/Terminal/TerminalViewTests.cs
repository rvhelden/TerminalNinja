using System.Text;
using TerminalNinja.Buffers;
using TerminalNinja.Input;
using TerminalNinja.Primitives;
using TerminalNinja.Terminal;

namespace TerminalNinja.Tests.Unit.Terminal;

/// <summary>
/// End-to-end tests for <see cref="TerminalView"/>: bytes flow in through Feed or a backend,
/// the cell grid gets populated, Render copies it onto a <see cref="CellBuffer"/>, the
/// cursor inverts at its position, and OnKeyEvent forwards encoded bytes to the backend.
/// </summary>
public class TerminalViewTests
{
    private static (TerminalView view, CellBuffer target) Setup(int rows = 5, int cols = 20)
    {
        var view = new TerminalView(rows, cols);
        var target = new CellBuffer(cols, rows);
        return (view, target);
    }

    private static void Render(TerminalView view, CellBuffer target)
    {
        view.Render(target, new Rect(0, 0, target.Width, target.Height));
    }

    [Test]
    public async Task Feed_AsciiText_RendersIntoCellBuffer()
    {
        var (v, t) = Setup();
        v.Feed(Encoding.ASCII.GetBytes("Hi"));

        Render(v, t);

        await Assert.That(t.GetCell(0, 0).Codepoint).IsEqualTo((uint)'H');
        await Assert.That(t.GetCell(1, 0).Codepoint).IsEqualTo((uint)'i');
    }

    [Test]
    public async Task Render_InvertsCursorCell()
    {
        var (v, t) = Setup();
        v.Feed(Encoding.ASCII.GetBytes("X")); // cursor lands at (0, 1)

        Render(v, t);

        // Cell (0, 1) is empty (space) — when we invert empty's white fg + black bg we get
        // black fg + white bg.
        var cursorCell = t.GetCell(1, 0);
        await Assert.That(cursorCell.Foreground).IsEqualTo(Color.Black);
        await Assert.That(cursorCell.Background).IsEqualTo(Color.White);
    }

    [Test]
    public async Task Render_CursorHidden_NoInversion()
    {
        var (v, t) = Setup();
        v.Feed("\x1B[?25l"u8.ToArray()); // hide cursor
        v.Feed(Encoding.ASCII.GetBytes("X"));

        Render(v, t);

        var cursorCell = t.GetCell(1, 0);
        await Assert.That(cursorCell.Foreground).IsEqualTo(Color.White);
        await Assert.That(cursorCell.Background).IsEqualTo(Color.Black);
    }

    [Test]
    public async Task Backend_DataReceived_FlowsThroughOnNextRender()
    {
        var (v, t) = Setup();
        var backend = new NullTerminalBackend();
        v.Backend = backend;
        await backend.StartAsync();

        // Backend simulates child output — queued; nothing rendered yet.
        backend.SimulateDataReceived(Encoding.ASCII.GetBytes("hi"));

        Render(v, t);
        await Assert.That(t.GetCell(0, 0).Codepoint).IsEqualTo((uint)'h');
        await Assert.That(t.GetCell(1, 0).Codepoint).IsEqualTo((uint)'i');
    }

    [Test]
    public async Task OnKeyEvent_WritesEncodedBytesToBackend()
    {
        var view = new TerminalView();
        var backend = new NullTerminalBackend();
        view.Backend = backend;
        await backend.StartAsync();

        view.OnKeyEvent(new KeyEvent(ConsoleKey.A, 'a', false, false, false));

        // WriteAsync on NullTerminalBackend completes synchronously; the byte should be
        // visible immediately.
        await Assert.That(backend.WrittenBytes.Count).IsEqualTo(1);
        await Assert.That(backend.WrittenBytes[0]).IsEqualTo((byte)'a');
    }

    [Test]
    public async Task OnKeyEvent_UpArrow_WritesCsiA()
    {
        var view = new TerminalView();
        var backend = new NullTerminalBackend();
        view.Backend = backend;
        await backend.StartAsync();

        view.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(backend.WrittenBytes.Count).IsEqualTo(3);
        await Assert.That(backend.WrittenBytes[0]).IsEqualTo((byte)0x1B);
        await Assert.That(backend.WrittenBytes[1]).IsEqualTo((byte)'[');
        await Assert.That(backend.WrittenBytes[2]).IsEqualTo((byte)'A');
    }

    [Test]
    public async Task OnKeyEvent_NoBackend_DoesNotThrow()
    {
        var view = new TerminalView();
        // No backend attached.
        view.OnKeyEvent(new KeyEvent(ConsoleKey.A, 'a', false, false, false));

        // Reaching here is the success condition.
        await Assert.That(view.Backend).IsNull();
    }

    [Test]
    public async Task SwapBackend_UnsubscribesFromPrevious()
    {
        var view = new TerminalView();
        var first = new NullTerminalBackend();
        var second = new NullTerminalBackend();

        view.Backend = first;
        await first.StartAsync();
        view.Backend = second;
        await second.StartAsync();

        // Data from the OLD backend after the swap must not affect the view.
        first.SimulateDataReceived(Encoding.ASCII.GetBytes("X"));

        var target = new CellBuffer(20, 5);
        view.Render(target, new Rect(0, 0, 20, 5));

        // Cell (0, 0) is the cursor — inverted but still ' '. No 'X' should appear.
        await Assert.That(target.GetCell(0, 0).Codepoint).IsEqualTo((uint)' ');
    }

    [Test]
    public async Task ResizeScreen_PropagatesToBackend()
    {
        var view = new TerminalView(rows: 24, cols: 80);
        var backend = new NullTerminalBackend();
        view.Backend = backend;
        await backend.StartAsync();

        view.ResizeScreen(rows: 30, cols: 100);

        // NullTerminalBackend.ResizeAsync completes synchronously; the history records the
        // (cols, rows) the view passed.
        await Assert.That(backend.ResizeHistory.Count).IsEqualTo(1);
        await Assert.That(backend.ResizeHistory[0]).IsEqualTo((100, 30));
        await Assert.That(view.Screen.Rows).IsEqualTo(30);
        await Assert.That(view.Screen.Cols).IsEqualTo(100);
    }

    [Test]
    public async Task Feed_SgrColor_PropagatesToRenderedCell()
    {
        var (v, t) = Setup();
        // Red foreground, then 'X'.
        v.Feed("\x1B[31m"u8.ToArray());
        v.Feed("X"u8.ToArray());

        Render(v, t);

        var cell = t.GetCell(0, 0);
        await Assert.That(cell.Codepoint).IsEqualTo((uint)'X');
        await Assert.That(cell.Foreground.R).IsEqualTo((byte)0xCD);
    }
}
