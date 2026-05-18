using System.Text;
using TerminalNinja.Primitives;
using TerminalNinja.Terminal;

namespace TerminalNinja.Terminal.Tests.Unit;

/// <summary>
/// End-to-end tests for <see cref="TerminalScreenBuffer"/>: feed ANSI byte streams
/// through <see cref="VtParser"/> into the buffer and assert on the resulting cell
/// grid, cursor position, SGR state, and metadata (title).
/// </summary>
public class TerminalScreenBufferTests
{
    private static (VtParser parser, TerminalScreenBuffer buffer) Setup(int rows = 5, int cols = 20)
    {
        var buffer = new TerminalScreenBuffer(rows, cols);
        return (new VtParser(), buffer);
    }

    private static void Feed(VtParser parser, TerminalScreenBuffer buffer, string ascii)
    {
        parser.Feed(Encoding.ASCII.GetBytes(ascii), buffer);
    }

    [Test]
    public async Task Print_AsciiText_FillsCellsAndAdvancesCursor()
    {
        var (p, b) = Setup();
        Feed(p, b, "Hi");

        await Assert.That(b.GetCell(0, 0).Codepoint).IsEqualTo((uint)'H');
        await Assert.That(b.GetCell(0, 1).Codepoint).IsEqualTo((uint)'i');
        await Assert.That(b.CursorRow).IsEqualTo(0);
        await Assert.That(b.CursorCol).IsEqualTo(2);
    }

    [Test]
    public async Task Print_PastRightEdge_WrapsToNextRow()
    {
        var (p, b) = Setup(rows: 5, cols: 3);
        Feed(p, b, "abcd");

        await Assert.That(b.GetCell(0, 0).Codepoint).IsEqualTo((uint)'a');
        await Assert.That(b.GetCell(0, 1).Codepoint).IsEqualTo((uint)'b');
        await Assert.That(b.GetCell(0, 2).Codepoint).IsEqualTo((uint)'c');
        await Assert.That(b.GetCell(1, 0).Codepoint).IsEqualTo((uint)'d');
        await Assert.That(b.CursorRow).IsEqualTo(1);
        await Assert.That(b.CursorCol).IsEqualTo(1);
    }

    [Test]
    public async Task Execute_CRLF_MovesCursorToNextRowColumnZero()
    {
        var (p, b) = Setup();
        Feed(p, b, "a\r\nb");

        await Assert.That(b.GetCell(0, 0).Codepoint).IsEqualTo((uint)'a');
        await Assert.That(b.GetCell(1, 0).Codepoint).IsEqualTo((uint)'b');
        await Assert.That(b.CursorRow).IsEqualTo(1);
        await Assert.That(b.CursorCol).IsEqualTo(1);
    }

    [Test]
    public async Task Execute_Backspace_MovesCursorBack()
    {
        var (p, b) = Setup();
        Feed(p, b, "ab\bc");

        // After 'a','b' cursor is at col 2. BS moves to col 1. Then 'c' writes there.
        await Assert.That(b.GetCell(0, 1).Codepoint).IsEqualTo((uint)'c');
        await Assert.That(b.CursorCol).IsEqualTo(2);
    }

    [Test]
    public async Task Csi_CUP_PlacesCursorOneBasedToZeroBased()
    {
        // ESC [ 3 ; 5 H — row 3, col 5 (1-based) → (2, 4) (0-based)
        var (p, b) = Setup();
        p.Feed("\x1B[3;5H"u8, b);

        await Assert.That(b.CursorRow).IsEqualTo(2);
        await Assert.That(b.CursorCol).IsEqualTo(4);
    }

    [Test]
    public async Task Csi_CUP_OutOfBounds_Clamps()
    {
        var (p, b) = Setup(rows: 5, cols: 20);
        p.Feed("\x1B[99;99H"u8, b);

        await Assert.That(b.CursorRow).IsEqualTo(4);
        await Assert.That(b.CursorCol).IsEqualTo(19);
    }

    [Test]
    public async Task Csi_CursorMoves_UpDownForwardBack()
    {
        var (p, b) = Setup();
        p.Feed("\x1B[3;5H"u8, b); // start at (2, 4)
        p.Feed("\x1B[A"u8, b); // up 1
        await Assert.That((b.CursorRow, b.CursorCol)).IsEqualTo((1, 4));

        p.Feed("\x1B[2B"u8, b); // down 2
        await Assert.That((b.CursorRow, b.CursorCol)).IsEqualTo((3, 4));

        p.Feed("\x1B[3C"u8, b); // forward 3
        await Assert.That((b.CursorRow, b.CursorCol)).IsEqualTo((3, 7));

        p.Feed("\x1B[5D"u8, b); // back 5
        await Assert.That((b.CursorRow, b.CursorCol)).IsEqualTo((3, 2));
    }

