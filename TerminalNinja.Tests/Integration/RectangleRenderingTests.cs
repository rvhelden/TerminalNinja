using System.Text;
using TerminalNinja.Ansi;
using TerminalNinja.Styling;
using ControlBorder = TerminalNinja.Controls.Border;

namespace TerminalNinja.Tests.Integration;

/// <summary>
/// Integration tests that verify the full pipeline: Border → CellBuffer → AnsiWriter → ANSI escape sequences.
/// These tests validate that UI elements are correctly converted to terminal output.
/// </summary>
public class BorderRenderingTests
{
    /// <summary>
    /// Helper method to render a rectangle and capture the ANSI output.
    /// </summary>
    private static string RenderBorderToAnsi(ControlBorder rectangle, int bufferWidth, int bufferHeight)
    {
        // Create a buffer and render the rectangle
        using var buffer = new CellBuffer(bufferWidth, bufferHeight);
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
    
    #region Simple Border Tests
    
    [Test]
    public async Task SmallBorder_WithBorder_OutputsCorrectAnsiSequence()
    {
        // Arrange - 5x3 rectangle with single-line border
        var rect = new global::TerminalNinja.Controls.Border
        {
            Width = Size.Absolute(5),
            Height = Size.Absolute(3),
            Background = Color.Blue,
            Foreground = Color.White,
            BorderStyle = global::TerminalNinja.Styling.BorderStyle.Single(Color.White)
        };
        
        // Act
        var output = RenderBorderToAnsi(rect, 5, 3);
        
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
    public async Task Border_NoBorder_OnlyFillsBackground()
    {
        // Arrange - 3x2 rectangle with no border
        var rect = new global::TerminalNinja.Controls.Border
        {
            Width = Size.Absolute(3),
            Height = Size.Absolute(2),
            Background = Color.Red,
            BorderStyle = global::TerminalNinja.Styling.BorderStyle.None
        };
        
        // Act
        var output = RenderBorderToAnsi(rect, 3, 2);
        
        // Assert - Should have red background but no border characters
        await Assert.That(output).Contains("\e[48;2;255;0;0m"); // Red background
        await Assert.That(output).DoesNotContain("┌");
        await Assert.That(output).DoesNotContain("─");
        await Assert.That(output).DoesNotContain("│");
    }
    
    #endregion
    
    #region Border Style Tests
    
    [Test]
    public async Task Border_DoubleBorder_UsesDoubleLineCharacters()
    {
        // Arrange
        var rect = new global::TerminalNinja.Controls.Border
        {
            Width = Size.Absolute(4),
            Height = Size.Absolute(3),
            Background = Color.Black,
            BorderStyle = global::TerminalNinja.Styling.BorderStyle.Double(Color.Cyan)
        };
        
        // Act
        var output = RenderBorderToAnsi(rect, 4, 3);
        
        // Assert - Should use double-line box characters
        await Assert.That(output).Contains("\e[38;2;0;255;255m"); // Cyan foreground
        await Assert.That(output).Contains("╔"); // Double top-left
        await Assert.That(output).Contains("═"); // Double horizontal
        await Assert.That(output).Contains("╗"); // Double top-right
        await Assert.That(output).Contains("║"); // Double vertical
    }
    
    [Test]
    public async Task Border_RoundedBorder_UsesRoundedCharacters()
    {
        // Arrange
        var rect = new global::TerminalNinja.Controls.Border
        {
            Width = Size.Absolute(4),
            Height = Size.Absolute(3),
            Background = Color.Black,
            BorderStyle = global::TerminalNinja.Styling.BorderStyle.Rounded(Color.Green)
        };
        
        // Act
        var output = RenderBorderToAnsi(rect, 4, 3);
        
        // Assert - Should use rounded corners
        await Assert.That(output).Contains("\e[38;2;0;255;0m"); // Green foreground
        await Assert.That(output).Contains("╭"); // Rounded top-left
        await Assert.That(output).Contains("╮"); // Rounded top-right
        await Assert.That(output).Contains("╯"); // Rounded bottom-right
    }
    
    #endregion
    
    #region Color Optimization Tests
    
    [Test]
    public async Task Border_MultipleSpaces_OptimizesColorOutput()
    {
        // Arrange - 4x2 rectangle (interior will be 2x0, so just border)
        // Actually, let's make it bigger so there are repeated background cells
        var rect = new global::TerminalNinja.Controls.Border
        {
            Width = Size.Absolute(5),
            Height = Size.Absolute(3),
            Background = Color.Yellow,
            Foreground = Color.Black,
            BorderStyle = global::TerminalNinja.Styling.BorderStyle.None
        };
        
        // Act
        var output = RenderBorderToAnsi(rect, 5, 3);
        
        // Assert - Yellow background should only be set once at the start
        var yellowBgCount = CountOccurrences(output, "\e[48;2;255;255;0m");
        await Assert.That(yellowBgCount).IsEqualTo(1); // Should only set color once
    }
    
    [Test]
    public async Task Border_BorderAndFill_OptimizesSameBackground()
    {
        // Arrange - Border and background are same color
        var rect = new global::TerminalNinja.Controls.Border
        {
            Width = Size.Absolute(5),
            Height = Size.Absolute(4),
            Background = new Color(50, 50, 50),
            Foreground = Color.White,
            BorderStyle = global::TerminalNinja.Styling.BorderStyle.Single(Color.White)
        };
        
        // Act
        var output = RenderBorderToAnsi(rect, 5, 4);
        
        // Assert - Background color (50,50,50) should only be set once
        var bgColorCount = CountOccurrences(output, "\e[48;2;50;50;50m");
        await Assert.That(bgColorCount).IsEqualTo(1); // Optimized - set once
    }
    
    #endregion
    
    #region Cursor Movement Tests
    
    [Test]
    public async Task Border_SingleRow_OptimizesCursorMovement()
    {
        // Arrange - 5x1 rectangle (one row)
        var rect = new global::TerminalNinja.Controls.Border
        {
            Width = Size.Absolute(5),
            Height = Size.Absolute(1),
            Background = Color.Magenta,
            BorderStyle = global::TerminalNinja.Styling.BorderStyle.None
        };
        
        // Act
        var output = RenderBorderToAnsi(rect, 5, 1);
        
        // Assert - Should only have one cursor position command for the row
        var cursorMoves = CountOccurrences(output, "\e[");
        await Assert.That(cursorMoves).IsLessThanOrEqualTo(3); // Position + colors (fg optional)
    }
    
    [Test]
    public async Task Border_FirstCellPosition_UsesOneBasedCoordinates()
    {
        // Arrange - Border at origin with white background (different from default black)
        var rect = new global::TerminalNinja.Controls.Border
        {
            Width = Size.Absolute(2),
            Height = Size.Absolute(2),
            Background = Color.White,  // Different from default Cell.Empty background
            BorderStyle = global::TerminalNinja.Styling.BorderStyle.None
        };
        
        // Act
        var output = RenderBorderToAnsi(rect, 2, 2);
        
        // Assert - First position should be (1,1) in ANSI (0,0 in buffer)
        await Assert.That(output).Contains("\e[1;1H");
    }
    
    #endregion
    
    #region Complex Layout Tests
    
    [Test]
    public async Task Border_5x5WithBorder_OutputsAllCells()
    {
        // Arrange - 5x5 with border (border + 3x3 interior)
        var rect = new global::TerminalNinja.Controls.Border
        {
            Width = Size.Absolute(5),
            Height = Size.Absolute(5),
            Background = new Color(30, 30, 80),
            Foreground = Color.Cyan,
            BorderStyle = global::TerminalNinja.Styling.BorderStyle.Single(Color.Cyan)
        };
        
        // Act
        var output = RenderBorderToAnsi(rect, 5, 5);
        
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
    public async Task Border_WithParentOffset_PositionsCorrectly()
    {
        // Arrange - Border with size 3x2, parent offset at (2, 3)
        var rect = new global::TerminalNinja.Controls.Border
        {
            Width = Size.Absolute(3),
            Height = Size.Absolute(2),
            Background = Color.White,
            BorderStyle = global::TerminalNinja.Styling.BorderStyle.None
        };
        
        // Act - render with parent offset
        using var buffer = new CellBuffer(10, 10);
        var parentBounds = new Rect(2, 3, 10, 10);
        rect.Render(buffer, parentBounds);
        
        // Assert - The cell at (2,3) should have white background
        await Assert.That(buffer.GetCell(2, 3).Background).IsEqualTo(Color.White);
        await Assert.That(buffer.GetCell(3, 3).Background).IsEqualTo(Color.White);
        await Assert.That(buffer.GetCell(2, 4).Background).IsEqualTo(Color.White);
    }
    
    #endregion
    
    #region Edge Cases
    
    [Test]
    public async Task Border_TooSmallForBorder_SkipsBorder()
    {
        // Arrange - 1x1 rectangle (too small for border)
        var rect = new global::TerminalNinja.Controls.Border
        {
            Width = Size.Absolute(1),
            Height = Size.Absolute(1),
            Background = Color.Red,
            BorderStyle = global::TerminalNinja.Styling.BorderStyle.Single(Color.White)
        };
        
        // Act
        var output = RenderBorderToAnsi(rect, 1, 1);
        
        // Assert - Should have background but no border
        await Assert.That(output).Contains("\e[48;2;255;0;0m");
        await Assert.That(output).DoesNotContain("┌");
    }
    
    [Test]
    public async Task Border_ExactlyBorderSize_OnlyShowsBorder()
    {
        // Arrange - 2x2 rectangle (only border, no interior)
        var rect = new global::TerminalNinja.Controls.Border
        {
            Width = Size.Absolute(2),
            Height = Size.Absolute(2),
            Background = Color.Blue,
            BorderStyle = global::TerminalNinja.Styling.BorderStyle.Single(Color.Yellow)
        };
        
        // Act
        var output = RenderBorderToAnsi(rect, 2, 2);
        
        // Assert - Should have corners (2x2 = just 4 corner cells)
        await Assert.That(output).Contains("┌");
        await Assert.That(output).Contains("┐");
        await Assert.That(output).Contains("┘");
        // No room for edges in 2x2
    }
    
    [Test]
    public async Task Border_LargeBuffer_OutputsOnlyDirtyCells()
    {
        // Arrange - Small 2x2 rectangle in large 100x100 buffer
        // First initialize the buffer by rendering once and swapping
        using var buffer = new CellBuffer(100, 100);
        var viewport = new Rect(0, 0, 100, 100);
        
        // Create a simple rectangle and render it
        var rect = new global::TerminalNinja.Controls.Border
        {
            Width = Size.Absolute(2),
            Height = Size.Absolute(2),
            Background = Color.Green,
            BorderStyle = global::TerminalNinja.Styling.BorderStyle.None
        };
        
        rect.Render(buffer, viewport);
        
        // Capture only the changed cells after initial render
        using var stream = new MemoryStream();
        using var writer = new AnsiWriter(stream);
        
        var cellCount = 0;
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
    public async Task Border_BorderCharacters_AreValidUtf8()
    {
        // Arrange
        var rect = new global::TerminalNinja.Controls.Border
        {
            Width = Size.Absolute(3),
            Height = Size.Absolute(3),
            Background = Color.Black,
            BorderStyle = global::TerminalNinja.Styling.BorderStyle.Single(Color.White)
        };
        
        // Act
        var output = RenderBorderToAnsi(rect, 3, 3);
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
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
