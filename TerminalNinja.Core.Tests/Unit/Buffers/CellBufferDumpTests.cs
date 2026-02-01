namespace TerminalNinja.Core.Tests.Unit.Buffers;

/// <summary>
/// Tests for CellBuffer.DumpToString() functionality.
/// </summary>
public class CellBufferDumpTests
{
    [Test]
    public async Task DumpToString_EmptyBuffer_ReturnsHeaderAndRuler()
    {
        // Arrange
        var buffer = new CellBuffer(10, 5);
        
        // Act
        var dump = buffer.DumpToString();
        
        // Assert
        await Assert.That(dump).Contains("=== TERMINAL BUFFER DUMP ===");
        await Assert.That(dump).Contains("Dimensions: 10 x 5");
        await Assert.That(dump).Contains("=== COLOR INFORMATION ===");
    }
    
    [Test]
    public async Task DumpToString_WithContent_ShowsCharacters()
    {
        // Arrange
        var buffer = new CellBuffer(10, 5);
        buffer.SetChar(0, 0, 'H', Color.White, Color.Black);
        buffer.SetChar(1, 0, 'i', Color.White, Color.Black);
        
        // Act
        var dump = buffer.DumpToString();
        
        // Assert
        await Assert.That(dump).Contains("Hi");
    }
    
    [Test]
    public async Task DumpToString_WithColors_ShowsColorInfo()
    {
        // Arrange
        var buffer = new CellBuffer(10, 5);
        buffer.SetChar(0, 0, 'X', Color.Red, Color.Blue);
        
        // Act
        var dump = buffer.DumpToString();
        
        // Assert
        await Assert.That(dump).Contains("FG:Red");
        await Assert.That(dump).Contains("BG:Blue");
    }
    
    [Test]
    public async Task DumpToString_IncludesTimestamp()
    {
        // Arrange
        var buffer = new CellBuffer(10, 5);
        
        // Act
        var dump = buffer.DumpToString();
        
        // Assert
        await Assert.That(dump).Contains("Timestamp:");
    }
}