    [Test]
    public async Task Sgr_FgRed_BgYellow_AppliedToFollowingPrint()
    {
        var (p, b) = Setup();
        p.Feed("\x1B[31;43m"u8, b); // red fg, yellow bg
        Feed(p, b, "X");

        var cell = b.GetCell(0, 0);
        await Assert.That(cell.Foreground.R).IsEqualTo((byte)0xCD);
        await Assert.That(cell.Background.G).IsEqualTo((byte)0xCD);
    }

    [Test]
    public async Task Sgr_BrightForeground_UsesBrightPalette()
    {
        var (p, b) = Setup();
        p.Feed("\x1B[91m"u8, b); // bright red fg
        Feed(p, b, "X");

        var cell = b.GetCell(0, 0);
        await Assert.That(cell.Foreground.R).IsEqualTo((byte)0xFF);
    }

    [Test]
    public async Task Sgr_Truecolor_38_2_AppliesExactRgb()
    {
        var (p, b) = Setup();
        p.Feed("\x1B[38;2;10;20;30m"u8, b);
        Feed(p, b, "X");

        var cell = b.GetCell(0, 0);
        await Assert.That(cell.Foreground.R).IsEqualTo((byte)10);
        await Assert.That(cell.Foreground.G).IsEqualTo((byte)20);
        await Assert.That(cell.Foreground.B).IsEqualTo((byte)30);
    }

    [Test]
    public async Task Sgr_Palette256_UsesCubeMapping()
    {
        // Palette index 196 = 6,6,6 cube — actually 196 - 16 = 180 → r=5, g=0, b=0 → (255,0,0).
        var (p, b) = Setup();
        p.Feed("\x1B[38;5;196m"u8, b);
        Feed(p, b, "X");

        var cell = b.GetCell(0, 0);
        await Assert.That(cell.Foreground.R).IsEqualTo((byte)255);
        await Assert.That(cell.Foreground.G).IsEqualTo((byte)0);
        await Assert.That(cell.Foreground.B).IsEqualTo((byte)0);
    }

    [Test]
    public async Task Sgr_BoldAndUnderline_SetThenClear()
    {
        var (p, b) = Setup();
        p.Feed("\x1B[1;4m"u8, b); // bold + underline
        Feed(p, b, "A");
        p.Feed("\x1B[22m"u8, b); // unbold
        Feed(p, b, "B");

        await Assert.That(b.GetCell(0, 0).Decorations & TextDecorations.Bold).IsEqualTo(TextDecorations.Bold);
        await Assert.That(b.GetCell(0, 0).Decorations & TextDecorations.Underline).IsEqualTo(TextDecorations.Underline);
        await Assert.That(b.GetCell(0, 1).Decorations & TextDecorations.Bold).IsEqualTo(TextDecorations.None);
        await Assert.That(b.GetCell(0, 1).Decorations & TextDecorations.Underline).IsEqualTo(TextDecorations.Underline);
    }

    [Test]
    public async Task Sgr_Reset_ClearsToDefaults()
    {
        var (p, b) = Setup();
        p.Feed("\x1B[1;31;43m"u8, b);
        Feed(p, b, "A");
        p.Feed("\x1B[0m"u8, b);
        Feed(p, b, "B");

        var afterReset = b.GetCell(0, 1);
        await Assert.That(afterReset.Foreground).IsEqualTo(Color.White);
        await Assert.That(afterReset.Background).IsEqualTo(Color.Black);
        await Assert.That(afterReset.Decorations).IsEqualTo(TextDecorations.None);
    }

    [Test]
    public async Task Erase_J0_ClearsCursorToEndOfScreen()
    {
        var (p, b) = Setup(rows: 3, cols: 5);
        Feed(p, b, "ABCDEFGHIJKL"); // wraps; fills most of the screen
        p.Feed("\x1B[2;3H"u8, b); // cursor to (1, 2)
        p.Feed("\x1B[J"u8, b); // erase from cursor to end

        await Assert.That(b.GetCell(0, 0).Codepoint).IsEqualTo((uint)'A');
        await Assert.That(b.GetCell(1, 1).Codepoint).IsEqualTo((uint)'G');
        await Assert.That(b.GetCell(1, 2).Codepoint).IsEqualTo((uint)' ');
        await Assert.That(b.GetCell(2, 0).Codepoint).IsEqualTo((uint)' ');
    }

