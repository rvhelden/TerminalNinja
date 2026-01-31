using System.Text;

namespace TerminalNinja.Core.Tests.Unit.Ansi;

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
        var escape = Core.Ansi.AnsiCodes.Escape;

        // Assert
        await Assert.That(escape).IsEqualTo('\u001B'); // ESC character
        await Assert.That((int)escape).IsEqualTo(27); // ESC is ASCII 27
    }

    [Test]
    public async Task Reset_ReturnsCorrectSequence()
    {
        // Arrange & Act
        var reset = Core.Ansi.AnsiCodes.Reset;
        var str = Encoding.UTF8.GetString(reset);

        // Assert
        await Assert.That(str).IsEqualTo("\u001B[0m");
    }

    [Test]
    public async Task ClearScreen_ReturnsCorrectSequence()
    {
        // Arrange & Act
        var clearScreen = Core.Ansi.AnsiCodes.ClearScreen;
        var str = Encoding.UTF8.GetString(clearScreen);

        // Assert
        await Assert.That(str).IsEqualTo("\u001B[2J");
    }

    [Test]
    public async Task ClearScreenAndHome_ReturnsCorrectSequence()
    {
        // Arrange & Act
        var clearAndHome = Core.Ansi.AnsiCodes.ClearScreenAndHome;
        var str = Encoding.UTF8.GetString(clearAndHome);

        // Assert
        await Assert.That(str).IsEqualTo("\u001B[2J\u001B[H");
    }

    [Test]
    public async Task HideCursor_ReturnsCorrectSequence()
    {
        // Arrange & Act
        var hideCursor = Core.Ansi.AnsiCodes.HideCursor;
        var str = Encoding.UTF8.GetString(hideCursor);

        // Assert
        await Assert.That(str).IsEqualTo("\u001B[?25l");
    }

    [Test]
    public async Task ShowCursor_ReturnsCorrectSequence()
    {
        // Arrange & Act
        var showCursor = Core.Ansi.AnsiCodes.ShowCursor;
        var str = Encoding.UTF8.GetString(showCursor);

        // Assert
        await Assert.That(str).IsEqualTo("\u001B[?25h");
    }

    [Test]
    public async Task Home_ReturnsCorrectSequence()
    {
        // Arrange & Act
        var home = Core.Ansi.AnsiCodes.Home;
        var str = Encoding.UTF8.GetString(home);

        // Assert
        await Assert.That(str).IsEqualTo("\u001B[H");
    }

    [Test]
    public async Task EscapeStart_ReturnsCorrectSequence()
    {
        // Arrange & Act
        var escapeStart = Core.Ansi.AnsiCodes.EscapeStart;
        var str = Encoding.UTF8.GetString(escapeStart);

        // Assert
        await Assert.That(str).IsEqualTo("\u001B[");
    }

    [Test]
    public async Task ForegroundPrefix_ReturnsCorrectSequence()
    {
        // Arrange & Act
        var fgPrefix = Core.Ansi.AnsiCodes.ForegroundPrefix;
        var str = Encoding.UTF8.GetString(fgPrefix);

        // Assert
        await Assert.That(str).IsEqualTo("\u001B[38;2;");
    }

    [Test]
    public async Task BackgroundPrefix_ReturnsCorrectSequence()
    {
        // Arrange & Act
        var bgPrefix = Core.Ansi.AnsiCodes.BackgroundPrefix;
        var str = Encoding.UTF8.GetString(bgPrefix);

        // Assert
        await Assert.That(str).IsEqualTo("\u001B[48;2;");
    }
}
