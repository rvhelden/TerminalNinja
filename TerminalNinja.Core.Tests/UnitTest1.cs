namespace TerminalNinja.Core.Tests;

public class Tests
{
    [Test]  // Available without explicit using statement
    public async Task MyTest()
    {
        await Assert.That(true).IsTrue();  // Assert is available automatically
    }
}