    [Test]
    public async Task Erase_J2_ClearsEntireScreen()
    {
        var (p, b) = Setup(rows: 3, cols: 5);
        Feed(p, b, "ABC");
        p.Feed("\x1B[2J"u8, b);

        await Assert.That(b.GetCell(0, 0).Codepoint).IsEqualTo((uint)' ');
        await Assert.That(b.GetCell(0, 1).Codepoint).IsEqualTo((uint)' ');
    }

    [Test]
    public async Task Erase_K0_ClearsCursorToEndOfLine()
    {
        var (p, b) = Setup(rows: 2, cols: 5);
        Feed(p, b, "ABCDE");
        p.Feed("\x1B[1;3H"u8, b); // back to (0, 2)
        p.Feed("\x1B[K"u8, b); // erase to end of line

        await Assert.That(b.GetCell(0, 0).Codepoint).IsEqualTo((uint)'A');
        await Assert.That(b.GetCell(0, 1).Codepoint).IsEqualTo((uint)'B');
        await Assert.That(b.GetCell(0, 2).Codepoint).IsEqualTo((uint)' ');
        await Assert.That(b.GetCell(0, 4).Codepoint).IsEqualTo((uint)' ');
    }

    [Test]
    public async Task Osc0_SetsTitle_FiresEvent()
    {
        var (p, b) = Setup();
        string? observed = null;
        b.TitleChanged += t => observed = t;

        p.Feed("\x1B]0;My Window\x07"u8, b);

        await Assert.That(b.Title).IsEqualTo("My Window");
        await Assert.That(observed).IsEqualTo("My Window");
    }

    [Test]
    public async Task DecPrivate_25_TogglesCursorVisibility()
    {
        var (p, b) = Setup();
        await Assert.That(b.CursorVisible).IsTrue();

        p.Feed("\x1B[?25l"u8, b);
        await Assert.That(b.CursorVisible).IsFalse();

        p.Feed("\x1B[?25h"u8, b);
        await Assert.That(b.CursorVisible).IsTrue();
    }

    [Test]
    public async Task LineFeed_AtBottomRow_ScrollsContentUp()
    {
        var (p, b) = Setup(rows: 3, cols: 4);
        Feed(p, b, "AAAA\r\nBBBB\r\nCCCC");
        await Assert.That(b.CursorRow).IsEqualTo(2);

        // A fourth line should scroll the first row off the top.
        p.Feed("\r\n"u8, b);
        Feed(p, b, "DDDD");

        await Assert.That(b.GetCell(0, 0).Codepoint).IsEqualTo((uint)'B');
        await Assert.That(b.GetCell(1, 0).Codepoint).IsEqualTo((uint)'C');
        await Assert.That(b.GetCell(2, 0).Codepoint).IsEqualTo((uint)'D');
    }

    [Test]
    public async Task Resize_PreservesOverlappingCells()
    {
        var (p, b) = Setup(rows: 3, cols: 5);
        Feed(p, b, "ABCDE\r\nFGHIJ");

        b.Resize(2, 3);

        await Assert.That(b.GetCell(0, 0).Codepoint).IsEqualTo((uint)'A');
        await Assert.That(b.GetCell(0, 2).Codepoint).IsEqualTo((uint)'C');
        await Assert.That(b.GetCell(1, 0).Codepoint).IsEqualTo((uint)'F');
        await Assert.That(b.GetCell(1, 2).Codepoint).IsEqualTo((uint)'H');
        await Assert.That(b.Rows).IsEqualTo(2);
        await Assert.That(b.Cols).IsEqualTo(3);
    }

    [Test]
    public async Task Print_Utf8Cjk_CodepointStored()
    {
        var (p, b) = Setup();
        // U+4E2D 中 = E4 B8 AD
        p.Feed(new byte[] { 0xE4, 0xB8, 0xAD }, b);

        await Assert.That(b.GetCell(0, 0).Codepoint).IsEqualTo(0x4E2Du);
    }

    [Test]
    public async Task Reset_HardReset_ClearsCellsCursorAndTitle()
    {
        var (p, b) = Setup();
        Feed(p, b, "Hello");
        p.Feed("\x1B]0;Title\x07"u8, b);
        // ESC c — feed raw bytes; "\x1Bc" parses as U+01BC in C#.
        p.Feed(new byte[] { 0x1B, (byte)'c' }, b);

        await Assert.That(b.GetCell(0, 0).Codepoint).IsEqualTo((uint)' ');
        await Assert.That(b.CursorRow).IsEqualTo(0);
        await Assert.That(b.CursorCol).IsEqualTo(0);
        await Assert.That(b.Title).IsEqualTo(string.Empty);
    }
}
