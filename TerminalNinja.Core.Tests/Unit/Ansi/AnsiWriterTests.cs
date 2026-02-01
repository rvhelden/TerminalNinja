using System.Text;
using TerminalNinja.Core.Ansi;

namespace TerminalNinja.Core.Tests.Unit.Ansi;

public class AnsiWriterTests
{
    #region Cursor Positioning Tests
    
    [Test]
    public async Task MoveTo_Origin_OutputsOneBasedCoordinates()
    {
        // Arrange & Act
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.MoveTo(0, 0));
        
        // Assert
        await Assert.That(output).IsEqualTo("\e[1;1H");
    }
    
    [Test]
    public async Task MoveTo_ArbitraryPosition_ConvertsZeroBasedToOneBased()
    {
        // Arrange & Act
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.MoveTo(5, 10));
        
        // Assert - Format is \e[row;col]H where row=y+1, col=x+1
        await Assert.That(output).IsEqualTo("\e[11;6H");
    }
    
    [Test]
    public async Task MoveTo_LargeCoordinates_FormatsCorrectly()
    {
        // Arrange & Act
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.MoveTo(99, 199));
        
        // Assert
        await Assert.That(output).IsEqualTo("\e[200;100H");
    }
    
    [Test]
    public async Task MoveTo_SamePositionTwice_OutputsOnlyOnce()
    {
        // Arrange & Act
        var output = AnsiTestHelpers.CaptureAnsiOutput(w =>
        {
            w.MoveTo(5, 10);
            w.MoveTo(5, 10);  // Same position - should be skipped
        });
        
        // Assert - Only one cursor command should be emitted
        await Assert.That(output).IsEqualTo("\e[11;6H");
    }
    
    [Test]
    public async Task MoveTo_SequentialSameRow_OptimizesSecondMove()
    {
        // Arrange & Act
        var output = AnsiTestHelpers.CaptureAnsiOutput(w =>
        {
            w.MoveTo(0, 0);     // \e[1;1H
            w.WriteChar('A');   // Cursor advances to (1, 0)
            w.MoveTo(1, 0);     // Already at position - optimized away
            w.WriteChar('B');   // Just write the character
        });
        
        // Assert - Only one cursor command needed since writes are truly consecutive
        await Assert.That(output).IsEqualTo("\e[1;1HAB");
    }
    
    [Test]
    public async Task MoveTo_DifferentRows_AlwaysOutputsCursorCommand()
    {
        // Arrange & Act
        var output = AnsiTestHelpers.CaptureAnsiOutput(w =>
        {
            w.MoveTo(0, 0);
            w.MoveTo(0, 1);  // Different row - must output cursor command
        });
        
        // Assert
        await Assert.That(output).IsEqualTo("\e[1;1H\e[2;1H");
    }
    
    [Test]
    public async Task MoveTo_NonConsecutivePositions_AlwaysOutputsCursorCommand()
    {
        // This test reproduces the bug where the optimization in MoveTo() incorrectly
        // assumes the cursor is at x+1 when it's actually not (due to skipped cells in diffing)
        
        // Arrange & Act
        var output = AnsiTestHelpers.CaptureAnsiOutput(w =>
        {
            // Simulate what happens during diffing:
            // 1. Write cell at position 5
            w.MoveTo(5, 0);
            w.WriteChar('A');   // _cursorX is now 6 (WriteChar increments it)
            
            // 2. Skip position 6 (unchanged cell, no write)
            
            // 3. Try to write at position 7
            // The buggy optimization will trigger: _cursorY == 0 && _cursorX (6) + 1 == 7
            // So it skips emitting the cursor positioning command
            // But the REAL terminal cursor is still at position 6, not 7!
            w.MoveTo(7, 0);     // BUG: optimization skips this, but terminal cursor is at 6, not 7
            w.WriteChar('B');   // This will write at position 6, not 7!
        });
        
        // Assert - Must have TWO cursor commands to properly position both characters
        var cursorCommands = AnsiTestHelpers.CountOccurrences(output, "\e[");
        await Assert.That(cursorCommands).IsEqualTo(2);
        await Assert.That(output).IsEqualTo("\e[1;6HA\e[1;8HB");
    }
    
    [Test]
    public async Task WriteCell_WithSkippedPositions_PositionsCorrectly()
    {
        // This test simulates the exact scenario from the bug report:
        // When updating a status bar with diffing, some cells are unchanged and skipped
        
        // Arrange
        var cell1 = new Cell('C', Color.White, Color.Black);
        var cell2 = new Cell('l', Color.White, Color.Black);
        var cell3 = new Cell('s', Color.White, Color.Black);  // At position 10, should be at 10 not wrong place
        
        // Act - Simulate differential rendering with gaps
        var output = AnsiTestHelpers.CaptureAnsiOutput(w =>
        {
            // Write "Cl" at positions 0-1
            w.WriteCell(0, 0, cell1);
            w.WriteCell(1, 0, cell2);
            
            // Skip positions 2-9 (unchanged cells in diff)
            
            // Write "s" at position 10
            w.WriteCell(10, 0, cell3);
        });
        
        // Assert - The character 's' must be positioned at column 10 (11 in 1-based ANSI)
        // Without the fix, the optimization would skip the MoveTo and write 's' at wrong position
        await Assert.That(output).Contains("\e[1;11H");  // Must reposition to column 11
    }
    
    #endregion
    
    #region Color Setting Tests
    
    [Test]
    public async Task SetForeground_Red_OutputsCorrectAnsiSequence()
    {
        // Arrange & Act
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.SetForeground(Color.Red));
        
        // Assert - \e[38;2;255;0;0m
        await Assert.That(output).IsEqualTo("\e[38;2;255;0;0m");
    }
    
    [Test]
    public async Task SetForeground_CustomColor_OutputsRgbValues()
    {
        // Arrange & Act
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.SetForeground(new Color(123, 45, 67)));
        
        // Assert
        await Assert.That(output).IsEqualTo("\e[38;2;123;45;67m");
    }
    
    [Test]
    public async Task SetBackground_Blue_OutputsCorrectAnsiSequence()
    {
        // Arrange & Act
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.SetBackground(Color.Blue));
        
        // Assert - \e[48;2;0;0;255m
        await Assert.That(output).IsEqualTo("\e[48;2;0;0;255m");
    }
    
    [Test]
    public async Task SetBackground_CustomColor_OutputsRgbValues()
    {
        // Arrange & Act
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.SetBackground(new Color(89, 156, 234)));
        
        // Assert
        await Assert.That(output).IsEqualTo("\e[48;2;89;156;234m");
    }
    
    [Test]
    public async Task SetForeground_SameColorTwice_OutputsOnlyOnce()
    {
        // Arrange & Act
        var output = AnsiTestHelpers.CaptureAnsiOutput(w =>
        {
            w.SetForeground(Color.Red);
            w.SetForeground(Color.Red);  // Same color - should be skipped
        });
        
        // Assert - Only one color command
        await Assert.That(output).IsEqualTo("\e[38;2;255;0;0m");
    }
    
    [Test]
    public async Task SetBackground_SameColorTwice_OutputsOnlyOnce()
    {
        // Arrange & Act
        var output = AnsiTestHelpers.CaptureAnsiOutput(w =>
        {
            w.SetBackground(Color.Green);
            w.SetBackground(Color.Green);  // Same color - should be skipped
        });
        
        // Assert - Only one color command
        await Assert.That(output).IsEqualTo("\e[48;2;0;255;0m");
    }
    
    [Test]
    public async Task SetForegroundAndBackground_BothOutput()
    {
        // Arrange & Act
        var output = AnsiTestHelpers.CaptureAnsiOutput(w =>
        {
            w.SetForeground(Color.White);
            w.SetBackground(Color.Black);
        });
        
        // Assert
        await Assert.That(output).IsEqualTo("\e[38;2;255;255;255m\e[48;2;0;0;0m");
    }
    
    #endregion
    
    #region WriteCell Tests
    
    [Test]
    public async Task WriteCell_OutputsCompleteSequence()
    {
        // Arrange
        var cell = new Cell('X', Color.Red, Color.Blue);
        
        // Act
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.WriteCell(5, 10, cell));
        
        // Assert - Position + Foreground + Background + Character
        await Assert.That(output).IsEqualTo("\e[11;6H\e[38;2;255;0;0m\e[48;2;0;0;255mX");
    }
    
    [Test]
    public async Task WriteCell_MultipleCells_OptimizesSequentialWrites()
    {
        // Arrange
        var cell1 = new Cell('A', Color.White, Color.Black);
        var cell2 = new Cell('B', Color.White, Color.Black);  // Same colors
        
        // Act
        var output = AnsiTestHelpers.CaptureAnsiOutput(w =>
        {
            w.WriteCell(0, 0, cell1);
            w.WriteCell(1, 0, cell2);  // Sequential position, same colors
        });
        
        // Assert - First cell: full sequence. Second cell: just character (optimized)
        await Assert.That(output).IsEqualTo("\e[1;1H\e[38;2;255;255;255m\e[48;2;0;0;0mAB");
    }
    
    [Test]
    public async Task WriteCell_DifferentColors_OutputsBothColorCommands()
    {
        // Arrange
        var cell1 = new Cell('A', Color.Red, Color.Black);
        var cell2 = new Cell('B', Color.Blue, Color.White);
        
        // Act
        var output = AnsiTestHelpers.CaptureAnsiOutput(w =>
        {
            w.WriteCell(0, 0, cell1);
            w.WriteCell(1, 0, cell2);
        });
        
        // Assert - Second cell outputs new colors
        await Assert.That(output).IsEqualTo("\e[1;1H\e[38;2;255;0;0m\e[48;2;0;0;0mA\e[38;2;0;0;255m\e[48;2;255;255;255mB");
    }
    
    #endregion
    
    #region Terminal Control Tests
    
    [Test]
    public async Task HideCursor_OutputsCorrectSequence()
    {
        // Arrange & Act
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.HideCursor());
        
        // Assert
        await Assert.That(output).IsEqualTo("\e[?25l");
    }
    
    [Test]
    public async Task ShowCursor_OutputsCorrectSequence()
    {
        // Arrange & Act
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.ShowCursor());
        
        // Assert
        await Assert.That(output).IsEqualTo("\e[?25h");
    }
    
    [Test]
    public async Task ClearScreen_OutputsCorrectSequence()
    {
        // Arrange & Act
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.ClearScreen());
        
        // Assert - Clear screen and move to home
        await Assert.That(output).IsEqualTo("\e[2J\e[H");
    }
    
    [Test]
    public async Task Reset_OutputsCorrectSequence()
    {
        // Arrange & Act
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.Reset());
        
        // Assert
        await Assert.That(output).IsEqualTo("\e[0m");
    }
    
    [Test]
    public async Task Reset_ClearsStyleTracking()
    {
        // Arrange & Act
        var output = AnsiTestHelpers.CaptureAnsiOutput(w =>
        {
            w.SetForeground(Color.Red);     // First time - outputs
            w.Reset();                      // Reset style
            w.SetForeground(Color.Red);     // After reset - must output again
        });
        
        // Assert - Color command appears twice (before and after reset)
        await Assert.That(output).IsEqualTo("\e[38;2;255;0;0m\e[0m\e[38;2;255;0;0m");
    }
    
    #endregion
    
    #region Integer Formatting Tests
    
    [Test]
    [MethodDataSource(nameof(GetSingleDigitCases))]
    public async Task MoveTo_SingleDigitCoordinates_FormatsCorrectly(int value)
    {
        // Arrange & Act
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.MoveTo(value, 0));
        
        // Assert - Single digit should be output directly
        var expected = $"\e[1;{value + 1}H";
        await Assert.That(output).IsEqualTo(expected);
    }
    
    public static IEnumerable<int> GetSingleDigitCases()
    {
        for (int i = 0; i < 10; i++)
            yield return i;
    }
    
    [Test]
    public async Task MoveTo_TwoDigitCoordinates_FormatsCorrectly()
    {
        // Arrange & Act
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.MoveTo(42, 99));
        
        // Assert
        await Assert.That(output).IsEqualTo("\e[100;43H");
    }
    
    [Test]
    public async Task SetForeground_ThreeDigitRgbValues_FormatsCorrectly()
    {
        // Arrange & Act
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.SetForeground(new Color(255, 128, 99)));
        
        // Assert - All three-digit values
        await Assert.That(output).IsEqualTo("\e[38;2;255;128;99m");
    }
    
    [Test]
    public async Task MoveTo_LargeCoordinates_FormatsMultipleDigits()
    {
        // Arrange & Act - Test 4+ digit numbers
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.MoveTo(999, 1234));
        
        // Assert
        await Assert.That(output).IsEqualTo("\e[1235;1000H");
    }
    
    #endregion
    
    #region UTF-8 Character Encoding Tests
    
    [Test]
    public async Task WriteChar_AsciiCharacter_OutputsSingleByte()
    {
        // Arrange & Act
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.WriteChar('A'));
        
        // Assert - ASCII 'A' is 0x41
        await Assert.That(output).IsEqualTo("A");
        await Assert.That(Encoding.UTF8.GetBytes(output).Length).IsEqualTo(1);
    }
    
    [Test]
    [MethodDataSource(nameof(GetBoxDrawingCharacters))]
    public async Task WriteChar_BoxDrawingCharacter_OutputsUtf8Sequence(char boxChar, string description)
    {
        // Arrange & Act
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.WriteChar(boxChar));
        
        // Assert - Box drawing chars are multi-byte UTF-8
        await Assert.That(output).IsEqualTo(boxChar.ToString());
        await Assert.That(Encoding.UTF8.GetBytes(output).Length).IsGreaterThan(1);
    }
    
    public static IEnumerable<(char, string)> GetBoxDrawingCharacters()
    {
        yield return ('─', "Horizontal line");
        yield return ('│', "Vertical line");
        yield return ('┌', "Top-left corner");
        yield return ('┐', "Top-right corner");
        yield return ('└', "Bottom-left corner");
        yield return ('┘', "Bottom-right corner");
        yield return ('├', "Left T-junction");
        yield return ('┤', "Right T-junction");
        yield return ('┬', "Top T-junction");
        yield return ('┴', "Bottom T-junction");
        yield return ('┼', "Cross");
    }
    
    [Test]
    public async Task WriteCell_WithBoxDrawingChar_EncodesCorrectly()
    {
        // Arrange
        var cell = new Cell('┌', Color.White, Color.Black);
        
        // Act
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.WriteCell(0, 0, cell));
        
        // Assert - Should contain UTF-8 encoded box character
        await Assert.That(output).Contains("┌");
        var bytes = Encoding.UTF8.GetBytes(output);
        await Assert.That(bytes.Length).IsGreaterThan(10);  // Position + colors + multi-byte char
    }
    
    #endregion
    
    #region Edge Cases and Buffer Tests
    
    [Test]
    public async Task Flush_EmptyWriter_DoesNotThrow()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = new AnsiWriter(stream);
        
        // Act & Assert - Should not throw
        writer.Flush();
        
        await Assert.That(stream.Length).IsEqualTo(0);
    }
    
    [Test]
    public async Task Dispose_FlushesBuffer()
    {
        // Arrange
        using var stream = new MemoryStream();
        
        // Act
        using (var writer = new AnsiWriter(stream))
        {
            writer.MoveTo(0, 0);
            // Don't flush manually - let Dispose do it
        }
        
        // Assert - Buffer should have been flushed on dispose
        var output = Encoding.UTF8.GetString(stream.ToArray());
        await Assert.That(output).IsEqualTo("\e[1;1H");
    }
    
    [Test]
    public async Task MultipleOperations_ProducesCorrectSequence()
    {
        // Arrange & Act - Complex sequence
        var output = AnsiTestHelpers.CaptureAnsiOutput(w =>
        {
            w.HideCursor();
            w.ClearScreen();
            w.MoveTo(10, 5);
            w.SetForeground(Color.Yellow);
            w.SetBackground(Color.Blue);
            w.WriteChar('★');
            w.ShowCursor();
        });
        
        // Assert - All operations in sequence
        await Assert.That(output).Contains("\e[?25l");      // Hide cursor
        await Assert.That(output).Contains("\e[2J\e[H");    // Clear screen
        await Assert.That(output).Contains("\e[6;11H");     // Move to position
        await Assert.That(output).Contains("\e[38;2;255;255;0m");  // Yellow foreground
        await Assert.That(output).Contains("\e[48;2;0;0;255m");    // Blue background
        await Assert.That(output).Contains("★");            // Character
        await Assert.That(output).Contains("\e[?25h");      // Show cursor
    }
    
    #endregion
}
