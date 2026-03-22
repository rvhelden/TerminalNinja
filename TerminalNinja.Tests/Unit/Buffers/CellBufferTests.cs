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
        using var buffer = new CellBuffer(80, 24);
        
        await Assert.That(buffer.Width).IsEqualTo(80);
        await Assert.That(buffer.Height).IsEqualTo(24);
    }
    
    [Test]
    public async Task IsInBounds_ReturnsTrueForValidCoordinates()
    {
        using var buffer = new CellBuffer(10, 10);
        
        await Assert.That(buffer.IsInBounds(0, 0)).IsTrue();
        await Assert.That(buffer.IsInBounds(9, 9)).IsTrue();
        await Assert.That(buffer.IsInBounds(5, 5)).IsTrue();
    }
    
    [Test]
    public async Task IsInBounds_ReturnsFalseForInvalidCoordinates()
    {
        using var buffer = new CellBuffer(10, 10);
        
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
        using var buffer = new CellBuffer(10, 10);
        var cell = new Cell('A', Color.Red, Color.Blue);
        
        buffer.SetCell(5, 5, cell);
        var retrieved = buffer.GetCell(5, 5);
        
        await Assert.That(retrieved).IsEqualTo(cell);
    }
    
    [Test]
    public async Task GetCell_OutOfBounds_ReturnsEmpty()
    {
        using var buffer = new CellBuffer(10, 10);
        
        await Assert.That(buffer.GetCell(-1, 5)).IsEqualTo(Cell.Empty);
        await Assert.That(buffer.GetCell(5, -1)).IsEqualTo(Cell.Empty);
        await Assert.That(buffer.GetCell(10, 5)).IsEqualTo(Cell.Empty);
        await Assert.That(buffer.GetCell(5, 10)).IsEqualTo(Cell.Empty);
    }
    
    [Test]
    public async Task SetCell_OutOfBounds_DoesNotThrow()
    {
        using var buffer = new CellBuffer(10, 10);
        
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
        using var buffer = new CellBuffer(10, 10);
        
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
        using var buffer = new CellBuffer(5, 5);
        buffer.SetCell(2, 2, new Cell('X', Color.Red, Color.Blue));
        
        buffer.Clear();
        
        var cell = buffer.GetCell(2, 2);
        await Assert.That(cell).IsEqualTo(Cell.Empty);
    }
    
    [Test]
    public async Task FillRect_FillsRegion()
    {
        using var buffer = new CellBuffer(10, 10);
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
        using var buffer = new CellBuffer(10, 10);
        
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
        using var buffer = new CellBuffer(10, 10);
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
        using var buffer = new CellBuffer(10, 10);
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
        using var buffer = new CellBuffer(10, 3);
        buffer.Clear();
        buffer.SwapBuffers();
        
        // Act - Fill entire first row (y=0, x=0 to 9)
        for (var x = 0; x < 10; x++)
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
        
        for (var i = 0; i < 10; i++)
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
        using var buffer = new CellBuffer(10, 10);
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
        using var buffer = new CellBuffer(10, 10);
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
        using var buffer = new CellBuffer(10, 5);
        buffer.Clear();
        buffer.SwapBuffers();
        
        // Act - Set first cell of each row
        for (var y = 0; y < 5; y++)
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
        
        for (var y = 0; y < 5; y++)
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
        using var buffer = new CellBuffer(5, 5);
        buffer.Clear();
        buffer.SwapBuffers();
        
        // Act - Fill entire buffer with known pattern
        for (var y = 0; y < 5; y++)
        {
            for (var x = 0; x < 5; x++)
            {
                var ch = (char)('A' + (y * 5 + x));
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
        var index = 0;
        for (var y = 0; y < 5; y++)
        {
            for (var x = 0; x < 5; x++)
            {
                await Assert.That(changes[index].X).IsEqualTo(x);
                await Assert.That(changes[index].Y).IsEqualTo(y);
                
                var expectedChar = (char)('A' + (y * 5 + x));
                await Assert.That(changes[index].Cell.Character).IsEqualTo(expectedChar);
                
                index++;
            }
        }
    }
    
    [Test]
    public async Task GetChanges_BottomRightCorner_HasCorrectCoordinates()
    {
        // Arrange
        using var buffer = new CellBuffer(10, 10);
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
        using var buffer = new CellBuffer(10, 10);
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
        
        for (var i = 0; i < sortedExpected.Count; i++)
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
        using var buffer = new CellBuffer(10, 10);
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
        using var buffer = new CellBuffer(10, 10);
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
        
        for (var i = 0; i < 3; i++)
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
        using var buffer = new CellBuffer(10, 10);
        buffer.Clear();
        buffer.SwapBuffers();
        
        // Act - Fill entire first row and first cell of second row
        for (var x = 0; x < 10; x++)
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
        for (var x = 0; x < 10; x++)
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
        using var buffer = new CellBuffer(10, 10);
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
        // After resize, overlapping content should be preserved
        await Assert.That(buffer.GetCell(5, 5)).IsEqualTo(new Cell('A', Color.Red, Color.Blue));
        // New cells should be empty
        await Assert.That(buffer.GetCell(15, 25)).IsEqualTo(Cell.Empty);
        
        buffer.Dispose();
    }
    
    [Test]
    public async Task Resize_Shrink_PreservesOverlappingContent()
    {
        var buffer = new CellBuffer(20, 20);
        buffer.SetCell(3, 3, new Cell('X', Color.Green, Color.Black));
        buffer.SetCell(15, 15, new Cell('Y', Color.Red, Color.Black));
        
        buffer.Resize(10, 10);
        
        await Assert.That(buffer.Width).IsEqualTo(10);
        await Assert.That(buffer.Height).IsEqualTo(10);
        // Cell within new bounds is preserved
        await Assert.That(buffer.GetCell(3, 3)).IsEqualTo(new Cell('X', Color.Green, Color.Black));
        // Cell outside new bounds is inaccessible (returns Cell.Empty from bounds check)
        await Assert.That(buffer.GetCell(15, 15)).IsEqualTo(Cell.Empty);
        
        buffer.Dispose();
    }
    
    [Test]
    public async Task Resize_WidthGrew_PreservesExistingRows()
    {
        var buffer = new CellBuffer(5, 3);
        // Fill row 0: "ABCDE", row 1: "FGHIJ", row 2: "KLMNO"
        for (var i = 0; i < 5; i++)
        {
            buffer.SetCell(i, 0, new Cell((char)('A' + i), Color.White, Color.Black));
            buffer.SetCell(i, 1, new Cell((char)('F' + i), Color.White, Color.Black));
            buffer.SetCell(i, 2, new Cell((char)('K' + i), Color.White, Color.Black));
        }
        
        buffer.Resize(10, 3); // Width grew, height same
        
        // Original content preserved
        await Assert.That(buffer.GetCell(0, 0).Character).IsEqualTo('A');
        await Assert.That(buffer.GetCell(4, 0).Character).IsEqualTo('E');
        await Assert.That(buffer.GetCell(0, 1).Character).IsEqualTo('F');
        await Assert.That(buffer.GetCell(4, 2).Character).IsEqualTo('O');
        // New columns are Cell.Empty
        await Assert.That(buffer.GetCell(5, 0)).IsEqualTo(Cell.Empty);
        await Assert.That(buffer.GetCell(9, 2)).IsEqualTo(Cell.Empty);
        
        buffer.Dispose();
    }
    
    [Test]
    public async Task Resize_WidthShrunk_TruncatesRows()
    {
        var buffer = new CellBuffer(10, 3);
        for (var i = 0; i < 10; i++)
        {
            buffer.SetCell(i, 0, new Cell((char)('A' + i), Color.White, Color.Black));
            buffer.SetCell(i, 1, new Cell((char)('K' + i), Color.White, Color.Black));
        }
        
        buffer.Resize(5, 3); // Width shrunk
        
        // First 5 columns preserved
        await Assert.That(buffer.GetCell(0, 0).Character).IsEqualTo('A');
        await Assert.That(buffer.GetCell(4, 0).Character).IsEqualTo('E');
        await Assert.That(buffer.GetCell(0, 1).Character).IsEqualTo('K');
        await Assert.That(buffer.GetCell(4, 1).Character).IsEqualTo('O');
        // Columns beyond new width are out of bounds
        await Assert.That(buffer.GetCell(5, 0)).IsEqualTo(Cell.Empty);
        
        buffer.Dispose();
    }
    
    [Test]
    public async Task Resize_WithinCapacity_DoesNotReallocate()
    {
        // Start with 100x100 = 10000, capacity rounds to 16384
        var buffer = new CellBuffer(100, 100);
        var initialCapacity = buffer.Capacity;
        
        buffer.SetCell(5, 5, new Cell('Z', Color.Cyan, Color.Black));
        
        // Shrink to 50x50 = 2500, well within 16384
        buffer.Resize(50, 50);
        
        await Assert.That(buffer.Capacity).IsEqualTo(initialCapacity);
        await Assert.That(buffer.GetCell(5, 5)).IsEqualTo(new Cell('Z', Color.Cyan, Color.Black));
        
        buffer.Dispose();
    }
    
    [Test]
    public async Task Resize_ExceedsCapacity_AllocatesLarger()
    {
        var buffer = new CellBuffer(10, 10); // 100 cells, capacity 128
        var initialCapacity = buffer.Capacity;
        
        buffer.SetCell(3, 3, new Cell('W', Color.Yellow, Color.Black));
        
        // Grow to 200x200 = 40000, well beyond 128
        buffer.Resize(200, 200);
        
        await Assert.That(buffer.Capacity).IsGreaterThan(initialCapacity);
        // Content in overlapping region preserved
        await Assert.That(buffer.GetCell(3, 3)).IsEqualTo(new Cell('W', Color.Yellow, Color.Black));
        
        buffer.Dispose();
    }
    
    [Test]
    public async Task Capacity_IsPowerOf2()
    {
        var buffer = new CellBuffer(17, 13); // 221 cells
        // Capacity should be next power of 2 >= 221 → 256
        await Assert.That(buffer.Capacity).IsEqualTo(256);
        
        buffer.Dispose();
    }
    
    [Test]
    public async Task Dispose_IsIdempotent()
    {
        var buffer = new CellBuffer(10, 10);
        buffer.SetCell(0, 0, new Cell('A', Color.White, Color.Black));
        
        buffer.Dispose();
        buffer.Dispose(); // Should not throw
        
        await Assert.That(buffer.Width).IsEqualTo(0);
        await Assert.That(buffer.Height).IsEqualTo(0);
    }
    
    [Test]
    public async Task GetRow_ReturnsCorrectSpan()
    {
        var buffer = new CellBuffer(5, 3);
        buffer.SetCell(0, 1, new Cell('A', Color.White, Color.Black));
        buffer.SetCell(4, 1, new Cell('Z', Color.White, Color.Black));
        
        var rowLength = buffer.GetRow(1).Length;
        var firstChar = buffer.GetRow(1)[0].Character;
        var lastChar = buffer.GetRow(1)[4].Character;
        
        await Assert.That(rowLength).IsEqualTo(5);
        await Assert.That(firstChar).IsEqualTo('A');
        await Assert.That(lastChar).IsEqualTo('Z');
        
        buffer.Dispose();
    }
    
    [Test]
    public async Task Indexer_ReturnsRef()
    {
        var buffer = new CellBuffer(5, 3);
        buffer.SetCell(2, 1, new Cell('Q', Color.Red, Color.Black));
        
        var character = buffer[2, 1].Character;
        var foreground = buffer[2, 1].Foreground;
        
        await Assert.That(character).IsEqualTo('Q');
        await Assert.That(foreground).IsEqualTo(Color.Red);
        
        buffer.Dispose();
    }
    
    [Test]
    public async Task Resize_HeightGrew_NewRowsAreEmpty()
    {
        var buffer = new CellBuffer(10, 5);
        buffer.SetCell(0, 0, new Cell('A', Color.White, Color.Black));
        buffer.SetCell(9, 4, new Cell('Z', Color.White, Color.Black));
        
        buffer.Resize(10, 10); // Same width, more rows
        
        await Assert.That(buffer.GetCell(0, 0).Character).IsEqualTo('A');
        await Assert.That(buffer.GetCell(9, 4).Character).IsEqualTo('Z');
        // New rows are empty
        await Assert.That(buffer.GetCell(0, 5)).IsEqualTo(Cell.Empty);
        await Assert.That(buffer.GetCell(9, 9)).IsEqualTo(Cell.Empty);
        
        buffer.Dispose();
    }
    
    [Test]
    public async Task Resize_PreviousBufferReset_ForcesFullRepaint()
    {
        // After resize, _previous must be Cell.Empty so the diff engine reports
        // ALL non-empty cells as changes (the terminal screen is cleared on resize).
        var buffer = new CellBuffer(10, 5);
        var testCell = new Cell('X', Color.Red, Color.Blue);
        buffer.SetCell(3, 2, testCell);
        
        // Simulate a render cycle: swap buffers so _previous has the 'X' cell
        buffer.SwapBuffers();
        // Now _previous has 'X' at (3,2). Set the same cell in _current.
        buffer.SetCell(3, 2, testCell);
        
        // Before resize, changes should be empty (current == previous at that cell)
        var changesBefore = new List<CellChange>();
        foreach (var c in buffer.GetChanges())
        {
            changesBefore.Add(c);
        }

        await Assert.That(changesBefore.Count).IsEqualTo(0);
        
        // Resize to different dimensions — this should reset _previous to Cell.Empty
        buffer.Resize(12, 6);
        
        // _current was preserved by resize, so (3,2) still has 'X'
        // _previous is now Cell.Empty, so the diff should report (3,2) as changed
        var changesAfter = new List<CellChange>();
        foreach (var c in buffer.GetChanges())
        {
            changesAfter.Add(c);
        }

        // The 'X' cell at (3,2) should appear in the diff because _previous is empty
        var xChange = changesAfter.FirstOrDefault(ch => ch.X == 3 && ch.Y == 2);
        await Assert.That(xChange.Cell).IsEqualTo(testCell);
        
        buffer.Dispose();
    }
    
    #endregion
}
