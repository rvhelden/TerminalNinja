using System;
using TerminalNinja.Core.Buffers;
using TerminalNinja.Core.Elements;
using TerminalNinja.Core.Primitives;
using TerminalNinja.Core.Styling;

namespace TerminalNinja.Core.Tests.Debug;

/// <summary>
/// Tests to verify exact cell positions and border rendering.
/// </summary>
public class BorderAlignmentTests
{
    [Test]
    public async Task SimpleHorizontalStack_WithBorders_ShouldNotOverlap()
    {
        // Arrange - Create a very simple case: 3 boxes in a row
        var buffer = new CellBuffer(60, 10);
        var parentBounds = new Rect(0, 0, 60, 10);
        
        var left = new Rectangle
        {
            BackgroundColor = Color.Red,
            ForegroundColor = Color.Red,
            Border = Border.Single(Color.Red)
        };
        
        var middle = new Rectangle
        {
            BackgroundColor = Color.Green,
            ForegroundColor = Color.Green,
            Border = Border.Single(Color.Green)
        };
        
        var right = new Rectangle
        {
            BackgroundColor = Color.Blue,
            ForegroundColor = Color.Blue,
            Border = Border.Single(Color.Blue)
        };
        
        var stack = new Stack
        {
            Orientation = StackOrientation.Horizontal,
            Children =
            [
                StackChild.Fixed(left, 20),
                StackChild.Fixed(middle, 20),
                StackChild.Fixed(right, 20)
            ]
        };
        
        // Act
        stack.Render(buffer, parentBounds);
        
        // Debug: Print what was actually rendered
        System.Console.WriteLine("\nRendered buffer (first 3 rows):");
        for (int y = 0; y < 3; y++)
        {
            System.Console.Write($"Row {y}: ");
            for (int x = 0; x < 60; x++)
            {
                var cell = buffer.GetCell(x, y);
                if (cell.Background == Color.Red)
                    System.Console.Write("R");
                else if (cell.Background == Color.Green)
                    System.Console.Write("G");
                else if (cell.Background == Color.Blue)
                    System.Console.Write("B");
                else
                    System.Console.Write(".");
            }
            System.Console.WriteLine();
        }
        
        // Assert - Left box should be columns 0-19
        await Assert.That(buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(buffer.GetCell(19, 0).Background).IsEqualTo(Color.Red);
        
        // Assert - Middle box should be columns 20-39
        await Assert.That(buffer.GetCell(20, 0).Background).IsEqualTo(Color.Green);
        await Assert.That(buffer.GetCell(39, 0).Background).IsEqualTo(Color.Green);
        
        // Assert - Right box should be columns 40-59
        await Assert.That(buffer.GetCell(40, 0).Background).IsEqualTo(Color.Blue);
        await Assert.That(buffer.GetCell(59, 0).Background).IsEqualTo(Color.Blue);
        
        // Assert - Check exact boundaries
        await Assert.That(buffer.GetCell(19, 5).Background).IsEqualTo(Color.Red);
        await Assert.That(buffer.GetCell(20, 5).Background).IsEqualTo(Color.Green);
        await Assert.That(buffer.GetCell(39, 5).Background).IsEqualTo(Color.Green);
        await Assert.That(buffer.GetCell(40, 5).Background).IsEqualTo(Color.Blue);
    }
    
    [Test]
    public async Task SimpleVerticalStack_WithBorders_ShouldNotOverlap()
    {
        // Arrange - Create a very simple case: 3 boxes stacked vertically
        var buffer = new CellBuffer(40, 30);
        var parentBounds = new Rect(0, 0, 40, 30);
        
        var top = new Rectangle
        {
            BackgroundColor = Color.Red,
            ForegroundColor = Color.Red,
            Border = Border.Single(Color.Red)
        };
        
        var middle = new Rectangle
        {
            BackgroundColor = Color.Green,
            ForegroundColor = Color.Green,
            Border = Border.Single(Color.Green)
        };
        
        var bottom = new Rectangle
        {
            BackgroundColor = Color.Blue,
            ForegroundColor = Color.Blue,
            Border = Border.Single(Color.Blue)
        };
        
        var stack = new Stack
        {
            Orientation = StackOrientation.Vertical,
            Children =
            [
                StackChild.Fixed(top, 10),
                StackChild.Fixed(middle, 10),
                StackChild.Fixed(bottom, 10)
            ]
        };
        
        // Act
        stack.Render(buffer, parentBounds);
        
        // Debug: Print what was actually rendered (column 0)
        System.Console.WriteLine("\nRendered buffer (column 0, all rows):");
        for (int y = 0; y < 30; y++)
        {
            var cell = buffer.GetCell(0, y);
            if (cell.Background == Color.Red)
                System.Console.WriteLine($"Row {y:D2}: RED   '{cell.Character}'");
            else if (cell.Background == Color.Green)
                System.Console.WriteLine($"Row {y:D2}: GREEN '{cell.Character}'");
            else if (cell.Background == Color.Blue)
                System.Console.WriteLine($"Row {y:D2}: BLUE  '{cell.Character}'");
            else
                System.Console.WriteLine($"Row {y:D2}: EMPTY '{cell.Character}'");
        }
        
        // Assert - Top box should be rows 0-9
        await Assert.That(buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(buffer.GetCell(0, 9).Background).IsEqualTo(Color.Red);
        
        // Assert - Middle box should be rows 10-19
        await Assert.That(buffer.GetCell(0, 10).Background).IsEqualTo(Color.Green);
        await Assert.That(buffer.GetCell(0, 19).Background).IsEqualTo(Color.Green);
        
        // Assert - Bottom box should be rows 20-29
        await Assert.That(buffer.GetCell(0, 20).Background).IsEqualTo(Color.Blue);
        await Assert.That(buffer.GetCell(0, 29).Background).IsEqualTo(Color.Blue);
        
        // Assert - Check exact boundaries
        await Assert.That(buffer.GetCell(20, 9).Background).IsEqualTo(Color.Red);
        await Assert.That(buffer.GetCell(20, 10).Background).IsEqualTo(Color.Green);
        await Assert.That(buffer.GetCell(20, 19).Background).IsEqualTo(Color.Green);
        await Assert.That(buffer.GetCell(20, 20).Background).IsEqualTo(Color.Blue);
    }
}
