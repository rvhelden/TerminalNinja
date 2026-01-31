namespace TerminalNinja.Core.Tests.Buffers;

public class DirtyRectTests
{
    [Test]
    public async Task DirtyRect_Initial_IsNotDirty()
    {
        var dirtyRect = new DirtyRect();
        
        await Assert.That(dirtyRect.IsDirty).IsFalse();
    }
    
    [Test]
    public async Task DirtyRect_Expand_FirstPoint_SetsBounds()
    {
        var dirtyRect = new DirtyRect();
        
        dirtyRect.Expand(10, 20);
        
        await Assert.That(dirtyRect.IsDirty).IsTrue();
        await Assert.That(dirtyRect.MinX).IsEqualTo(10);
        await Assert.That(dirtyRect.MinY).IsEqualTo(20);
        await Assert.That(dirtyRect.MaxX).IsEqualTo(10);
        await Assert.That(dirtyRect.MaxY).IsEqualTo(20);
    }
    
    [Test]
    public async Task DirtyRect_Expand_ExpandsToIncludeNewPoints()
    {
        var dirtyRect = new DirtyRect();
        
        dirtyRect.Expand(10, 20);
        dirtyRect.Expand(5, 15);
        dirtyRect.Expand(15, 25);
        
        await Assert.That(dirtyRect.MinX).IsEqualTo(5);
        await Assert.That(dirtyRect.MinY).IsEqualTo(15);
        await Assert.That(dirtyRect.MaxX).IsEqualTo(15);
        await Assert.That(dirtyRect.MaxY).IsEqualTo(25);
    }
    
    [Test]
    public async Task DirtyRect_Reset_ClearsDirtyFlag()
    {
        var dirtyRect = new DirtyRect();
        dirtyRect.Expand(10, 20);
        
        dirtyRect.Reset();
        
        await Assert.That(dirtyRect.IsDirty).IsFalse();
    }
    
    [Test]
    public async Task DirtyRect_ToRect_CreatesCorrectRectangle()
    {
        var dirtyRect = new DirtyRect();
        dirtyRect.Expand(10, 20);
        dirtyRect.Expand(30, 40);
        
        var rect = dirtyRect.ToRect();
        
        await Assert.That(rect.X).IsEqualTo(10);
        await Assert.That(rect.Y).IsEqualTo(20);
        await Assert.That(rect.Width).IsEqualTo(21);  // MaxX - MinX + 1
        await Assert.That(rect.Height).IsEqualTo(21); // MaxY - MinY + 1
    }
    
    [Test]
    public async Task DirtyRect_Expand_SamePointMultipleTimes_DoesNotChange()
    {
        var dirtyRect = new DirtyRect();
        
        dirtyRect.Expand(10, 20);
        dirtyRect.Expand(10, 20);
        dirtyRect.Expand(10, 20);
        
        await Assert.That(dirtyRect.MinX).IsEqualTo(10);
        await Assert.That(dirtyRect.MinY).IsEqualTo(20);
        await Assert.That(dirtyRect.MaxX).IsEqualTo(10);
        await Assert.That(dirtyRect.MaxY).IsEqualTo(20);
    }
}

public class CellBufferTests
{
    [Test]
    public async Task CellBuffer_Constructor_InitializesSize()
    {
        var buffer = new CellBuffer(80, 24);
        
        await Assert.That(buffer.Width).IsEqualTo(80);
        await Assert.That(buffer.Height).IsEqualTo(24);
    }
    
    [Test]
    public async Task CellBuffer_GetCell_ReturnsSetCell()
    {
        var buffer = new CellBuffer(10, 10);
        var cell = new Cell('A', Color.Red, Color.Blue);
        
        buffer.SetCell(5, 5, cell);
        var retrieved = buffer.GetCell(5, 5);
        
        await Assert.That(retrieved).IsEqualTo(cell);
    }
    
    [Test]
    public async Task CellBuffer_GetCell_OutOfBounds_ReturnsEmpty()
    {
        var buffer = new CellBuffer(10, 10);
        
        await Assert.That(buffer.GetCell(-1, 5)).IsEqualTo(Cell.Empty);
        await Assert.That(buffer.GetCell(5, -1)).IsEqualTo(Cell.Empty);
        await Assert.That(buffer.GetCell(10, 5)).IsEqualTo(Cell.Empty);
        await Assert.That(buffer.GetCell(5, 10)).IsEqualTo(Cell.Empty);
    }
    
    [Test]
    public async Task CellBuffer_SetCell_OutOfBounds_DoesNotThrow()
    {
        var buffer = new CellBuffer(10, 10);
        
        // Should not throw
        buffer.SetCell(-1, 5, Cell.Empty);
        buffer.SetCell(5, -1, Cell.Empty);
        buffer.SetCell(10, 5, Cell.Empty);
        buffer.SetCell(5, 10, Cell.Empty);
        
        // If we got here, no exception was thrown - test passes
        await Task.CompletedTask;
    }
    
