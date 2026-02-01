using TerminalNinja.Ansi;
using TerminalNinja.Controls;
using TerminalNinja.Styling;

namespace TerminalNinja.Tests.Integration;

/// <summary>
/// Tests that verify the optimization where Clear() followed by Present()
/// outputs nothing when the buffer is already in a clear state.
/// </summary>
public class ClearPresentOptimizationTests
{
    [Test]
    public async Task ClearThenPresent_WhenAlreadyClear_OutputsNothing()
    {
        // Arrange - Create buffer and clear it
        var buffer = new CellBuffer(10, 10);
        buffer.Clear();
        
        // First present to establish baseline (empty screen)
        using var stream1 = new MemoryStream();
        using var writer1 = new AnsiWriter(stream1);
        foreach (var change in buffer.GetChanges())
        {
            writer1.WriteCell(change.X, change.Y, change.Cell);
        }
        writer1.Flush();
        buffer.SwapBuffers();
        
        // Act - Clear again and present
        buffer.Clear();
        using var stream2 = new MemoryStream();
        using var writer2 = new AnsiWriter(stream2);
        
        int cellsWritten = 0;
        foreach (var change in buffer.GetChanges())
        {
            writer2.WriteCell(change.X, change.Y, change.Cell);
            cellsWritten++;
        }
        writer2.Flush();
        
        // Assert - No cells should be written (buffer already clear)
        await Assert.That(cellsWritten).IsEqualTo(0);
        await Assert.That(stream2.Length).IsEqualTo(0);
    }
    
    [Test]
    public async Task RenderClearPresent_ThenClearPresent_SecondClearOutputsNothing()
    {
        // Arrange - Render something, present it, then clear and present
        var buffer = new CellBuffer(5, 5);
        var rect = new Rectangle
        {
            ForegroundColor = new Color(255, 0, 0)
        };
        rect.Render(buffer, new Rect(0, 0, 5, 5));
        
        // First present (renders the rectangle)
        using var stream1 = new MemoryStream();
        using var writer1 = new AnsiWriter(stream1);
        foreach (var change in buffer.GetChanges())
        {
            writer1.WriteCell(change.X, change.Y, change.Cell);
        }
        writer1.Flush();
        buffer.SwapBuffers();
        
        // First clear and present (clears the screen)
        buffer.Clear();
        using var stream2 = new MemoryStream();
        using var writer2 = new AnsiWriter(stream2);
        int firstClearCells = 0;
        foreach (var change in buffer.GetChanges())
        {
            writer2.WriteCell(change.X, change.Y, change.Cell);
            firstClearCells++;
        }
        writer2.Flush();
        buffer.SwapBuffers();
        
        // Act - Second clear and present (should output nothing)
        buffer.Clear();
        using var stream3 = new MemoryStream();
        using var writer3 = new AnsiWriter(stream3);
        int secondClearCells = 0;
        foreach (var change in buffer.GetChanges())
        {
            writer3.WriteCell(change.X, change.Y, change.Cell);
            secondClearCells++;
        }
        writer3.Flush();
        
        // Assert - First clear outputs cells (to clear visible content)
        await Assert.That(firstClearCells).IsGreaterThan(0);
        await Assert.That(stream2.Length).IsGreaterThan(0);
        
        // Assert - Second clear outputs nothing (already clear)
        await Assert.That(secondClearCells).IsEqualTo(0);
        await Assert.That(stream3.Length).IsEqualTo(0);
    }
    
