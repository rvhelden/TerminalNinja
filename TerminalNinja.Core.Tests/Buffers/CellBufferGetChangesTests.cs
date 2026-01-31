namespace TerminalNinja.Core.Tests.Buffers;

/// <summary>
/// Tests that verify CellBuffer.GetChanges() returns correct coordinates,
/// particularly checking for off-by-one errors in coordinate indexing.
/// </summary>
public class CellBufferGetChangesTests
{
    [Test]
    public async Task GetChanges_FirstRow_StartsAtZeroZero()
    {
        // Arrange
        var buffer = new CellBuffer(10, 10);
        buffer.Clear();
        buffer.SwapBuffers();
        
        // Act - Set first cell
        buffer.SetCell(0, 0, new Cell('A', Color.White, Color.Black));
        
        var changes = new List<CellChange>();
        foreach (var change in buffer.GetChanges())
        {
            changes.Add(change);
        }
        
        // Assert
        await Assert.That(changes.Count).IsEqualTo(1);
        await Assert.That(changes[0].X).IsEqualTo(0);
        await Assert.That(changes[0].Y).IsEqualTo(0);
    }
    
    [Test]
    public async Task GetChanges_EntireFirstRow_HasCorrectCoordinates()
    {
        // Arrange
        var buffer = new CellBuffer(10, 3);
        buffer.Clear();
        buffer.SwapBuffers();
        
        // Act - Fill entire first row (y=0, x=0 to 9)
        for (int x = 0; x < 10; x++)
        {
            buffer.SetCell(x, 0, new Cell((char)('A' + x), Color.White, Color.Black));
        }
        
        var changes = new List<CellChange>();
        foreach (var change in buffer.GetChanges())
        {
            changes.Add(change);
        }
        
        // Assert - Should have 10 changes, all with y=0
        await Assert.That(changes.Count).IsEqualTo(10);
        
        for (int i = 0; i < 10; i++)
        {
            await Assert.That(changes[i].X).IsEqualTo(i);
            await Assert.That(changes[i].Y).IsEqualTo(0);
            await Assert.That(changes[i].Cell.Character).IsEqualTo((char)('A' + i));
        }
    }
    
    [Test]
    public async Task GetChanges_SecondRowFirstCell_IsZeroOne()
    {
        // Arrange
        var buffer = new CellBuffer(10, 10);
        buffer.Clear();
        buffer.SwapBuffers();
        
        // Act - Set first cell of second row
        buffer.SetCell(0, 1, new Cell('B', Color.White, Color.Black));
        
        var changes = new List<CellChange>();
        foreach (var change in buffer.GetChanges())
        {
            changes.Add(change);
        }
        
        // Assert - Should be at (0, 1) NOT (1, 1)
        await Assert.That(changes.Count).IsEqualTo(1);
        await Assert.That(changes[0].X).IsEqualTo(0);
        await Assert.That(changes[0].Y).IsEqualTo(1);
    }
    
    [Test]
    public async Task GetChanges_FirstRowLastCell_ThenSecondRowFirstCell_CorrectOrder()
    {
        // Arrange
        var buffer = new CellBuffer(10, 10);
        buffer.Clear();
        buffer.SwapBuffers();
        
        // Act - Set last cell of first row (9, 0) and first cell of second row (0, 1)
        buffer.SetCell(9, 0, new Cell('Z', Color.White, Color.Black));
        buffer.SetCell(0, 1, new Cell('A', Color.White, Color.Black));
        
        var changes = new List<CellChange>();
        foreach (var change in buffer.GetChanges())
        {
            changes.Add(change);
        }
        
        // Assert - Should have exactly 2 changes in correct order
        await Assert.That(changes.Count).IsEqualTo(2);
        
        // First change: last cell of first row (9, 0)
        await Assert.That(changes[0].X).IsEqualTo(9);
        await Assert.That(changes[0].Y).IsEqualTo(0);
        await Assert.That(changes[0].Cell.Character).IsEqualTo('Z');
        
        // Second change: first cell of second row (0, 1) - NOT (1, 1)
        await Assert.That(changes[1].X).IsEqualTo(0);
        await Assert.That(changes[1].Y).IsEqualTo(1);
        await Assert.That(changes[1].Cell.Character).IsEqualTo('A');
    }
    