    [Test]
    public async Task CellBuffer_Clear_FillsWithEmptyCells()
    {
        var buffer = new CellBuffer(5, 5);
        buffer.SetCell(2, 2, new Cell('X', Color.Red, Color.Blue));
        
        buffer.Clear();
        
        var cell = buffer.GetCell(2, 2);
        await Assert.That(cell).IsEqualTo(Cell.Empty);
    }
    
    [Test]
    public async Task CellBuffer_FillRect_FillsRegion()
    {
        var buffer = new CellBuffer(10, 10);
        var cell = new Cell('X', Color.Red, Color.Blue);
        var rect = new Rect(2, 2, 3, 3);
        
        buffer.FillRect(rect, cell);
        
        // Check corners of the filled region
        await Assert.That(buffer.GetCell(2, 2)).IsEqualTo(cell);
        await Assert.That(buffer.GetCell(4, 4)).IsEqualTo(cell);
        await Assert.That(buffer.GetCell(3, 3)).IsEqualTo(cell);
        
        // Check outside the region
        await Assert.That(buffer.GetCell(1, 2)).IsEqualTo(Cell.Empty);
        await Assert.That(buffer.GetCell(5, 4)).IsEqualTo(Cell.Empty);
    }
    
    [Test]
    public async Task CellBuffer_GetChanges_IncludesModifiedCells()
    {
        var buffer = new CellBuffer(10, 10);
        
        // Swap buffers to clear initial "all changed" state
        buffer.SwapBuffers();
        
        var cellA = new Cell('A', Color.White, Color.Black);
        var cellB = new Cell('B', Color.White, Color.Black);
        
        buffer.SetCell(2, 3, cellA);
        buffer.SetCell(5, 7, cellB);
        
        var changes = buffer.GetChanges();
        var changedCells = new List<(int X, int Y, char Character)>();
        foreach (var change in changes)
        {
            changedCells.Add((change.X, change.Y, change.Cell.Character));
        }
        
        // Should include both modified cells
        await Assert.That(changedCells).Contains((2, 3, 'A'));
        await Assert.That(changedCells).Contains((5, 7, 'B'));
    }
    
    [Test]
    public async Task CellBuffer_SwapBuffers_ClearsChanges()
    {
        var buffer = new CellBuffer(10, 10);
        buffer.SetCell(5, 5, new Cell('A', Color.Red, Color.Blue));
        
        buffer.SwapBuffers();
        
        var changes = buffer.GetChanges();
        var changeCount = 0;
        foreach (var _ in changes)
        {
            changeCount++;
        }
        
        // After swap, no changes should be reported
        await Assert.That(changeCount).IsEqualTo(0);
    }
    
    [Test]
    public async Task CellBuffer_SetSameCellTwice_OnlyMarkedOnce()
    {
        var buffer = new CellBuffer(10, 10);
        var cell = new Cell('A', Color.Red, Color.Blue);
        
        buffer.SetCell(5, 5, cell);
        buffer.SetCell(5, 5, cell); // Same cell, should not create duplicate change
        
        var changes = buffer.GetChanges();
        var changeCount = 0;
        foreach (var change in changes)
        {
            if (change.X == 5 && change.Y == 5)
            {
                changeCount++;
            }
        }
        
        await Assert.That(changeCount).IsEqualTo(1);
    }
    
    [Test]
    public async Task CellBuffer_Resize_ChangesSize()
    {
        var buffer = new CellBuffer(10, 10);
        buffer.SetCell(5, 5, new Cell('A', Color.Red, Color.Blue));
        
        buffer.Resize(20, 30);
        
        await Assert.That(buffer.Width).IsEqualTo(20);
        await Assert.That(buffer.Height).IsEqualTo(30);
        // After resize, content should be empty
        await Assert.That(buffer.GetCell(5, 5)).IsEqualTo(Cell.Empty);
    }
    
    [Test]
    public async Task CellBuffer_SetChar_StoresCell()
    {
        var buffer = new CellBuffer(10, 10);
        
        buffer.SetChar(5, 5, 'X', Color.Red, Color.Blue);
        
        var cell = buffer.GetCell(5, 5);
        await Assert.That(cell.Character).IsEqualTo('X');
        await Assert.That(cell.Foreground).IsEqualTo(Color.Red);
        await Assert.That(cell.Background).IsEqualTo(Color.Blue);
    }
    
    [Test]
    public async Task CellBuffer_IsInBounds_ReturnsTrueForValidCoordinates()
    {
        var buffer = new CellBuffer(10, 10);
        
        await Assert.That(buffer.IsInBounds(0, 0)).IsTrue();
        await Assert.That(buffer.IsInBounds(9, 9)).IsTrue();
        await Assert.That(buffer.IsInBounds(5, 5)).IsTrue();
    }
    
    [Test]
    public async Task CellBuffer_IsInBounds_ReturnsFalseForInvalidCoordinates()
    {
        var buffer = new CellBuffer(10, 10);
        
        await Assert.That(buffer.IsInBounds(-1, 5)).IsFalse();
        await Assert.That(buffer.IsInBounds(5, -1)).IsFalse();
        await Assert.That(buffer.IsInBounds(10, 5)).IsFalse();
        await Assert.That(buffer.IsInBounds(5, 10)).IsFalse();
    }
}
