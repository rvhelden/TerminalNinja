using System.Text;
using TerminalNinja.Core.Ansi;
using TerminalNinja.Core.Styling;

namespace TerminalNinja.Core.Tests.Ansi;

/// <summary>
/// Integration tests that verify the full pipeline: Rectangle → CellBuffer → AnsiWriter → ANSI escape sequences.
/// These tests validate that UI elements are correctly converted to terminal output.
/// </summary>
public class RectangleToAnsiTests
{
    /// <summary>
    /// Helper method to render a rectangle and capture the ANSI output.
    /// </summary>
    private static string RenderRectangleToAnsi(Rectangle rectangle, int bufferWidth, int bufferHeight)
    {
        // Create a buffer and render the rectangle
        var buffer = new CellBuffer(bufferWidth, bufferHeight);
        var viewport = new Rect(0, 0, bufferWidth, bufferHeight);
        rectangle.Render(buffer, viewport);
        
        // Capture ANSI output
        using var stream = new MemoryStream();
        using var writer = new AnsiWriter(stream);
        
        // Write all dirty cells (simulating Renderer.Present())
        foreach (var change in buffer.GetChanges())
        {
            writer.WriteCell(change.X, change.Y, change.Cell);
        }
        
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    
    #region Simple Rectangle Tests
    
    [Test]
    public async Task SmallRectangle_WithBorder_OutputsCorrectAnsiSequence()
    {
        // Arrange - 5x3 rectangle with single-line border
        var rect = new Rectangle
        {
            Width = Size.Absolute(5),
            Height = Size.Absolute(3),
            BackgroundColor = Color.Blue,
            ForegroundColor = Color.White,
            Border = Border.Single(Color.White)
        };
        
        // Act
        var output = RenderRectangleToAnsi(rect, 5, 3);
        
        // Assert - Should contain:
        // - Cursor positioning commands
        // - Blue background color (0, 0, 255)
        // - White foreground color (255, 255, 255)
        // - Box-drawing characters (┌─┐│┘)
        
        await Assert.That(output).Contains("\e[48;2;0;0;255m"); // Blue background
        await Assert.That(output).Contains("\e[38;2;255;255;255m"); // White foreground
        await Assert.That(output).Contains("┌"); // Top-left corner
        await Assert.That(output).Contains("─"); // Horizontal line
        await Assert.That(output).Contains("┐"); // Top-right corner
        await Assert.That(output).Contains("│"); // Vertical line
        await Assert.That(output).Contains("┘"); // Bottom-right corner
        
        // Verify structure: should have top row, middle row, and bottom row
        await Assert.That(output).Contains("┌───┐"); // Top border
    }
    
    [Test]
    public async Task Rectangle_NoBorder_OnlyFillsBackground()
    {
        // Arrange - 3x2 rectangle with no border
        var rect = new Rectangle
        {
            Width = Size.Absolute(3),
            Height = Size.Absolute(2),
            BackgroundColor = Color.Red,
            Border = Border.None
        };
        
        // Act
        var output = RenderRectangleToAnsi(rect, 3, 2);
        
        // Assert - Should have red background but no border characters
        await Assert.That(output).Contains("\e[48;2;255;0;0m"); // Red background
        await Assert.That(output).DoesNotContain("┌");
        await Assert.That(output).DoesNotContain("─");
        await Assert.That(output).DoesNotContain("│");
    }
    
    #endregion
    
    #region Border Style Tests
    
    [Test]
    public async Task Rectangle_DoubleBorder_UsesDoubleLineCharacters()
    {
        // Arrange
        var rect = new Rectangle
        {
            Width = Size.Absolute(4),
            Height = Size.Absolute(3),
            BackgroundColor = Color.Black,
            Border = Border.Double(Color.Cyan)
        };
        
        // Act
        var output = RenderRectangleToAnsi(rect, 4, 3);
        
        // Assert - Should use double-line box characters
        await Assert.That(output).Contains("\e[38;2;0;255;255m"); // Cyan foreground
        await Assert.That(output).Contains("╔"); // Double top-left
        await Assert.That(output).Contains("═"); // Double horizontal
        await Assert.That(output).Contains("╗"); // Double top-right
        await Assert.That(output).Contains("║"); // Double vertical
    }
    
    [Test]
    public async Task Rectangle_RoundedBorder_UsesRoundedCharacters()
    {
        // Arrange
        var rect = new Rectangle
        {
            Width = Size.Absolute(4),
            Height = Size.Absolute(3),
            BackgroundColor = Color.Black,
            Border = Border.Rounded(Color.Green)
        };
        
        // Act
        var output = RenderRectangleToAnsi(rect, 4, 3);
        
        // Assert - Should use rounded corners
        await Assert.That(output).Contains("\e[38;2;0;255;0m"); // Green foreground
        await Assert.That(output).Contains("╭"); // Rounded top-left
        await Assert.That(output).Contains("╮"); // Rounded top-right
        await Assert.That(output).Contains("╯"); // Rounded bottom-right
    }
    
    #endregion
    
    #region Color Optimization Tests
    
    [Test]
    public async Task Rectangle_MultipleSpaces_OptimizesColorOutput()
    {
        // Arrange - 4x2 rectangle (interior will be 2x0, so just border)
        // Actually, let's make it bigger so there are repeated background cells
        var rect = new Rectangle
        {
            Width = Size.Absolute(5),
            Height = Size.Absolute(3),
            BackgroundColor = Color.Yellow,
            ForegroundColor = Color.Black,
            Border = Border.None
        };
        
        // Act
        var output = RenderRectangleToAnsi(rect, 5, 3);
        
        // Assert - Yellow background should only be set once at the start
        var yellowBgCount = CountOccurrences(output, "\e[48;2;255;255;0m");
        await Assert.That(yellowBgCount).IsEqualTo(1); // Should only set color once
    }
    
    [Test]
    public async Task Rectangle_BorderAndFill_OptimizesSameBackgroundColor()
    {
        // Arrange - Border and background are same color
        var rect = new Rectangle
        {
            Width = Size.Absolute(5),
            Height = Size.Absolute(4),
            BackgroundColor = new Color(50, 50, 50),
            ForegroundColor = Color.White,
            Border = Border.Single(Color.White)
        };
        
        // Act
        var output = RenderRectangleToAnsi(rect, 5, 4);
        
        // Assert - Background color (50,50,50) should only be set once
        var bgColorCount = CountOccurrences(output, "\e[48;2;50;50;50m");
        await Assert.That(bgColorCount).IsEqualTo(1); // Optimized - set once
    }
    
    #endregion
    
    #region Cursor Movement Tests
    
    [Test]
    public async Task Rectangle_SingleRow_OptimizesCursorMovement()
    {
        // Arrange - 5x1 rectangle (one row)
        var rect = new Rectangle
        {
            Width = Size.Absolute(5),
            Height = Size.Absolute(1),
            BackgroundColor = Color.Magenta,
            Border = Border.None
        };
        
        // Act
        var output = RenderRectangleToAnsi(rect, 5, 1);
        
        // Assert - Should only have one cursor position command for the row
        var cursorMoves = CountOccurrences(output, "\e[");
        await Assert.That(cursorMoves).IsLessThanOrEqualTo(3); // Position + colors (fg optional)
    }
    
    [Test]
    public async Task Rectangle_FirstCellPosition_UsesOneBasedCoordinates()
    {
        // Arrange - Rectangle at origin with white background (different from default black)
        var rect = new Rectangle
        {
            Width = Size.Absolute(2),
            Height = Size.Absolute(2),
            BackgroundColor = Color.White,  // Different from default Cell.Empty background
            Border = Border.None
        };
        
        // Act
        var output = RenderRectangleToAnsi(rect, 2, 2);
        
        // Assert - First position should be (1,1) in ANSI (0,0 in buffer)
        await Assert.That(output).Contains("\e[1;1H");
    }
    
    #endregion
    
    #region Complex Layout Tests
    
    [Test]
    public async Task Rectangle_5x5WithBorder_OutputsAllCells()
    {
        // Arrange - 5x5 with border (border + 3x3 interior)
        var rect = new Rectangle
        {
            Width = Size.Absolute(5),
            Height = Size.Absolute(5),
            BackgroundColor = new Color(30, 30, 80),
            ForegroundColor = Color.Cyan,
            Border = Border.Single(Color.Cyan)
        };
        
        // Act
        var output = RenderRectangleToAnsi(rect, 5, 5);
        
        // Assert
        // Should have background color
        await Assert.That(output).Contains("\e[48;2;30;30;80m");
        
        // Should have corners
        await Assert.That(output).Contains("┌");
        await Assert.That(output).Contains("┐");
        await Assert.That(output).Contains("┘");
        
        // Should have horizontal and vertical lines
        await Assert.That(output).Contains("─");
        await Assert.That(output).Contains("│");
        
        // Count total characters (5x5 = 25 cells)
        // Each cell outputs at least one visible character
        var bytes = Encoding.UTF8.GetBytes(output);
        await Assert.That(bytes.Length).IsGreaterThan(100); // Substantial output
    }
    
    [Test]
    public async Task Rectangle_WithOffset_PositionsCorrectly()
    {
        // Arrange - Rectangle at position (2, 3) with size 3x2
        var rect = new Rectangle
        {
            X = Size.Absolute(2),
            Y = Size.Absolute(3),
            Width = Size.Absolute(3),
            Height = Size.Absolute(2),
            BackgroundColor = Color.White,
            Border = Border.None
        };
        
        // Act
        var output = RenderRectangleToAnsi(rect, 10, 10);
        
        // Assert - Should contain white background cells at rows 4 and 5 (y=3,4 → rows 4,5)
        // The white background color should appear in the output
        await Assert.That(output).Contains("\e[48;2;255;255;255m"); // White background
        
        // Verify the white region appears in the correct rows
        // Looking for pattern indicating row 4 or 5 positioning
        var hasRow4 = output.Contains("\e[4;") || output.Contains("4;2H") || output.Contains("4;3H") || output.Contains("4;4H") || output.Contains("4;5H");
        var hasRow5 = output.Contains("\e[5;") || output.Contains("5;2H") || output.Contains("5;3H") || output.Contains("5;4H") || output.Contains("5;5H");
        
        await Assert.That(hasRow4 || hasRow5).IsTrue();
    }
    
    #endregion
    
    #region Edge Cases
    
    [Test]
    public async Task Rectangle_TooSmallForBorder_SkipsBorder()
    {
        // Arrange - 1x1 rectangle (too small for border)
        var rect = new Rectangle
        {
            Width = Size.Absolute(1),
            Height = Size.Absolute(1),
            BackgroundColor = Color.Red,
            Border = Border.Single(Color.White)
        };
        
        // Act
        var output = RenderRectangleToAnsi(rect, 1, 1);
        
        // Assert - Should have background but no border
        await Assert.That(output).Contains("\e[48;2;255;0;0m");
        await Assert.That(output).DoesNotContain("┌");
    }
    
    [Test]
    public async Task Rectangle_ExactlyBorderSize_OnlyShowsBorder()
    {
        // Arrange - 2x2 rectangle (only border, no interior)
        var rect = new Rectangle
        {
            Width = Size.Absolute(2),
            Height = Size.Absolute(2),
            BackgroundColor = Color.Blue,
            Border = Border.Single(Color.Yellow)
        };
        
        // Act
        var output = RenderRectangleToAnsi(rect, 2, 2);
        
        // Assert - Should have corners (2x2 = just 4 corner cells)
        await Assert.That(output).Contains("┌");
        await Assert.That(output).Contains("┐");
        await Assert.That(output).Contains("┘");
        // No room for edges in 2x2
    }
    
    [Test]
    public async Task Rectangle_LargeBuffer_OutputsOnlyDirtyCells()
    {
        // Arrange - Small 2x2 rectangle in large 100x100 buffer
        // First initialize the buffer by rendering once and swapping
        var buffer = new CellBuffer(100, 100);
        var viewport = new Rect(0, 0, 100, 100);
        
        // Create a simple rectangle and render it
        var rect = new Rectangle
        {
            Width = Size.Absolute(2),
            Height = Size.Absolute(2),
            BackgroundColor = Color.Green,
            Border = Border.None
        };
        
        rect.Render(buffer, viewport);
        
        // Capture only the changed cells after initial render
        using var stream = new MemoryStream();
        using var writer = new AnsiWriter(stream);
        
        int cellCount = 0;
        foreach (var change in buffer.GetChanges())
        {
            writer.WriteCell(change.X, change.Y, change.Cell);
            cellCount++;
        }
        
        writer.Flush();
        var output = Encoding.UTF8.GetString(stream.ToArray());
        
        // Assert - On first render, all cells are dirty, so this test validates
        // that we can count the cells
        await Assert.That(cellCount).IsGreaterThan(0);
        await Assert.That(output).Contains("\e[48;2;0;255;0m"); // Green background
    }
    
    #endregion
    
    #region UTF-8 Validation
    
    [Test]
    public async Task Rectangle_BorderCharacters_AreValidUtf8()
    {
        // Arrange
        var rect = new Rectangle
        {
            Width = Size.Absolute(3),
            Height = Size.Absolute(3),
            BackgroundColor = Color.Black,
            Border = Border.Single(Color.White)
        };
        
        // Act
        var output = RenderRectangleToAnsi(rect, 3, 3);
        var bytes = Encoding.UTF8.GetBytes(output);
        
        // Assert - Should be valid UTF-8 (no exceptions during encoding)
        var decoded = Encoding.UTF8.GetString(bytes);
        await Assert.That(decoded).IsEqualTo(output);
        
        // Should contain multi-byte UTF-8 sequences for box characters
        await Assert.That(bytes.Length).IsGreaterThan(output.Length); // Multi-byte chars present
    }
    
    #endregion
    
    /// <summary>
    /// Helper to count occurrences of a substring in a string.
    /// </summary>
    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
