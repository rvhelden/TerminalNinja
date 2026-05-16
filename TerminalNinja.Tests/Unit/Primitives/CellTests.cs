namespace TerminalNinja.Tests.Unit.Primitives;

public class CellTests
{
    [Test]
    public async Task Cell_Constructor_SetsAllProperties()
    {
        var cell = new Cell('A', Color.Red, Color.Blue);
        
        await Assert.That(cell.Codepoint).IsEqualTo('A');
        await Assert.That(cell.Foreground).IsEqualTo(Color.Red);
        await Assert.That(cell.Background).IsEqualTo(Color.Blue);
    }
    
    [Test]
    public async Task Cell_Empty_HasCorrectDefaults()
    {
        var cell = Cell.Empty;
        
        await Assert.That(cell.Codepoint).IsEqualTo(' ');
        await Assert.That(cell.Foreground).IsEqualTo(Color.White);
        await Assert.That(cell.Background).IsEqualTo(Color.Black);
    }
    
    [Test]
    public async Task Cell_Equality_ComparesAllProperties()
    {
        var cell1 = new Cell('A', Color.Red, Color.Blue);
        var cell2 = new Cell('A', Color.Red, Color.Blue);
        var cell3 = new Cell('B', Color.Red, Color.Blue);
        
        await Assert.That(cell1).IsEqualTo(cell2);
        await Assert.That(cell1).IsNotEqualTo(cell3);
    }
    
    [Test]
    public async Task Cell_DifferentForeground_AreNotEqual()
    {
        var cell1 = new Cell('A', Color.Red, Color.Blue);
        var cell2 = new Cell('A', Color.Green, Color.Blue);
        
        await Assert.That(cell1).IsNotEqualTo(cell2);
    }
    
    [Test]
    public async Task Cell_DifferentBackground_AreNotEqual()
    {
        var cell1 = new Cell('A', Color.Red, Color.Blue);
        var cell2 = new Cell('A', Color.Red, Color.Green);
        
        await Assert.That(cell1).IsNotEqualTo(cell2);
    }
}
