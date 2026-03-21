namespace TerminalNinja.Tests.Unit.Primitives;

public class SizeTests
{
    [Test]
    [MethodDataSource(nameof(GetSizeResolutionCases))]
    public async Task Resolve_ReturnsExpectedValue(Size size, int parentSize, int expected)
    {
        var result = size.Resolve(parentSize);
        
        await Assert.That(result).IsEqualTo(expected);
    }

    public static IEnumerable<(Size, int, int)> GetSizeResolutionCases()
    {
        // Absolute sizes
        yield return (Size.Absolute(50), 100, 50);
        yield return (Size.Absolute(50), 200, 50);
        yield return (Size.Absolute(0), 100, 0);
        yield return (Size.Absolute(100), 50, 100); // Larger than parent
        
        // Percentage sizes
        yield return (Size.Percent(50), 100, 50);
        yield return (Size.Percent(50), 200, 100);
        yield return (Size.Percent(100), 80, 80);
        yield return (Size.Percent(25), 100, 25);
        yield return (Size.Percent(75), 200, 150);
        yield return (Size.Percent(0), 100, 0);
        
        // Stretch
        yield return (Size.Stretch, 100, 100);
        yield return (Size.Stretch, 50, 50);
        yield return (Size.Stretch, 1, 1);
        
        // Auto (resolves to parent size as fallback)
        yield return (Size.Auto, 100, 100);
        yield return (Size.Auto, 50, 50);
        yield return (Size.Auto, 0, 0);
        
        // Edge cases
        yield return (Size.Percent(50), 0, 0);
        yield return (Size.Absolute(10), 0, 10);
    }
    
    [Test]
    public async Task Size_Absolute_CreatesAbsoluteSize()
    {
        var size = Size.Absolute(100);
        
        await Assert.That(size.Value).IsEqualTo(100f);
        await Assert.That(size.Mode).IsEqualTo(SizeMode.Absolute);
    }
    
    [Test]
    public async Task Size_Percent_CreatesRelativeSize()
    {
        var size = Size.Percent(50);
        
        await Assert.That(size.Value).IsEqualTo(0.5f);
        await Assert.That(size.Mode).IsEqualTo(SizeMode.Relative);
    }
    
    [Test]
    public async Task Size_Stretch_CreatesStretchSize()
    {
        var size = Size.Stretch;
        
        await Assert.That(size.Value).IsEqualTo(1f);
        await Assert.That(size.Mode).IsEqualTo(SizeMode.Stretch);
    }
    
    [Test]
    public async Task Size_Auto_CreatesAutoSize()
    {
        var size = Size.Auto;
        
        await Assert.That(size.Value).IsEqualTo(0f);
        await Assert.That(size.Mode).IsEqualTo(SizeMode.Auto);
    }
}
