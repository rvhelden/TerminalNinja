namespace TerminalNinja.Tests.Unit.Buffers;

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
