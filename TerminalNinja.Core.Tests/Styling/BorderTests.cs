namespace TerminalNinja.Core.Tests.Styling;

public class BorderTests
{
    [Test]
    public async Task Border_None_HasZeroWidth()
    {
        var border = Border.None;
        
        await Assert.That((int)border.Width).IsEqualTo(0);
        await Assert.That(border.HasBorder).IsFalse();
    }
    
    [Test]
    public async Task Border_Single_CreatesWithCorrectProperties()
    {
        var border = Border.Single(Color.Cyan);
        
        await Assert.That((int)border.Width).IsEqualTo(1);
        await Assert.That(border.Color).IsEqualTo(Color.Cyan);
        await Assert.That(border.Chars).IsEqualTo(BorderChars.Single);
        await Assert.That(border.HasBorder).IsTrue();
    }
    
    [Test]
    public async Task Border_Double_CreatesWithCorrectProperties()
    {
        var border = Border.Double(Color.Red);
        
        await Assert.That((int)border.Width).IsEqualTo(1);
        await Assert.That(border.Color).IsEqualTo(Color.Red);
        await Assert.That(border.Chars).IsEqualTo(BorderChars.Double);
        await Assert.That(border.HasBorder).IsTrue();
    }
    
    [Test]
    public async Task Border_Rounded_CreatesWithCorrectProperties()
    {
        var border = Border.Rounded(Color.Green);
        
        await Assert.That((int)border.Width).IsEqualTo(1);
        await Assert.That(border.Color).IsEqualTo(Color.Green);
        await Assert.That(border.Chars).IsEqualTo(BorderChars.Rounded);
        await Assert.That(border.HasBorder).IsTrue();
    }
    
    [Test]
    public async Task Border_Ascii_CreatesWithCorrectProperties()
    {
        var border = Border.Ascii(Color.Yellow);
        
        await Assert.That((int)border.Width).IsEqualTo(1);
        await Assert.That(border.Color).IsEqualTo(Color.Yellow);
        await Assert.That(border.Chars).IsEqualTo(BorderChars.Ascii);
        await Assert.That(border.HasBorder).IsTrue();
    }
    
    [Test]
    public async Task Border_HasBorder_FalseWhenWidthIsZero()
    {
        var border = new Border(0, Color.White, BorderChars.Single);
        
        await Assert.That(border.HasBorder).IsFalse();
    }
    
    [Test]
    public async Task Border_HasBorder_FalseWhenCharsAreEmpty()
    {
        var border = new Border(1, Color.White, BorderChars.None);
        
        await Assert.That(border.HasBorder).IsFalse();
    }
}

public class BorderCharsTests
{
    [Test]
    public async Task BorderChars_Single_HasCorrectCharacters()
    {
        var chars = BorderChars.Single;
        
        await Assert.That(chars.TopLeft).IsEqualTo('┌');
        await Assert.That(chars.TopRight).IsEqualTo('┐');
        await Assert.That(chars.BottomLeft).IsEqualTo('└');
        await Assert.That(chars.BottomRight).IsEqualTo('┘');
        await Assert.That(chars.Horizontal).IsEqualTo('─');
        await Assert.That(chars.Vertical).IsEqualTo('│');
        await Assert.That(chars.IsEmpty).IsFalse();
    }
    
    [Test]
    public async Task BorderChars_Double_HasCorrectCharacters()
    {
        var chars = BorderChars.Double;
        
        await Assert.That(chars.TopLeft).IsEqualTo('╔');
        await Assert.That(chars.TopRight).IsEqualTo('╗');
        await Assert.That(chars.BottomLeft).IsEqualTo('╚');
        await Assert.That(chars.BottomRight).IsEqualTo('╝');
        await Assert.That(chars.Horizontal).IsEqualTo('═');
        await Assert.That(chars.Vertical).IsEqualTo('║');
        await Assert.That(chars.IsEmpty).IsFalse();
    }
    
    [Test]
    public async Task BorderChars_Rounded_HasCorrectCharacters()
    {
        var chars = BorderChars.Rounded;
        
        await Assert.That(chars.TopLeft).IsEqualTo('╭');
        await Assert.That(chars.TopRight).IsEqualTo('╮');
        await Assert.That(chars.BottomLeft).IsEqualTo('╰');
        await Assert.That(chars.BottomRight).IsEqualTo('╯');
        await Assert.That(chars.Horizontal).IsEqualTo('─');
        await Assert.That(chars.Vertical).IsEqualTo('│');
        await Assert.That(chars.IsEmpty).IsFalse();
    }
    
    [Test]
    public async Task BorderChars_Ascii_HasCorrectCharacters()
    {
        var chars = BorderChars.Ascii;
        
        await Assert.That(chars.TopLeft).IsEqualTo('+');
        await Assert.That(chars.TopRight).IsEqualTo('+');
        await Assert.That(chars.BottomLeft).IsEqualTo('+');
        await Assert.That(chars.BottomRight).IsEqualTo('+');
        await Assert.That(chars.Horizontal).IsEqualTo('-');
        await Assert.That(chars.Vertical).IsEqualTo('|');
        await Assert.That(chars.IsEmpty).IsFalse();
    }
    
    [Test]
    public async Task BorderChars_None_IsEmpty()
    {
        var chars = BorderChars.None;
        
        await Assert.That(chars.IsEmpty).IsTrue();
        await Assert.That(chars.TopLeft).IsEqualTo('\0');
    }
    
    [Test]
    public async Task BorderChars_Constructor_SetsAllFields()
    {
        var chars = new BorderChars('A', 'B', 'C', 'D', 'E', 'F');
        
        await Assert.That(chars.TopLeft).IsEqualTo('A');
        await Assert.That(chars.TopRight).IsEqualTo('B');
        await Assert.That(chars.BottomLeft).IsEqualTo('C');
        await Assert.That(chars.BottomRight).IsEqualTo('D');
        await Assert.That(chars.Horizontal).IsEqualTo('E');
        await Assert.That(chars.Vertical).IsEqualTo('F');
    }
}
