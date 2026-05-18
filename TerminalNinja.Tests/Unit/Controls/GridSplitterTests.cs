using TerminalNinja.Buffers;
using TerminalNinja.Controls;
using TerminalNinja.Input;
using TerminalNinja.Primitives;

namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Pins the public contract of <see cref="GridSplitter"/>: a press starts a
/// drag, subsequent moves emit signed deltas, release ends the drag, arrow
/// keys nudge by the configured step (4× under Shift).
/// </summary>
public class GridSplitterTests
{
    private static void Press(GridSplitter s, int x)
        => s.OnMouseEvent(new MouseEvent(x, 0, MouseButton.Left, MouseAction.Press));

    private static void Move(GridSplitter s, int x)
        => s.OnMouseEvent(new MouseEvent(x, 0, MouseButton.None, MouseAction.Move));

    private static void Release(GridSplitter s, int x)
        => s.OnMouseEvent(new MouseEvent(x, 0, MouseButton.Left, MouseAction.Release));

    [Test]
    public async Task PreferredSize_IsOneCellWide()
    {
        var s = new GridSplitter();
        var size = s.GetPreferredSize(new Rect(0, 0, 80, 24));
        await Assert.That(size.Width).IsEqualTo(1);
        await Assert.That(size.Height).IsEqualTo(24);
    }

    [Test]
    public async Task MoveWithoutPress_EmitsNoDelta()
    {
        var s = new GridSplitter();
        int delta = 0;
        s.Resized += d => delta += d;
        Move(s, 5);
        await Assert.That(delta).IsEqualTo(0);
    }

    [Test]
    public async Task DragRight_EmitsPositiveDelta()
    {
        var s = new GridSplitter();
        int sum = 0;
        s.Resized += d => sum += d;
        Press(s, 10);
        Move(s, 13);
        await Assert.That(sum).IsEqualTo(3);
    }

    [Test]
    public async Task DragLeft_EmitsNegativeDelta()
    {
        var s = new GridSplitter();
        int sum = 0;
        s.Resized += d => sum += d;
        Press(s, 10);
        Move(s, 7);
        await Assert.That(sum).IsEqualTo(-3);
    }

    [Test]
    public async Task SequentialMoves_AreIncremental()
    {
        // After a press at x=10, moves to 12 → 15 → 11 should emit +2, +3, -4.
        var s = new GridSplitter();
        var deltas = new List<int>();
        s.Resized += deltas.Add;
        Press(s, 10);
        Move(s, 12);
        Move(s, 15);
        Move(s, 11);
        await Assert.That(deltas).IsEquivalentTo(new[] { 2, 3, -4 });
    }

    [Test]
    public async Task ReleaseStopsTracking()
    {
        var s = new GridSplitter();
        int sum = 0;
        s.Resized += d => sum += d;
        Press(s, 10);
        Move(s, 12);
        Release(s, 12);
        Move(s, 20);          // ignored — no longer dragging
        await Assert.That(sum).IsEqualTo(2);
    }

    [Test]
    public async Task LeftArrow_EmitsNegativeStep()
    {
        var s = new GridSplitter { Step = 1 };
        int sum = 0;
        s.Resized += d => sum += d;
        s.OnKeyEvent(new KeyEvent(ConsoleKey.LeftArrow, '\0', false, false, false));
        await Assert.That(sum).IsEqualTo(-1);
    }

    [Test]
    public async Task RightArrow_EmitsPositiveStep()
    {
        var s = new GridSplitter { Step = 2 };
        int sum = 0;
        s.Resized += d => sum += d;
        s.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        await Assert.That(sum).IsEqualTo(2);
    }

    [Test]
    public async Task ShiftArrow_UsesLargerStep()
    {
        var s = new GridSplitter { Step = 1 };
        int sum = 0;
        s.Resized += d => sum += d;
        s.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', Shift: true, Alt: false, Ctrl: false));
        // Shift uses max(Step, 4) → 4 here.
        await Assert.That(sum).IsEqualTo(4);
    }

    [Test]
    public async Task RenderDrawsVerticalBarGlyph()
    {
        var s = new GridSplitter();
        using var buffer = new CellBuffer(10, 5);
        s.Render(buffer, new Rect(3, 0, 1, 5));
        // Every row of the column should carry a vertical-rule glyph (│ when unfocused).
        for (int y = 0; y < 5; y++)
        {
            var c = buffer.GetCell(3, y);
            await Assert.That((char)c.Codepoint).IsEqualTo('│');
        }
    }
}
