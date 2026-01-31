namespace TerminalNinja.Core.Tests.Unit.Ansi;

/// <summary>
/// Tests for AnsiStyle struct covering:
/// - Initial state (unset)
/// - NeedsForeground and NeedsBackground logic
/// - Update method
/// - Reset method
/// - State tracking for optimization
/// </summary>
public class AnsiStyleTests
{
    [Test]
    public async Task Default_AnsiStyle_HasNothingSet()
    {
        // Arrange & Act
        var style = new Core.Ansi.AnsiStyle();

        // Assert
        await Assert.That(style.ForegroundSet).IsFalse();
        await Assert.That(style.BackgroundSet).IsFalse();
    }

    [Test]
    public async Task NeedsForeground_WhenNotSet_ReturnsTrue()
    {
        // Arrange
        var style = new Core.Ansi.AnsiStyle();
        var color = Color.Red;

        // Act
        var needs = style.NeedsForeground(color);

        // Assert
        await Assert.That(needs).IsTrue();
    }

    [Test]
    public async Task NeedsForeground_WhenSetToDifferentColor_ReturnsTrue()
    {
        // Arrange
        var style = new Core.Ansi.AnsiStyle();
        style.Update(Color.Red, Color.Black);

        // Act
        var needs = style.NeedsForeground(Color.Blue);

        // Assert
        await Assert.That(needs).IsTrue();
    }

    [Test]
    public async Task NeedsForeground_WhenSetToSameColor_ReturnsFalse()
    {
        // Arrange
        var style = new Core.Ansi.AnsiStyle();
        style.Update(Color.Red, Color.Black);

        // Act
        var needs = style.NeedsForeground(Color.Red);

        // Assert
        await Assert.That(needs).IsFalse();
    }

    [Test]
    public async Task NeedsBackground_WhenNotSet_ReturnsTrue()
    {
        // Arrange
        var style = new Core.Ansi.AnsiStyle();
        var color = Color.Green;

        // Act
        var needs = style.NeedsBackground(color);

        // Assert
        await Assert.That(needs).IsTrue();
    }

    [Test]
    public async Task NeedsBackground_WhenSetToDifferentColor_ReturnsTrue()
    {
        // Arrange
        var style = new Core.Ansi.AnsiStyle();
        style.Update(Color.White, Color.Green);

        // Act
        var needs = style.NeedsBackground(Color.Blue);

        // Assert
        await Assert.That(needs).IsTrue();
    }

    [Test]
    public async Task NeedsBackground_WhenSetToSameColor_ReturnsFalse()
    {
        // Arrange
        var style = new Core.Ansi.AnsiStyle();
        style.Update(Color.White, Color.Green);

        // Act
        var needs = style.NeedsBackground(Color.Green);

        // Assert
        await Assert.That(needs).IsFalse();
    }

    [Test]
    public async Task Update_SetsBothColorsAndFlags()
    {
        // Arrange
        var style = new Core.Ansi.AnsiStyle();

        // Act
        style.Update(Color.Cyan, Color.Magenta);

        // Assert
        await Assert.That(style.Foreground).IsEqualTo(Color.Cyan);
        await Assert.That(style.Background).IsEqualTo(Color.Magenta);
        await Assert.That(style.ForegroundSet).IsTrue();
        await Assert.That(style.BackgroundSet).IsTrue();
    }

    [Test]
    public async Task Reset_ClearsSetFlags()
    {
        // Arrange
        var style = new Core.Ansi.AnsiStyle();
        style.Update(Color.Red, Color.Blue);

        // Act
        style.Reset();

        // Assert
        await Assert.That(style.ForegroundSet).IsFalse();
        await Assert.That(style.BackgroundSet).IsFalse();
        // Note: The color values themselves are not cleared, only the flags
    }

    [Test]
    public async Task AfterReset_NeedsAllColors()
    {
        // Arrange
        var style = new Core.Ansi.AnsiStyle();
        style.Update(Color.Red, Color.Blue);
        style.Reset();

        // Act & Assert
        await Assert.That(style.NeedsForeground(Color.Red)).IsTrue();
        await Assert.That(style.NeedsBackground(Color.Blue)).IsTrue();
    }

    [Test]
    public async Task MultipleUpdates_TrackCurrentState()
    {
        // Arrange
        var style = new Core.Ansi.AnsiStyle();

        // Act - First update
        style.Update(Color.Red, Color.Black);
        var needsRed = style.NeedsForeground(Color.Red);
        var needsBlue = style.NeedsForeground(Color.Blue);

        // Act - Second update
        style.Update(Color.Blue, Color.White);
        var needsBlueAfter = style.NeedsForeground(Color.Blue);
        var needsRedAfter = style.NeedsForeground(Color.Red);

        // Assert
        await Assert.That(needsRed).IsFalse();  // Red was just set
        await Assert.That(needsBlue).IsTrue();  // Blue was not set yet
        await Assert.That(needsBlueAfter).IsFalse();  // Blue is now set
        await Assert.That(needsRedAfter).IsTrue();  // Red is no longer current
    }
}
