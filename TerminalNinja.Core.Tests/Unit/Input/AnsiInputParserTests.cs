using TerminalNinja.Core.Input;

namespace TerminalNinja.Core.Tests.Unit.Input;

/// <summary>
/// Tests for ANSI escape sequence parsing.
/// </summary>
public class AnsiInputParserTests
{
    #region SGR Mouse Parsing

    [Test]
    public async Task TryParseSgrMouse_LeftButtonPress_ParsesCorrectly()
    {
        // ESC[<0;10;5M = Left button press at column 10, row 5
        var input = "\x1b[<0;10;5M";
        
        var result = AnsiInputParser.TryParseSgrMouse(input, out var consumed);
        
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Button).IsEqualTo(MouseButton.Left);
        await Assert.That(result.Action).IsEqualTo(MouseAction.Press);
        await Assert.That(result.X).IsEqualTo(9);  // 1-based to 0-based
        await Assert.That(result.Y).IsEqualTo(4);  // 1-based to 0-based
        await Assert.That(consumed).IsEqualTo(10);
    }

    [Test]
    public async Task TryParseSgrMouse_LeftButtonRelease_ParsesCorrectly()
    {
        // ESC[<0;10;5m = Left button release
        var input = "\x1b[<0;10;5m";
        
        var result = AnsiInputParser.TryParseSgrMouse(input, out var consumed);
        
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Button).IsEqualTo(MouseButton.Left);
        await Assert.That(result.Action).IsEqualTo(MouseAction.Release);
        await Assert.That(consumed).IsEqualTo(10);
    }

    [Test]
    public async Task TryParseSgrMouse_MiddleButton_ParsesCorrectly()
    {
        // ESC[<1;15;8M = Middle button press
        var input = "\x1b[<1;15;8M";
        
        var result = AnsiInputParser.TryParseSgrMouse(input, out _);
        
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Button).IsEqualTo(MouseButton.Middle);
        await Assert.That(result.Action).IsEqualTo(MouseAction.Press);
    }

    [Test]
    public async Task TryParseSgrMouse_RightButton_ParsesCorrectly()
    {
        // ESC[<2;20;12M = Right button press
        var input = "\x1b[<2;20;12M";
        
        var result = AnsiInputParser.TryParseSgrMouse(input, out _);
        
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Button).IsEqualTo(MouseButton.Right);
        await Assert.That(result.Action).IsEqualTo(MouseAction.Press);
    }

    [Test]
    public async Task TryParseSgrMouse_MouseMove_ParsesCorrectly()
    {
        // ESC[<35;25;10M = Mouse move (button code 35 = 32 + 3 for move)
        var input = "\x1b[<35;25;10M";
        
        var result = AnsiInputParser.TryParseSgrMouse(input, out _);
        
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Action).IsEqualTo(MouseAction.Move);
        await Assert.That(result.X).IsEqualTo(24);
        await Assert.That(result.Y).IsEqualTo(9);
    }

    [Test]
    public async Task TryParseSgrMouse_ScrollUp_ParsesCorrectly()
    {
        // ESC[<64;10;5M = Scroll up
        var input = "\x1b[<64;10;5M";
        
        var result = AnsiInputParser.TryParseSgrMouse(input, out _);
        
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Action).IsEqualTo(MouseAction.ScrollUp);
        await Assert.That(result.Button).IsEqualTo(MouseButton.None);
    }

    [Test]
    public async Task TryParseSgrMouse_ScrollDown_ParsesCorrectly()
    {
        // ESC[<65;10;5M = Scroll down
        var input = "\x1b[<65;10;5M";
        
        var result = AnsiInputParser.TryParseSgrMouse(input, out _);
        
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Action).IsEqualTo(MouseAction.ScrollDown);
        await Assert.That(result.Button).IsEqualTo(MouseButton.None);
    }

    [Test]
    public async Task TryParseSgrMouse_InvalidSequence_ReturnsNull()
    {
        var input = "\x1b[<abc;10;5M"; // Invalid number
        
        var result = AnsiInputParser.TryParseSgrMouse(input, out var consumed);
        
        await Assert.That(result).IsNull();
        await Assert.That(consumed).IsEqualTo(0);
    }

    [Test]
    public async Task TryParseSgrMouse_IncompleteSequence_ReturnsNull()
    {
        var input = "\x1b[<0;10"; // Incomplete
        
        var result = AnsiInputParser.TryParseSgrMouse(input, out _);
        
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task TryParseSgrMouse_NotSgrFormat_ReturnsNull()
    {
        var input = "\x1b[A"; // Arrow key, not mouse
        
        var result = AnsiInputParser.TryParseSgrMouse(input, out _);
        
        await Assert.That(result).IsNull();
    }

    #endregion

    #region Keyboard Escape Sequences

    [Test]
    public async Task TryParseKeyEscape_UpArrow_ParsesCorrectly()
    {
        var input = "\x1b[A";
        
        var result = AnsiInputParser.TryParseKeyEscape(input, out var consumed);
        
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Key).IsEqualTo(ConsoleKey.UpArrow);
        await Assert.That(consumed).IsEqualTo(3);
    }

    [Test]
    public async Task TryParseKeyEscape_DownArrow_ParsesCorrectly()
    {
        var input = "\x1b[B";
        
        var result = AnsiInputParser.TryParseKeyEscape(input, out _);
        
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Key).IsEqualTo(ConsoleKey.DownArrow);
    }

    [Test]
    public async Task TryParseKeyEscape_RightArrow_ParsesCorrectly()
    {
        var input = "\x1b[C";
        
        var result = AnsiInputParser.TryParseKeyEscape(input, out _);
        
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Key).IsEqualTo(ConsoleKey.RightArrow);
    }

    [Test]
    public async Task TryParseKeyEscape_LeftArrow_ParsesCorrectly()
    {
        var input = "\x1b[D";
        
        var result = AnsiInputParser.TryParseKeyEscape(input, out _);
        
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Key).IsEqualTo(ConsoleKey.LeftArrow);
    }

    [Test]
    public async Task TryParseKeyEscape_Home_ParsesCorrectly()
    {
        var input = "\x1b[H";
        
        var result = AnsiInputParser.TryParseKeyEscape(input, out _);
        
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Key).IsEqualTo(ConsoleKey.Home);
    }

    [Test]
    public async Task TryParseKeyEscape_End_ParsesCorrectly()
    {
        var input = "\x1b[F";
        
        var result = AnsiInputParser.TryParseKeyEscape(input, out _);
        
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Key).IsEqualTo(ConsoleKey.End);
    }

    [Test]
    public async Task TryParseKeyEscape_Delete_ParsesCorrectly()
    {
        var input = "\x1b[3~";
        
        var result = AnsiInputParser.TryParseKeyEscape(input, out var consumed);
        
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Key).IsEqualTo(ConsoleKey.Delete);
        await Assert.That(consumed).IsEqualTo(4);
    }

    [Test]
    public async Task TryParseKeyEscape_F1_ParsesCorrectly()
    {
        var input = "\x1bOP";
        
        var result = AnsiInputParser.TryParseKeyEscape(input, out var consumed);
        
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Key).IsEqualTo(ConsoleKey.F1);
        await Assert.That(consumed).IsEqualTo(3);
    }

    [Test]
    public async Task TryParseKeyEscape_EscapeKey_ParsesCorrectly()
    {
        var input = "\x1b";
        
        var result = AnsiInputParser.TryParseKeyEscape(input, out var consumed);
        
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Key).IsEqualTo(ConsoleKey.Escape);
        await Assert.That(consumed).IsEqualTo(1);
    }

    [Test]
    public async Task TryParseKeyEscape_InvalidSequence_ReturnsNull()
    {
        var input = "\x1b[Z"; // Unknown sequence
        
        var result = AnsiInputParser.TryParseKeyEscape(input, out _);
        
        await Assert.That(result).IsNull();
    }

    #endregion
}
