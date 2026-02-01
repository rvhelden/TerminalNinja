namespace TerminalNinja.Tests.Unit.Buffers;

/// <summary>
/// Comprehensive tests for CellBuffer, including basic operations,
/// coordinate accuracy verification, and off-by-one bug detection.
/// </summary>
public class CellBufferTests
{
    #region Constructor and Basic Operations
    
    [Test]
    public async Task Constructor_InitializesSize()
    {
        var buffer = new CellBuffer(80, 24);
        
        await Assert.That(buffer.Width).IsEqualTo(80);
        await Assert.That(buffer.Height).IsEqualTo(24);
    }
    
    [Test]
    public async Task IsInBounds_ReturnsTrueForValidCoordinates()
    {
        var buffer = new CellBuffer(10, 10);
        
        await Assert.That(buffer.IsInBounds(0, 0)).IsTrue();
        await Assert.That(buffer.IsInBounds(9, 9)).IsTrue();
        await Assert.That(buffer.IsInBounds(5, 5)).IsTrue();
    }
    
    [Test]
    public async Task IsInBounds_ReturnsFalseForInvalidCoordinates()
    {
        var buffer = new CellBuffer(10, 10);
        
        await Assert.That(buffer.IsInBounds(-1, 5)).IsFalse();
        await Assert.That(buffer.IsInBounds(5, -1)).IsFalse();
        await Assert.That(buffer.IsInBounds(10, 5)).IsFalse();
        await Assert.That(buffer.IsInBounds(5, 10)).IsFalse();
    }
    
    #endregion
    
    #region SetCell and GetCell
    
    [Test]
    public async Task GetCell_ReturnsSetCell()
    {
        var buffer = new CellBuffer(10, 10);
        var cell = new Cell('A', Color.Red, Color.Blue);
        
        buffer.SetCell(5, 5, cell);
        var retrieved = buffer.GetCell(5, 5);
        
        await Assert.That(retrieved).IsEqualTo(cell);
    }
    
    [Test]
    public async Task GetCell_OutOfBounds_ReturnsEmpty()
    {
        var buffer = new CellBuffer(10, 10);
        
        await Assert.That(buffer.GetCell(-1, 5)).IsEqualTo(Cell.Empty);
        await Assert.That(buffer.GetCell(5, -1)).IsEqualTo(Cell.Empty);
        await Assert.That(buffer.GetCell(10, 5)).IsEqualTo(Cell.Empty);
        await Assert.That(buffer.GetCell(5, 10)).IsEqualTo(Cell.Empty);
    }
    
    [Test]
    public async Task SetCell_OutOfBounds_DoesNotThrow()
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
    public async Task SetChar_StoresCell()
    {
        var buffer = new CellBuffer(10, 10);
        
        buffer.SetChar(5, 5, 'X', Color.Red, Color.Blue);
        
        var cell = buffer.GetCell(5, 5);
        await Assert.That(cell.Character).IsEqualTo('X');
        await Assert.That(cell.Foreground).IsEqualTo(Color.Red);
        await Assert.That(cell.Background).IsEqualTo(Color.Blue);
    }
    
    #endregion
    
    #region Clear and FillRect
    
    [Test]
    public async Task Clear_FillsWithEmptyCells()
    {
        var buffer = new CellBuffer(5, 5);
        buffer.SetCell(2, 2, new Cell('X', Color.Red, Color.Blue));
        
        buffer.Clear();
        
        var cell = buffer.GetCell(2, 2);
        await Assert.That(cell).IsEqualTo(Cell.Empty);
    }
    
    [Test]
    public async Task FillRect_FillsRegion()
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
    
    #endregion
    
    #region GetChanges - Basic Enumeration
    
    [Test]
    public async Task GetChanges_IncludesModifiedCells()
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
    public async Task SetSameCellTwice_OnlyMarkedOnce()
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
    
    #endregion
    
    #region GetChanges - Coordinate Accuracy
    
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
    
    #endregion
    
    #region GetChanges - Off-By-One Bug Verification
    
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
    
    #endregion
    
    #region SwapBuffers and Resize
    
    [Test]
    public async Task SwapBuffers_ClearsChanges()
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
    public async Task Resize_ChangesSize()
    {
        var buffer = new CellBuffer(10, 10);
        buffer.SetCell(5, 5, new Cell('A', Color.Red, Color.Blue));
        
        buffer.Resize(20, 30);
        
        await Assert.That(buffer.Width).IsEqualTo(20);
        await Assert.That(buffer.Height).IsEqualTo(30);
        // After resize, content should be empty
        await Assert.That(buffer.GetCell(5, 5)).IsEqualTo(Cell.Empty);
    }
    
    #endregion
}
