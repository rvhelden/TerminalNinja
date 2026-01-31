using System.Text;
using TerminalNinja.Core.Ansi;
using TerminalNinja.Core.Styling;

namespace TerminalNinja.Core.Tests.Ansi;

/// <summary>
/// Integration tests that verify the full pipeline: Stack → Children → CellBuffer → AnsiWriter → ANSI escape sequences.
/// These tests validate that Stack containers correctly layout and render child elements.
/// </summary>
public class StackToAnsiTests
{
    /// <summary>
    /// Helper method to render a stack and capture the ANSI output.
    /// </summary>
    private static string RenderStackToAnsi(Stack stack, int bufferWidth, int bufferHeight)
    {
        // Create a buffer and render the stack
        var buffer = new CellBuffer(bufferWidth, bufferHeight);
        var viewport = new Rect(0, 0, bufferWidth, bufferHeight);
        stack.Render(buffer, viewport);
        
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
    
    #region Horizontal Stack Tests
    
    [Test]
    public async Task HorizontalStack_TwoFixedChildren_RendersCorrectPositions()
    {
        // Arrange - Two 5-width rectangles side-by-side
        var stack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children =
            [
                StackChild.Fixed(new Rectangle
                {
                    Width = Size.Stretch,
                    Height = Size.Stretch,
                    BackgroundColor = Color.Red,
                    Border = Border.None
                }, 5),
                StackChild.Fixed(new Rectangle
                {
                    Width = Size.Stretch,
                    Height = Size.Stretch,
                    BackgroundColor = Color.Blue,
                    Border = Border.None
                }, 5)
            ]
        };
        
        // Act
        var output = RenderStackToAnsi(stack, 10, 3);
        
        // Assert - Should have both colors
        await Assert.That(output).Contains("\e[48;2;255;0;0m"); // Red background
        await Assert.That(output).Contains("\e[48;2;0;0;255m"); // Blue background
        
        // Verify colors appear in order (red first, then blue)
        var redIndex = output.IndexOf("\e[48;2;255;0;0m", StringComparison.Ordinal);
        var blueIndex = output.IndexOf("\e[48;2;0;0;255m", StringComparison.Ordinal);
        await Assert.That(redIndex).IsLessThan(blueIndex);
    }
    
    [Test]
    public async Task HorizontalStack_ThreeFixedWithBorders_OutputsBorderCharacters()
    {
        // Arrange - Three rectangles with different border styles
        var stack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children =
            [
                StackChild.Fixed(new Rectangle
                {
                    Width = Size.Stretch,
                    Height = Size.Stretch,
                    BackgroundColor = Color.Black,
                    Border = Border.Single(Color.Red)
                }, 5),
                StackChild.Fixed(new Rectangle
                {
                    Width = Size.Stretch,
                    Height = Size.Stretch,
                    BackgroundColor = Color.Black,
                    Border = Border.Double(Color.Green)
                }, 5),
                StackChild.Fixed(new Rectangle
                {
                    Width = Size.Stretch,
                    Height = Size.Stretch,
                    BackgroundColor = Color.Black,
                    Border = Border.Rounded(Color.Blue)
                }, 5)
            ]
        };
        
        // Act
        var output = RenderStackToAnsi(stack, 15, 4);
        
        // Assert - Should contain all three border styles
        await Assert.That(output).Contains("┌"); // Single border
        await Assert.That(output).Contains("╔"); // Double border
        await Assert.That(output).Contains("╭"); // Rounded border
        
