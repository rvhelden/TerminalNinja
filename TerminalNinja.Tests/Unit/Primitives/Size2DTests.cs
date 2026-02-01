namespace TerminalNinja.Tests.Unit.Primitives;

/// <summary>
/// Tests for Size2D record struct covering:
/// - Construction
/// - Property access
/// - Equality and comparison
/// - Value type semantics
/// </summary>
public class Size2DTests
{
    [Test]
    public async Task Constructor_CreatesInstanceWithCorrectValues()
    {
        // Arrange & Act
        var size = new Size2D(100, 50);

        // Assert
        await Assert.That(size.Width).IsEqualTo(100);
        await Assert.That(size.Height).IsEqualTo(50);
    }

    [Test]
    public async Task Size2D_IsValueType()
    {
        // Arrange
        var size1 = new Size2D(10, 20);

        // Act
        var size2 = size1; // Copy
        size2 = new Size2D(30, 40); // Modify copy

        // Assert
        await Assert.That(size1.Width).IsEqualTo(10); // Original unchanged
        await Assert.That(size1.Height).IsEqualTo(20);
        await Assert.That(size2.Width).IsEqualTo(30);
        await Assert.That(size2.Height).IsEqualTo(40);
    }

    [Test]
    public async Task Equality_WorksCorrectly()
    {
        // Arrange
        var size1 = new Size2D(100, 50);
        var size2 = new Size2D(100, 50);
        var size3 = new Size2D(100, 60);
        var size4 = new Size2D(90, 50);

        // Assert
        await Assert.That(size1).IsEqualTo(size2);
        await Assert.That(size1).IsNotEqualTo(size3); // Different height
        await Assert.That(size1).IsNotEqualTo(size4); // Different width
    }

    [Test]
    public async Task ZeroSize_CanBeCreated()
    {
        // Arrange & Act
        var size = new Size2D(0, 0);

        // Assert
        await Assert.That(size.Width).IsEqualTo(0);
        await Assert.That(size.Height).IsEqualTo(0);
    }

    [Test]
    public async Task NegativeValues_AreAllowed()
    {
        // Arrange & Act
        var size = new Size2D(-10, -20);

        // Assert
        await Assert.That(size.Width).IsEqualTo(-10);
        await Assert.That(size.Height).IsEqualTo(-20);
    }
}
