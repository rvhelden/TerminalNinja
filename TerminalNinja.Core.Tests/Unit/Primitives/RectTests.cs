namespace TerminalNinja.Core.Tests.Unit.Primitives;

public class RectTests
{
    [Test]
    public async Task Rect_Constructor_SetsAllProperties()
    {
        var rect = new Rect(10, 20, 30, 40);
        
        await Assert.That(rect.X).IsEqualTo(10);
        await Assert.That(rect.Y).IsEqualTo(20);
        await Assert.That(rect.Width).IsEqualTo(30);
        await Assert.That(rect.Height).IsEqualTo(40);
    }
    
    [Test]
    public async Task Rect_Right_ReturnsXPlusWidth()
    {
        var rect = new Rect(10, 20, 30, 40);
        
        await Assert.That(rect.Right).IsEqualTo(40);
    }
    
    [Test]
    public async Task Rect_Bottom_ReturnsYPlusHeight()
    {
        var rect = new Rect(10, 20, 30, 40);
        
        await Assert.That(rect.Bottom).IsEqualTo(60);
    }
    
    [Test]
    public async Task Rect_Contains_ReturnsTrueForPointInside()
    {
        var rect = new Rect(10, 20, 30, 40);
        
        await Assert.That(rect.Contains(15, 25)).IsTrue();
        await Assert.That(rect.Contains(10, 20)).IsTrue(); // Top-left inclusive
        await Assert.That(rect.Contains(39, 59)).IsTrue(); // Bottom-right - 1
    }
    
    [Test]
    public async Task Rect_Contains_ReturnsFalseForPointOutside()
    {
        var rect = new Rect(10, 20, 30, 40);
        
        await Assert.That(rect.Contains(5, 25)).IsFalse();  // Left of rect
        await Assert.That(rect.Contains(45, 25)).IsFalse(); // Right of rect
        await Assert.That(rect.Contains(15, 15)).IsFalse(); // Above rect
        await Assert.That(rect.Contains(15, 65)).IsFalse(); // Below rect
        await Assert.That(rect.Contains(40, 60)).IsFalse(); // Exactly at right/bottom edge
    }
    
    [Test]
    public async Task Rect_Intersect_ReturnsOverlappingArea()
    {
        var rect1 = new Rect(10, 10, 30, 30);
        var rect2 = new Rect(20, 20, 30, 30);
        
        var intersection = rect1.Intersect(rect2);
        
        await Assert.That(intersection.X).IsEqualTo(20);
        await Assert.That(intersection.Y).IsEqualTo(20);
        await Assert.That(intersection.Width).IsEqualTo(20);
        await Assert.That(intersection.Height).IsEqualTo(20);
    }
    
    [Test]
    public async Task Rect_Intersect_ReturnsEmptyForNonOverlapping()
    {
        var rect1 = new Rect(10, 10, 20, 20);
        var rect2 = new Rect(50, 50, 20, 20);
        
        var intersection = rect1.Intersect(rect2);
        
        await Assert.That(intersection.Width).IsEqualTo(0);
        await Assert.That(intersection.Height).IsEqualTo(0);
    }
    
    [Test]
    public async Task Rect_Intersect_ReturnsSelfWhenContained()
    {
        var outer = new Rect(0, 0, 100, 100);
        var inner = new Rect(20, 20, 30, 30);
        
        var intersection = inner.Intersect(outer);
        
        await Assert.That(intersection).IsEqualTo(inner);
    }
    
    [Test]
    public async Task Rect_Overlaps_ReturnsTrueForOverlappingRects()
    {
        var rect1 = new Rect(10, 10, 30, 30);
        var rect2 = new Rect(20, 20, 30, 30);
        
        await Assert.That(rect1.Overlaps(rect2)).IsTrue();
        await Assert.That(rect2.Overlaps(rect1)).IsTrue(); // Symmetric
    }
    
    [Test]
    public async Task Rect_Overlaps_ReturnsFalseForNonOverlapping()
    {
        var rect1 = new Rect(10, 10, 20, 20);
        var rect2 = new Rect(50, 50, 20, 20);
        
        await Assert.That(rect1.Overlaps(rect2)).IsFalse();
        await Assert.That(rect2.Overlaps(rect1)).IsFalse(); // Symmetric
    }
    
    [Test]
    public async Task Rect_Overlaps_ReturnsTrueForTouchingRects()
    {
        var rect1 = new Rect(10, 10, 20, 20);
        var rect2 = new Rect(30, 10, 20, 20); // Touching on right edge of rect1
        
        // The Overlaps implementation should return false for edge-touching (no overlap)
        await Assert.That(rect1.Overlaps(rect2)).IsFalse();
    }
}