    [Test]
    public async Task MultipleClearCalls_OnlyFirstPresentOutputsChanges()
    {
        // Arrange - Create buffer and render content
        var buffer = new CellBuffer(3, 3);
        var rect = new Rectangle
        {
            ForegroundColor = new Color(0, 255, 0)
        };
        rect.Render(buffer, new Rect(0, 0, 3, 3));
        
        // Present the rendered content
        using var stream1 = new MemoryStream();
        using var writer1 = new AnsiWriter(stream1);
        foreach (var change in buffer.GetChanges())
        {
            writer1.WriteCell(change.X, change.Y, change.Cell);
        }
        writer1.Flush();
        buffer.SwapBuffers();
        
        // Act - Call Clear() multiple times in succession
        buffer.Clear();
        using var streamClear1 = new MemoryStream();
        using var writerClear1 = new AnsiWriter(streamClear1);
        int clear1Cells = 0;
        foreach (var change in buffer.GetChanges())
        {
            writerClear1.WriteCell(change.X, change.Y, change.Cell);
            clear1Cells++;
        }
        writerClear1.Flush();
        buffer.SwapBuffers();
        
        buffer.Clear();
        using var streamClear2 = new MemoryStream();
        using var writerClear2 = new AnsiWriter(streamClear2);
        int clear2Cells = 0;
        foreach (var change in buffer.GetChanges())
        {
            writerClear2.WriteCell(change.X, change.Y, change.Cell);
            clear2Cells++;
        }
        writerClear2.Flush();
        buffer.SwapBuffers();
        
        buffer.Clear();
        using var streamClear3 = new MemoryStream();
        using var writerClear3 = new AnsiWriter(streamClear3);
        int clear3Cells = 0;
        foreach (var change in buffer.GetChanges())
        {
            writerClear3.WriteCell(change.X, change.Y, change.Cell);
            clear3Cells++;
        }
        writerClear3.Flush();
        
        // Assert - First clear outputs cells (to clear rendered content)
        await Assert.That(clear1Cells).IsGreaterThan(0);
        await Assert.That(streamClear1.Length).IsGreaterThan(0);
        
        // Assert - Subsequent clears output nothing
        await Assert.That(clear2Cells).IsEqualTo(0);
        await Assert.That(streamClear2.Length).IsEqualTo(0);
        await Assert.That(clear3Cells).IsEqualTo(0);
        await Assert.That(streamClear3.Length).IsEqualTo(0);
    }
    
    [Test]
    public async Task ClearPresentCycle_WithIntermediateRender_OutputsCorrectly()
    {
        // Arrange - Create buffer
        var buffer = new CellBuffer(4, 4);
        
        // Initial clear and present
        buffer.Clear();
        using var stream1 = new MemoryStream();
        using var writer1 = new AnsiWriter(stream1);
        foreach (var change in buffer.GetChanges())
        {
            writer1.WriteCell(change.X, change.Y, change.Cell);
        }
        writer1.Flush();
        buffer.SwapBuffers();
        
        // Render content
        var rect = new Rectangle { ForegroundColor = new Color(255, 255, 0) };
        rect.Render(buffer, new Rect(0, 0, 4, 4));
        using var stream2 = new MemoryStream();
        using var writer2 = new AnsiWriter(stream2);
        int renderCells = 0;
        foreach (var change in buffer.GetChanges())
        {
            writer2.WriteCell(change.X, change.Y, change.Cell);
            renderCells++;
        }
        writer2.Flush();
        buffer.SwapBuffers();
        
        // Clear
        buffer.Clear();
        using var stream3 = new MemoryStream();
        using var writer3 = new AnsiWriter(stream3);
        int clearCells = 0;
        foreach (var change in buffer.GetChanges())
        {
            writer3.WriteCell(change.X, change.Y, change.Cell);
            clearCells++;
        }
        writer3.Flush();
        buffer.SwapBuffers();
        
        // Act - Clear again (should output nothing)
        buffer.Clear();
        using var stream4 = new MemoryStream();
        using var writer4 = new AnsiWriter(stream4);
        int secondClearCells = 0;
        foreach (var change in buffer.GetChanges())
        {
            writer4.WriteCell(change.X, change.Y, change.Cell);
            secondClearCells++;
        }
        writer4.Flush();
        
        // Assert - Render outputs cells
        await Assert.That(renderCells).IsGreaterThan(0);
        
        // Assert - First clear outputs cells (to clear rendered content)
        await Assert.That(clearCells).IsGreaterThan(0);
        
        // Assert - Second clear outputs nothing (already clear)
        await Assert.That(secondClearCells).IsEqualTo(0);
        await Assert.That(stream4.Length).IsEqualTo(0);
    }
    