        // Should have all three colors
        await Assert.That(output).Contains("\e[38;2;255;0;0m"); // Red
        await Assert.That(output).Contains("\e[38;2;0;255;0m"); // Green
        await Assert.That(output).Contains("\e[38;2;0;0;255m"); // Blue
    }
    
    [Test]
    public async Task HorizontalStack_StretchChildren_FillsAvailableSpace()
    {
        // Arrange - Two stretch children in 20-width buffer
        var stack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children =
            [
                StackChild.Stretch(new Rectangle
                {
                    Width = Size.Stretch,
                    Height = Size.Stretch,
                    BackgroundColor = Color.Cyan,
                    Border = Border.None
                }),
                StackChild.Stretch(new Rectangle
                {
                    Width = Size.Stretch,
                    Height = Size.Stretch,
                    BackgroundColor = Color.Magenta,
                    Border = Border.None
                })
            ]
        };
        
        // Act
        var output = RenderStackToAnsi(stack, 20, 2);
        
        // Assert - Each child should get 10 cells (20/2)
        await Assert.That(output).Contains("\e[48;2;0;255;255m"); // Cyan
        await Assert.That(output).Contains("\e[48;2;255;0;255m"); // Magenta
    }
    
    #endregion
    
    #region Vertical Stack Tests
    
    [Test]
    public async Task VerticalStack_ThreeFixedRows_RendersInOrder()
    {
        // Arrange - Three rows stacked vertically
        var stack = new Stack
        {
            Orientation = StackOrientation.Vertical,
            Children =
            [
                StackChild.Fixed(new Rectangle
                {
                    Width = Size.Stretch,
                    Height = Size.Stretch,
                    BackgroundColor = Color.Red,
                    Border = Border.None
                }, 2),
                StackChild.Fixed(new Rectangle
                {
                    Width = Size.Stretch,
                    Height = Size.Stretch,
                    BackgroundColor = Color.Green,
                    Border = Border.None
                }, 3),
                StackChild.Fixed(new Rectangle
                {
                    Width = Size.Stretch,
                    Height = Size.Stretch,
                    BackgroundColor = Color.Blue,
                    Border = Border.None
                }, 2)
            ]
        };
        
        // Act
        var output = RenderStackToAnsi(stack, 10, 7);
        
        // Assert - All three colors present
        await Assert.That(output).Contains("\e[48;2;255;0;0m"); // Red (rows 1-2)
        await Assert.That(output).Contains("\e[48;2;0;255;0m"); // Green (rows 3-5)
        await Assert.That(output).Contains("\e[48;2;0;0;255m"); // Blue (rows 6-7)
        
        // Verify order
        var redIndex = output.IndexOf("\e[48;2;255;0;0m", StringComparison.Ordinal);
        var greenIndex = output.IndexOf("\e[48;2;0;255;0m", StringComparison.Ordinal);
        var blueIndex = output.IndexOf("\e[48;2;0;0;255m", StringComparison.Ordinal);
        
        await Assert.That(redIndex).IsLessThan(greenIndex);
        await Assert.That(greenIndex).IsLessThan(blueIndex);
    }
    
    [Test]
    public async Task VerticalStack_WithBorders_StacksCorrectly()
    {
        // Arrange - Three bordered rectangles stacked vertically
        var stack = new Stack
        {
            Orientation = StackOrientation.Vertical,
            Children =
            [
                StackChild.Fixed(new Rectangle
                {
                    Width = Size.Stretch,
                    Height = Size.Stretch,
                    BackgroundColor = new Color(30, 30, 80),
                    Border = Border.Single(Color.Cyan)
                }, 3),
                StackChild.Fixed(new Rectangle
                {
                    Width = Size.Stretch,
                    Height = Size.Stretch,
                    BackgroundColor = new Color(20, 60, 20),
                    Border = Border.Double(Color.Green)
                }, 3),
                StackChild.Fixed(new Rectangle
                {
                    Width = Size.Stretch,
                    Height = Size.Stretch,
                    BackgroundColor = new Color(60, 20, 20),
                    Border = Border.Rounded(Color.Red)
                }, 3)
            ]
        };
        
        // Act
        var output = RenderStackToAnsi(stack, 10, 9);
        
        // Assert - Should have all border styles
        await Assert.That(output).Contains("┌"); // Single
        await Assert.That(output).Contains("═"); // Double horizontal
        await Assert.That(output).Contains("╮"); // Rounded corner
        
        // Should have all background colors
        await Assert.That(output).Contains("\e[48;2;30;30;80m");
        await Assert.That(output).Contains("\e[48;2;20;60;20m");
        await Assert.That(output).Contains("\e[48;2;60;20;20m");
    }
    
    #endregion
    
    #region Nested Stack Tests
    
    [Test]
    public async Task HolyGrailLayout_FullPipeline_RendersAllSections()
    {
        // Arrange - Classic Holy Grail layout
        var header = new Rectangle
        {
            Width = Size.Stretch,
            Height = Size.Stretch,
            BackgroundColor = new Color(30, 30, 80),
            Border = Border.Single(Color.Cyan)
        };
        
        var middleRow = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children =
            [
                StackChild.Fixed(new Rectangle
                {
                    Width = Size.Stretch,
                    Height = Size.Stretch,
                    BackgroundColor = new Color(20, 60, 20),
                    Border = Border.Single(Color.Green)
                }, 20),
                StackChild.Stretch(new Rectangle
                {
                    Width = Size.Stretch,
                    Height = Size.Stretch,
                    BackgroundColor = new Color(15, 15, 15),
                    Border = Border.Rounded(Color.White)
                }),
                StackChild.Fixed(new Rectangle
                {
                    Width = Size.Stretch,
                    Height = Size.Stretch,
                    BackgroundColor = new Color(60, 20, 20),
                    Border = Border.Single(Color.Red)
                }, 25)
            ]
        };
        
        var footer = new Rectangle
        {
            Width = Size.Stretch,
            Height = Size.Stretch,
            BackgroundColor = new Color(40, 40, 40),
            Border = Border.Double(Color.Yellow)
        };
        
        var layout = new Stack
        {
            Orientation = StackOrientation.Vertical,
            Children =
            [
                StackChild.Fixed(header, 5),
                StackChild.Stretch(middleRow),
                StackChild.Fixed(footer, 3)
            ]
        };
        
        // Act
        var output = RenderStackToAnsi(layout, 80, 24);
        
        // Assert - Should have all section colors
        await Assert.That(output).Contains("\e[48;2;30;30;80m"); // Header
        await Assert.That(output).Contains("\e[48;2;20;60;20m"); // Left sidebar
        await Assert.That(output).Contains("\e[48;2;15;15;15m"); // Main content
        await Assert.That(output).Contains("\e[48;2;60;20;20m"); // Right sidebar
        await Assert.That(output).Contains("\e[48;2;40;40;40m"); // Footer
        
        // Should have all border styles
        await Assert.That(output).Contains("┌"); // Single border
        await Assert.That(output).Contains("╭"); // Rounded border
        await Assert.That(output).Contains("═"); // Double border horizontal
        
        // Should have all border colors
        await Assert.That(output).Contains("\e[38;2;0;255;255m"); // Cyan
        await Assert.That(output).Contains("\e[38;2;0;255;0m"); // Green
        await Assert.That(output).Contains("\e[38;2;255;255;255m"); // White
        await Assert.That(output).Contains("\e[38;2;255;0;0m"); // Red
        await Assert.That(output).Contains("\e[38;2;255;255;0m"); // Yellow
    }
    
    #endregion
    
    #region Color Optimization Tests
    
    [Test]
    public async Task Stack_SameBackgroundAcrossChildren_OptimizesColorOutput()
    {
        // Arrange - Two children with same background color
        var stack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children =
            [
                StackChild.Fixed(new Rectangle
                {
                    Width = Size.Stretch,
                    Height = Size.Stretch,
                    BackgroundColor = Color.Black,
                    Border = Border.None
                }, 5),
                StackChild.Fixed(new Rectangle
                {
                    Width = Size.Stretch,
                    Height = Size.Stretch,
                    BackgroundColor = Color.Black,
                    Border = Border.None
                }, 5)
            ]
        };
        
        // Act
        var output = RenderStackToAnsi(stack, 10, 2);
        
        // Assert - Black background color should only be set once
        var blackBgCount = CountOccurrences(output, "\e[48;2;0;0;0m");
        await Assert.That(blackBgCount).IsEqualTo(1); // Optimized
    }
    
    [Test]
    public async Task Stack_AlternatingColors_OutputsEachColorChange()
    {
        // Arrange - Alternating red and blue
        var stack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children =
            [
                StackChild.Fixed(new Rectangle
                {
                    Width = Size.Stretch,
                    Height = Size.Stretch,
                    BackgroundColor = Color.Red,
                    Border = Border.None
                }, 3),
                StackChild.Fixed(new Rectangle
                {
                    Width = Size.Stretch,
                    Height = Size.Stretch,
                    BackgroundColor = Color.Blue,
                    Border = Border.None
                }, 3),
                StackChild.Fixed(new Rectangle
                {
                    Width = Size.Stretch,
                    Height = Size.Stretch,
                    BackgroundColor = Color.Red,
                    Border = Border.None
                }, 3),
                StackChild.Fixed(new Rectangle
                {
                    Width = Size.Stretch,
                    Height = Size.Stretch,
                    BackgroundColor = Color.Blue,
                    Border = Border.None
                }, 3)
            ]
        };
        
        // Act
        var output = RenderStackToAnsi(stack, 12, 2);
        
        // Assert - Colors change multiple times as rendering switches between children
        // With 2 rows and 4 alternating children, red/blue appear multiple times
        var redCount = CountOccurrences(output, "\e[48;2;255;0;0m");
        var blueCount = CountOccurrences(output, "\e[48;2;0;0;255m");
        
        await Assert.That(redCount).IsGreaterThanOrEqualTo(2); // At least 2 transitions
        await Assert.That(blueCount).IsGreaterThanOrEqualTo(2); // At least 2 transitions
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
