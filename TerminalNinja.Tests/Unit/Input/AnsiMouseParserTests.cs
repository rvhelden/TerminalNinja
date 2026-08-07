using TerminalNinja.Platform.Unix;

namespace TerminalNinja.Tests.Unit.Input;

/// <summary>
/// Tests for the incremental ANSI mouse report parser: fed the characters that follow an ESC,
/// it must recognise SGR and X10 reports and reject everything else so the caller can replay
/// non-mouse input as the key presses it was.
/// </summary>
public class AnsiMouseParserTests
{
    private static (AnsiMouseParser.Status Status, MouseEvent? Result) FeedAll(string sequence)
    {
        var parser = new AnsiMouseParser();
        var status = AnsiMouseParser.Status.Failed;
        foreach (var c in sequence)
        {
            status = parser.Feed(c);
            if (status != AnsiMouseParser.Status.Pending)
            {
                return (status, parser.Result);
            }
        }

        return (status, parser.Result);
    }

    [Test]
    public async Task Sgr_LeftPress_Decodes()
    {
        var (status, result) = FeedAll("[<0;10;5M");

        await Assert.That(status).IsEqualTo(AnsiMouseParser.Status.Matched);
        await Assert.That(result).IsEqualTo(new MouseEvent(9, 4, MouseButton.Left, MouseAction.Press));
    }

    [Test]
    public async Task Sgr_LeftRelease_LowercaseFinal_Decodes()
    {
        var (status, result) = FeedAll("[<0;3;4m");

        await Assert.That(status).IsEqualTo(AnsiMouseParser.Status.Matched);
        await Assert.That(result).IsEqualTo(new MouseEvent(2, 3, MouseButton.Left, MouseAction.Release));
    }

    [Test]
    public async Task Sgr_RightPress_Decodes()
    {
        var (_, result) = FeedAll("[<2;1;1M");
        await Assert.That(result).IsEqualTo(new MouseEvent(0, 0, MouseButton.Right, MouseAction.Press));
    }

    [Test]
    public async Task Sgr_MotionWithoutButton_IsMove()
    {
        // 35 = 32 (motion) | 3 (no button) — any-event tracking pointer movement.
        var (_, result) = FeedAll("[<35;10;5M");
        await Assert.That(result).IsEqualTo(new MouseEvent(9, 4, MouseButton.None, MouseAction.Move));
    }

    [Test]
    public async Task Sgr_LeftDrag_IsMoveWithButton()
    {
        // 32 = motion | left button held.
        var (_, result) = FeedAll("[<32;2;2M");
        await Assert.That(result).IsEqualTo(new MouseEvent(1, 1, MouseButton.Left, MouseAction.Move));
    }

    [Test]
    public async Task Sgr_Wheel_Decodes()
    {
        var (_, up) = FeedAll("[<64;7;8M");
        var (_, down) = FeedAll("[<65;7;8M");

        await Assert.That(up).IsEqualTo(new MouseEvent(6, 7, MouseButton.None, MouseAction.ScrollUp));
        await Assert.That(down).IsEqualTo(new MouseEvent(6, 7, MouseButton.None, MouseAction.ScrollDown));
    }

    [Test]
    public async Task Sgr_ModifierBits_Populate()
    {
        // 16 = ctrl, 4 = shift, 8 = alt, on a left press.
        var (_, ctrl) = FeedAll("[<16;1;1M");
        var (_, shiftAlt) = FeedAll("[<12;1;1M");

        await Assert.That(ctrl).IsEqualTo(new MouseEvent(0, 0, MouseButton.Left, MouseAction.Press, Ctrl: true));
        await Assert.That(shiftAlt).IsEqualTo(new MouseEvent(0, 0, MouseButton.Left, MouseAction.Press, Shift: true, Alt: true));
    }

    [Test]
    public async Task Sgr_LargeCoordinates_Decode()
    {
        var (_, result) = FeedAll("[<0;223;145M");
        await Assert.That(result).IsEqualTo(new MouseEvent(222, 144, MouseButton.Left, MouseAction.Press));
    }

    [Test]
    public async Task Sgr_MalformedParameters_MatchAsSwallowed_NotReplayed()
    {
        // Two parameters instead of three: a complete but meaningless report. It must be
        // consumed (Matched, null result) — replaying its digits as keys is the phantom-press bug.
        var (status, result) = FeedAll("[<0;10M");

        await Assert.That(status).IsEqualTo(AnsiMouseParser.Status.Matched);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task NonBracket_FailsImmediately()
    {
        // Alt+b arrives as ESC 'b' — must be replayable as keys.
        var parser = new AnsiMouseParser();
        await Assert.That(parser.Feed('b')).IsEqualTo(AnsiMouseParser.Status.Failed);
    }

    [Test]
    public async Task NonMouseCsi_Fails()
    {
        var (status, _) = FeedAll("[A");
        await Assert.That(status).IsEqualTo(AnsiMouseParser.Status.Failed);
    }

    [Test]
    public async Task Sgr_UnterminatedGarbage_Fails()
    {
        var (status, _) = FeedAll("[<12;34x");
        await Assert.That(status).IsEqualTo(AnsiMouseParser.Status.Failed);
    }

    [Test]
    public async Task Sgr_OverlongParameters_Fail()
    {
        var (status, _) = FeedAll("[<" + new string('1', 40) + "M");
        await Assert.That(status).IsEqualTo(AnsiMouseParser.Status.Failed);
    }

    [Test]
    public async Task X10_Press_Decodes()
    {
        // X10: '[M' then 32+Cb, 32+Cx, 32+Cy with 1-based coordinates.
        // Left press (Cb=0) at column 3, row 4: ' ' (32), '#' (35), '$' (36).
        var (status, result) = FeedAll("[M #$");

        await Assert.That(status).IsEqualTo(AnsiMouseParser.Status.Matched);
        await Assert.That(result).IsEqualTo(new MouseEvent(2, 3, MouseButton.Left, MouseAction.Press));
    }

    [Test]
    public async Task X10_Release_Decodes()
    {
        // Cb=3 is the X10 release marker (button not reported): '#' (35).
        var (_, result) = FeedAll("[M##$");
        await Assert.That(result).IsEqualTo(new MouseEvent(2, 3, MouseButton.None, MouseAction.Release));
    }
}
