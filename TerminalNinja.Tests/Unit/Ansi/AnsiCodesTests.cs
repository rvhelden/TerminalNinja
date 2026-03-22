using System.Text;

namespace TerminalNinja.Tests.Unit.Ansi;

/// <summary>
/// Tests for AnsiCodes static class covering:
/// - Escape character constant
/// - All ANSI escape sequence properties
/// - Correct byte sequences
/// </summary>
public class AnsiCodesTests
{
    [Test]
    public async Task Escape_IsCorrectCharacter()
    {
        // Arrange & Act
        var escape = TerminalNinja.Ansi.AnsiCodes.Escape;

        // Assert
        await Assert.That(escape).IsEqualTo('\u001B'); // ESC character
        await Assert.That((int)escape).IsEqualTo(27); // ESC is ASCII 27
    }

    [Test]
    public async Task Reset_ReturnsCorrectSequence()
    {
        // Arrange & Act
        var reset = TerminalNinja.Ansi.AnsiCodes.Reset;
        var str = Encoding.UTF8.GetString(reset);

        // Assert
        await Assert.That(str).IsEqualTo("\e[0m");
    }

    [Test]
    public async Task ClearScreen_ReturnsCorrectSequence()
    {
        // Arrange & Act
        var clearScreen = TerminalNinja.Ansi.AnsiCodes.ClearScreen;
        var str = Encoding.UTF8.GetString(clearScreen);

        // Assert
        await Assert.That(str).IsEqualTo("\e[2J");
    }

    [Test]
    public async Task ClearScreenAndHome_ReturnsCorrectSequence()
    {
        // Arrange & Act
        var clearAndHome = TerminalNinja.Ansi.AnsiCodes.ClearScreenAndHome;
        var str = Encoding.UTF8.GetString(clearAndHome);

        // Assert
        await Assert.That(str).IsEqualTo("\e[2J\e[H");
    }

    [Test]
    public async Task HideCursor_ReturnsCorrectSequence()
    {
        // Arrange & Act
        var hideCursor = TerminalNinja.Ansi.AnsiCodes.HideCursor;
        var str = Encoding.UTF8.GetString(hideCursor);

        // Assert
        await Assert.That(str).IsEqualTo("\e[?25l");
    }

    [Test]
    public async Task ShowCursor_ReturnsCorrectSequence()
    {
        // Arrange & Act
        var showCursor = TerminalNinja.Ansi.AnsiCodes.ShowCursor;
        var str = Encoding.UTF8.GetString(showCursor);

        // Assert
        await Assert.That(str).IsEqualTo("\e[?25h");
    }

    [Test]
    public async Task Home_ReturnsCorrectSequence()
    {
        // Arrange & Act
        var home = TerminalNinja.Ansi.AnsiCodes.Home;
        var str = Encoding.UTF8.GetString(home);

        // Assert
        await Assert.That(str).IsEqualTo("\e[H");
    }

    [Test]
    public async Task EscapeStart_ReturnsCorrectSequence()
    {
        // Arrange & Act
        var escapeStart = TerminalNinja.Ansi.AnsiCodes.EscapeStart;
        var str = Encoding.UTF8.GetString(escapeStart);

        // Assert
        await Assert.That(str).IsEqualTo("\e[");
    }

    [Test]
    public async Task ForegroundPrefix_ReturnsCorrectSequence()
    {
        // Arrange & Act
        var fgPrefix = TerminalNinja.Ansi.AnsiCodes.ForegroundPrefix;
        var str = Encoding.UTF8.GetString(fgPrefix);

        // Assert
        await Assert.That(str).IsEqualTo("\e[38;2;");
    }

    [Test]
    public async Task BackgroundPrefix_ReturnsCorrectSequence()
    {
        // Arrange & Act
        var bgPrefix = TerminalNinja.Ansi.AnsiCodes.BackgroundPrefix;
        var str = Encoding.UTF8.GetString(bgPrefix);

        // Assert
        await Assert.That(str).IsEqualTo("\e[48;2;");
    }
}
