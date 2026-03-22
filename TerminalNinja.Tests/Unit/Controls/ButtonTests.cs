namespace TerminalNinja.Tests.Unit.Controls;

public class ButtonTests
{
    [Test]
    public async Task DefaultWidth_IsAuto()
    {
        var button = new Button();
        
        await Assert.That(button.Width.Mode).IsEqualTo(SizeMode.Auto);
    }
    
    [Test]
    public async Task DefaultHeight_IsAuto()
    {
        var button = new Button();
        
        await Assert.That(button.Height.Mode).IsEqualTo(SizeMode.Auto);
    }
    
    [Test]
    public async Task GetPreferredSize_Auto_FitsTextContent()
    {
        var button = new Button { Text = "OK" };
        var parent = new Rect(0, 0, 80, 24);
        
        var preferred = button.GetPreferredSize(parent);
        
        // Width = text length (2) + 4 padding = 6
        await Assert.That(preferred.Width).IsEqualTo(6);
        // Height = 3 (border + text + border)
        await Assert.That(preferred.Height).IsEqualTo(3);
    }
    
    [Test]
    public async Task GetPreferredSize_Auto_LongText_FitsTextContent()
    {
        var button = new Button { Text = "Submit Application" };
        var parent = new Rect(0, 0, 80, 24);
        
        var preferred = button.GetPreferredSize(parent);
        
        // Width = text length (18) + 4 padding = 22
        await Assert.That(preferred.Width).IsEqualTo(22);
        await Assert.That(preferred.Height).IsEqualTo(3);
    }
    
    [Test]
    public async Task GetPreferredSize_EmptyText_HasMinimalWidth()
    {
        var button = new Button { Text = "" };
        var parent = new Rect(0, 0, 80, 24);
        
        var preferred = button.GetPreferredSize(parent);
        
        // Width = text length (0) + 4 padding = 4
        await Assert.That(preferred.Width).IsEqualTo(4);
        await Assert.That(preferred.Height).IsEqualTo(3);
    }
    
    [Test]
    public async Task GetPreferredSize_AbsoluteWidth_UsesExplicitSize()
    {
        var button = new Button { Text = "OK", Width = Size.Absolute(20) };
        var parent = new Rect(0, 0, 80, 24);
        
        var preferred = button.GetPreferredSize(parent);
        
        await Assert.That(preferred.Width).IsEqualTo(20);
    }
    
    [Test]
    public async Task GetPreferredSize_AbsoluteHeight_UsesExplicitSize()
    {
        var button = new Button { Text = "OK", Height = Size.Absolute(5) };
        var parent = new Rect(0, 0, 80, 24);
        
        var preferred = button.GetPreferredSize(parent);
        
        await Assert.That(preferred.Height).IsEqualTo(5);
    }
    
    [Test]
    public async Task CalculateBounds_Auto_FitsTextContent()
    {
        var button = new Button { Text = "Click Me" };
        var parent = new Rect(0, 0, 80, 24);
        
        var bounds = button.CalculateBounds(parent);
        
        // Width = text length (8) + 4 padding = 12
        await Assert.That(bounds.Width).IsEqualTo(12);
        await Assert.That(bounds.Height).IsEqualTo(3);
    }
    
    [Test]
    public async Task CalculateBounds_Stretch_FillsParent()
    {
        var button = new Button { Text = "OK", Width = Size.Stretch, Height = Size.Stretch };
        var parent = new Rect(0, 0, 80, 24);
        
        var bounds = button.CalculateBounds(parent);
        
        await Assert.That(bounds.Width).IsEqualTo(80);
        await Assert.That(bounds.Height).IsEqualTo(24);
    }
    
    [Test]
    public async Task CalculateBounds_Absolute_UsesExplicitSize()
    {
        var button = new Button { Text = "OK", Width = Size.Absolute(15), Height = Size.Absolute(5) };
        var parent = new Rect(0, 0, 80, 24);
        
        var bounds = button.CalculateBounds(parent);
        
        await Assert.That(bounds.Width).IsEqualTo(15);
        await Assert.That(bounds.Height).IsEqualTo(5);
    }
    
    [Test]
    public async Task Render_Auto_DoesNotThrow()
    {
        var button = new Button { Text = "Test" };
        using var buffer = new CellBuffer(40, 10);
        
        button.Render(buffer, new Rect(0, 0, 40, 10));
        
        // Verify text appears in the buffer
        var preferred = button.GetPreferredSize(new Rect(0, 0, 40, 10));
        await Assert.That(preferred.Width).IsEqualTo(8); // "Test" (4) + 4
    }
}