    [Test]
    public async Task PartialRenderThenClear_OutputsOnlyChangedCells()
    {
        // Arrange - Render small rectangle in corner
        var buffer = new CellBuffer(10, 10);
        buffer.Clear();
        using var stream1 = new MemoryStream();
        using var writer1 = new AnsiWriter(stream1);
        foreach (var change in buffer.GetChanges())
        {
            writer1.WriteCell(change.X, change.Y, change.Cell);
        }
        writer1.Flush();
        buffer.SwapBuffers();
        
        // Render small 3x3 rectangle
        var rect = new Rectangle { ForegroundColor = new Color(100, 100, 100) };
        rect.Render(buffer, new Rect(0, 0, 3, 3));
        using var stream2 = new MemoryStream();
        using var writer2 = new AnsiWriter(stream2);
        int renderCells = 0;
        foreach (var change in buffer.GetChanges())
        {
            writer2.WriteCell(change.X, change.Y, change.Cell);
            renderCells++;
        }
        writer2.Flush();
        buffer.SwapBuffers();
        
        // Act - Clear entire buffer
        buffer.Clear();
        using var stream3 = new MemoryStream();
        using var writer3 = new AnsiWriter(stream3);
        int clearCells = 0;
        foreach (var change in buffer.GetChanges())
        {
            writer3.WriteCell(change.X, change.Y, change.Cell);
            clearCells++;
        }
        writer3.Flush();
        
        // Assert - Some cells were rendered (only the rectangle area)
        await Assert.That(renderCells).IsGreaterThan(0);
        await Assert.That(renderCells).IsEqualTo(9); // 3x3 rectangle
        
        // Assert - Clear outputs same number of cells (only cells that differ)
        // With the fix, Clear() efficiently detects only changed cells
        await Assert.That(clearCells).IsEqualTo(renderCells);
        await Assert.That(stream3.Length).IsGreaterThan(0);
    }
    
    [Test]
    public async Task EmptyBufferFromStart_ClearPresent_OutputsNothing()
    {
        // Arrange - Create buffer but don't render anything
        var buffer = new CellBuffer(5, 5);
        
        // Act - Clear and present without any prior rendering
        buffer.Clear();
        using var stream = new MemoryStream();
        using var writer = new AnsiWriter(stream);
        int cellsWritten = 0;
        foreach (var change in buffer.GetChanges())
        {
            writer.WriteCell(change.X, change.Y, change.Cell);
            cellsWritten++;
        }
        writer.Flush();
        
        // Assert - On fresh buffer, both _current and _previous are initialized to Cell.Empty
        // So Clear() outputs nothing (both buffers already clear)
        await Assert.That(cellsWritten).IsEqualTo(0);
        await Assert.That(stream.Length).IsEqualTo(0);
    }
    
    [Test]
    public async Task ClearAfterSwap_WithNoPriorPresent_DetectsChanges()
    {
        // Arrange - Create buffer, clear it, swap without presenting
        var buffer = new CellBuffer(3, 3);
        buffer.Clear();
        buffer.SwapBuffers();
        
        // Act - Clear again and check for changes
        buffer.Clear();
        using var stream = new MemoryStream();
        using var writer = new AnsiWriter(stream);
        int cellsWritten = 0;
        foreach (var change in buffer.GetChanges())
        {
            writer.WriteCell(change.X, change.Y, change.Cell);
            cellsWritten++;
        }
        writer.Flush();
        
        // Assert - Should output nothing (both buffers already clear)
        await Assert.That(cellsWritten).IsEqualTo(0);
        await Assert.That(stream.Length).IsEqualTo(0);
    }
}
