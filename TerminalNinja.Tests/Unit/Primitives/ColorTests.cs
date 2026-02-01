namespace TerminalNinja.Tests.Unit.Primitives;

public class ColorTests
{
    [Test]
    [MethodDataSource(nameof(GetHexParsingCases))]
    public async Task FromHex_ParsesCorrectly(string hex, byte r, byte g, byte b)
    {
        var color = Color.FromHex(hex);
        
        await Assert.That(color.R).IsEqualTo(r);
        await Assert.That(color.G).IsEqualTo(g);
        await Assert.That(color.B).IsEqualTo(b);
    }

    public static IEnumerable<(string, byte, byte, byte)> GetHexParsingCases()
    {
        yield return ("#FF0000", 255, 0, 0);
        yield return ("FF0000", 255, 0, 0);  // Without # prefix
        yield return ("#00FF00", 0, 255, 0);
        yield return ("#0000FF", 0, 0, 255);
        yield return ("#FFFFFF", 255, 255, 255);
        yield return ("#000000", 0, 0, 0);
        yield return ("#123456", 0x12, 0x34, 0x56);
        yield return ("#ABCDEF", 0xAB, 0xCD, 0xEF);
    }
    
    [Test]
    public async Task FromHex_InvalidLength_ThrowsArgumentException()
    {
        var ex1 = await Assert.That(() => Color.FromHex("FFF"))
            .ThrowsExactly<ArgumentException>();
        
        await Assert.That(ex1?.Message ?? "").Contains("6 characters");
        
        var ex2 = await Assert.That(() => Color.FromHex("#FF"))
            .ThrowsExactly<ArgumentException>();
        
        await Assert.That(ex2?.Message ?? "").Contains("6 characters");
    }
    
    [Test]
    public async Task ToHex_FormatsCorrectly()
    {
        var color = new Color(255, 128, 64);
        
        var hex = color.ToHex();
        
        await Assert.That(hex).IsEqualTo("#FF8040");
    }
    
    [Test]
    public async Task Color_PredefinedColors_HaveCorrectValues()
    {
        await Assert.That(Color.Black).IsEqualTo(new Color(0, 0, 0));
        await Assert.That(Color.White).IsEqualTo(new Color(255, 255, 255));
        await Assert.That(Color.Red).IsEqualTo(new Color(255, 0, 0));
        await Assert.That(Color.Green).IsEqualTo(new Color(0, 255, 0));
        await Assert.That(Color.Blue).IsEqualTo(new Color(0, 0, 255));
        await Assert.That(Color.Cyan).IsEqualTo(new Color(0, 255, 255));
        await Assert.That(Color.Magenta).IsEqualTo(new Color(255, 0, 255));
        await Assert.That(Color.Yellow).IsEqualTo(new Color(255, 255, 0));
        await Assert.That(Color.Gray).IsEqualTo(new Color(128, 128, 128));
        await Assert.That(Color.DarkGray).IsEqualTo(new Color(64, 64, 64));
    }
    
    [Test]
    public async Task Color_Equality_ComparesRGB()
    {
        var color1 = new Color(100, 150, 200);
        var color2 = new Color(100, 150, 200);
        var color3 = new Color(100, 150, 201);
        
        await Assert.That(color1).IsEqualTo(color2);
        await Assert.That(color1).IsNotEqualTo(color3);
    }
    
    [Test]
    public async Task FromHex_ToHex_RoundTrip()
    {
        var original = new Color(123, 45, 67);
        var hex = original.ToHex();
        var parsed = Color.FromHex(hex);
        
        await Assert.That(parsed).IsEqualTo(original);
    }
}
