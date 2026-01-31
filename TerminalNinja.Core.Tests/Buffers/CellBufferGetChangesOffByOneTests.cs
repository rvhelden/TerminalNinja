namespace TerminalNinja.Core.Tests.Buffers;

/// <summary>
/// Simple test to demonstrate the off-by-one error in GetChanges() coordinates.
/// </summary>
public class CellBufferGetChangesOffByOneTests
{
    [Test]
    public async Task GetChanges_TwoRowsFirstColumn_RevealsOffByOneError()
    {
        // Arrange
        var buffer = new CellBuffer(10, 10);
        buffer.Clear();
        buffer.SwapBuffers();
        
        // Act - Set first column of first two rows: (0,0) and (0,1)
        buffer.SetCell(0, 0, new Cell('A', Color.White, Color.Black));
        buffer.SetCell(0, 1, new Cell('B', Color.White, Color.Black));
        
        // Collect changes
        var changes = new List<(int x, int y, char ch)>();
        foreach (var change in buffer.GetChanges())
        {
            changes.Add((change.X, change.Y, change.Cell.Character));
        }
        
        // Assert
        await Assert.That(changes.Count).IsEqualTo(2);
        
        // First change should be (0, 0) = 'A'
        await Assert.That(changes[0].x).IsEqualTo(0);
        await Assert.That(changes[0].y).IsEqualTo(0);
        await Assert.That(changes[0].ch).IsEqualTo('A');
        
        // Second change should be (0, 1) = 'B', NOT (1, 1)
        await Assert.That(changes[1].x).IsEqualTo(0);
        await Assert.That(changes[1].y).IsEqualTo(1);
        await Assert.That(changes[1].ch).IsEqualTo('B');
    }
    
    [Test]
    public async Task GetChanges_ThreeRowsFirstColumn_ShowsPattern()
    {
        // Arrange
        var buffer = new CellBuffer(10, 10);
        buffer.Clear();
        buffer.SwapBuffers();
        
        // Act - Set first cell of three consecutive rows
        buffer.SetCell(0, 0, new Cell('A', Color.White, Color.Black));
        buffer.SetCell(0, 1, new Cell('B', Color.White, Color.Black));
        buffer.SetCell(0, 2, new Cell('C', Color.White, Color.Black));
        
        // Collect changes
        var changes = new List<(int x, int y, char ch)>();
        foreach (var change in buffer.GetChanges())
        {
            changes.Add((change.X, change.Y, change.Cell.Character));
        }
        
        // Assert - If off-by-one error exists, we might get (0,0), (1,1), (2,2) or skip some
        await Assert.That(changes.Count).IsEqualTo(3);
        
        for (int i = 0; i < 3; i++)
        {
            await Assert.That(changes[i].x).IsEqualTo(0);
            await Assert.That(changes[i].y).IsEqualTo(i);
            await Assert.That(changes[i].ch).IsEqualTo((char)('A' + i));
        }
    }
    
    [Test]
    public async Task GetChanges_FullFirstRowAndSecondRowStart_ShowsSkipping()
    {
        // Arrange
        var buffer = new CellBuffer(10, 10);
        buffer.Clear();
        buffer.SwapBuffers();
        
        // Act - Fill entire first row and first cell of second row
        for (int x = 0; x < 10; x++)
        {
            buffer.SetCell(x, 0, new Cell((char)('0' + x), Color.White, Color.Black));
        }
        buffer.SetCell(0, 1, new Cell('X', Color.White, Color.Black));
        
        // Collect changes
        var changes = new List<(int x, int y, char ch)>();
        foreach (var change in buffer.GetChanges())
        {
            changes.Add((change.X, change.Y, change.Cell.Character));
        }
        
        // This test will reveal if the second row's first cell gets skipped
        // Expected: 10 cells from first row, then (0,1)='X'
        await Assert.That(changes.Count).IsEqualTo(11);
        
        // Verify first row
        for (int x = 0; x < 10; x++)
        {
            await Assert.That(changes[x].x).IsEqualTo(x);
            await Assert.That(changes[x].y).IsEqualTo(0);
        }
        
        // Verify second row first cell - this will fail if off-by-one exists
        await Assert.That(changes[10].x).IsEqualTo(0);
        await Assert.That(changes[10].y).IsEqualTo(1);
        await Assert.That(changes[10].ch).IsEqualTo('X');
    }
}
