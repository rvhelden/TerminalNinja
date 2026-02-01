namespace TerminalNinja.Tests.Unit.Controls;

public class StackChildTests
{
    [Test]
    public async Task Fixed_CreatesChildWithFixedSizeMode()
    {
        // Arrange
        var control = new Rectangle { Width = Size.Absolute(10), Height = Size.Absolute(5) };
        var fixedSize = 20;

        // Act
        var child = StackChild.Fixed(control, fixedSize);

        // Assert
        await Assert.That(child.Content).IsEqualTo(control);
        await Assert.That(child.SizeMode).IsEqualTo(ChildSizeMode.Fixed);
        await Assert.That(child.FixedSize).IsEqualTo(fixedSize);
    }

    [Test]
    public async Task Fixed_WithZeroSize_CreatesValidChild()
    {
        // Arrange
        var control = new Rectangle { Width = Size.Absolute(10), Height = Size.Absolute(5) };

        // Act
        var child = StackChild.Fixed(control, 0);

        // Assert
        await Assert.That(child.SizeMode).IsEqualTo(ChildSizeMode.Fixed);
        await Assert.That(child.FixedSize).IsEqualTo(0);
    }

    [Test]
    public async Task Stretch_CreatesChildWithStretchSizeMode()
    {
        // Arrange
        var control = new Rectangle { Width = Size.Absolute(10), Height = Size.Absolute(5) };

        // Act
        var child = StackChild.Stretch(control);

        // Assert
        await Assert.That(child.Content).IsEqualTo(control);
        await Assert.That(child.SizeMode).IsEqualTo(ChildSizeMode.Stretch);
        await Assert.That(child.FixedSize).IsEqualTo(0); // Should be default value
    }

    [Test]
    public async Task Auto_CreatesChildWithAutoSizeMode()
    {
        // Arrange
        var control = new Rectangle { Width = Size.Absolute(10), Height = Size.Absolute(5) };

        // Act
        var child = StackChild.Auto(control);

        // Assert
        await Assert.That(child.Content).IsEqualTo(control);
        await Assert.That(child.SizeMode).IsEqualTo(ChildSizeMode.Auto);
        await Assert.That(child.FixedSize).IsEqualTo(0); // Should be default value
    }

    [Test]
    public async Task StackChild_IsValueType()
    {
        // Arrange
        var control = new Rectangle { Width = Size.Absolute(10), Height = Size.Absolute(5) };
        var child1 = StackChild.Fixed(control, 10);

        // Act
        var child2 = child1; // Copy
        child2 = child2 with { FixedSize = 20 }; // Modify copy

        // Assert
        await Assert.That(child1.FixedSize).IsEqualTo(10); // Original unchanged
        await Assert.That(child2.FixedSize).IsEqualTo(20);
    }

    [Test]
    public async Task StackChild_EqualityWorks()
    {
        // Arrange
        var control = new Rectangle { Width = Size.Absolute(10), Height = Size.Absolute(5) };
        var child1 = StackChild.Fixed(control, 10);
        var child2 = StackChild.Fixed(control, 10);
        var child3 = StackChild.Fixed(control, 20);

        // Assert
        await Assert.That(child1).IsEqualTo(child2);
        await Assert.That(child1).IsNotEqualTo(child3);
    }

    [Test]
    public async Task StackChild_WithModifier_CreatesNewInstance()
    {
        // Arrange
        var element1 = new Rectangle { Width = Size.Absolute(10), Height = Size.Absolute(5) };
        var element2 = new Rectangle { Width = Size.Absolute(20), Height = Size.Absolute(10) };
        var child = StackChild.Fixed(element1, 10);

        // Act
        var modified = child with { Content = element2, FixedSize = 30 };

        // Assert
        await Assert.That(modified.Content).IsEqualTo(element2);
        await Assert.That(modified.FixedSize).IsEqualTo(30);
        await Assert.That(modified.SizeMode).IsEqualTo(ChildSizeMode.Fixed); // Unchanged
        await Assert.That(child.Content).IsEqualTo(element1); // Original unchanged
    }

    [Test]
    public async Task StackChild_DifferentFactoryMethods_ProduceDifferentSizeModes()
    {
        // Arrange
        var control = new Rectangle { Width = Size.Absolute(10), Height = Size.Absolute(5) };

        // Act
        var fixedChild = StackChild.Fixed(control, 10);
        var stretchChild = StackChild.Stretch(control);
        var autoChild = StackChild.Auto(control);

        // Assert
        await Assert.That(fixedChild.SizeMode).IsEqualTo(ChildSizeMode.Fixed);
        await Assert.That(stretchChild.SizeMode).IsEqualTo(ChildSizeMode.Stretch);
        await Assert.That(autoChild.SizeMode).IsEqualTo(ChildSizeMode.Auto);
        await Assert.That(fixedChild).IsNotEqualTo(stretchChild);
        await Assert.That(stretchChild).IsNotEqualTo(autoChild);
        await Assert.That(fixedChild).IsNotEqualTo(autoChild);
    }
}