    [Test]
    public async Task GetChanges_MultipleRows_AllStartAtXZero()
    {
        // Arrange
        var buffer = new CellBuffer(10, 5);
        buffer.Clear();
        buffer.SwapBuffers();
        
        // Act - Set first cell of each row
        for (int y = 0; y < 5; y++)
        {
            buffer.SetCell(0, y, new Cell((char)('A' + y), Color.White, Color.Black));
        }
        
        var changes = new List<CellChange>();
        foreach (var change in buffer.GetChanges())
        {
            changes.Add(change);
        }
        
        // Assert - All changes should have x=0
        await Assert.That(changes.Count).IsEqualTo(5);
        
        for (int y = 0; y < 5; y++)
        {
            await Assert.That(changes[y].X).IsEqualTo(0);
            await Assert.That(changes[y].Y).IsEqualTo(y);
            await Assert.That(changes[y].Cell.Character).IsEqualTo((char)('A' + y));
        }
    }
    
    [Test]
    public async Task GetChanges_FillEntireBuffer_CoordinatesMatchSetCellCalls()
    {
        // Arrange
        var buffer = new CellBuffer(5, 5);
        buffer.Clear();
        buffer.SwapBuffers();
        
        // Act - Fill entire buffer with known pattern
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                char ch = (char)('A' + (y * 5 + x));
                buffer.SetCell(x, y, new Cell(ch, Color.White, Color.Black));
            }
        }
        
        var changes = new List<CellChange>();
        foreach (var change in buffer.GetChanges())
        {
            changes.Add(change);
        }
        
        // Assert - Should have 25 changes (5x5)
        await Assert.That(changes.Count).IsEqualTo(25);
        
        // Verify each coordinate matches what we set
        int index = 0;
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                await Assert.That(changes[index].X).IsEqualTo(x);
                await Assert.That(changes[index].Y).IsEqualTo(y);
                
                char expectedChar = (char)('A' + (y * 5 + x));
                await Assert.That(changes[index].Cell.Character).IsEqualTo(expectedChar);
                
                index++;
            }
        }
    }
    
    [Test]
    public async Task GetChanges_BottomRightCorner_HasCorrectCoordinates()
    {
        // Arrange
        var buffer = new CellBuffer(10, 10);
        buffer.Clear();
        buffer.SwapBuffers();
        
        // Act - Set bottom-right corner (9, 9)
        buffer.SetCell(9, 9, new Cell('X', Color.White, Color.Black));
        
        var changes = new List<CellChange>();
        foreach (var change in buffer.GetChanges())
        {
            changes.Add(change);
        }
        
        // Assert
        await Assert.That(changes.Count).IsEqualTo(1);
        await Assert.That(changes[0].X).IsEqualTo(9);
        await Assert.That(changes[0].Y).IsEqualTo(9);
    }
    
    [Test]
    public async Task GetChanges_SparsePattern_AllCoordinatesCorrect()
    {
        // Arrange
        var buffer = new CellBuffer(10, 10);
        buffer.Clear();
        buffer.SwapBuffers();
        
        // Act - Set specific cells in sparse pattern
        var expectedCells = new[]
        {
            (0, 0, 'A'),
            (5, 0, 'B'),
            (9, 0, 'C'),
            (0, 5, 'D'),
            (5, 5, 'E'),
            (9, 9, 'F')
        };
        
        foreach (var (x, y, ch) in expectedCells)
        {
            buffer.SetCell(x, y, new Cell(ch, Color.White, Color.Black));
        }
        
        var changes = new List<CellChange>();
        foreach (var change in buffer.GetChanges())
        {
            changes.Add(change);
        }
        
        // Assert - Should have exactly 6 changes
        await Assert.That(changes.Count).IsEqualTo(6);
        
        // Verify each change matches expected coordinates
        // Changes are returned in row-major order (y first, then x)
        var sortedExpected = expectedCells.OrderBy(c => c.Item2).ThenBy(c => c.Item1).ToList();
        
        for (int i = 0; i < sortedExpected.Count; i++)
        {
            await Assert.That(changes[i].X).IsEqualTo(sortedExpected[i].Item1);
            await Assert.That(changes[i].Y).IsEqualTo(sortedExpected[i].Item2);
            await Assert.That(changes[i].Cell.Character).IsEqualTo(sortedExpected[i].Item3);
        }
    }
}
