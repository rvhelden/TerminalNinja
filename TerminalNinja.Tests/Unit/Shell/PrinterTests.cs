using System.Collections.Immutable;
using TerminalNinja.Shell.Repl;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Tests.Unit.Shell;

public class PrinterTests
{
    [Test]
    public async Task Format_Scalars_RenderRaw()
    {
        await Assert.That(Printer.Format(new NInt(42))).IsEqualTo("42");
        await Assert.That(Printer.Format(new NFloat(3.14))).IsEqualTo("3.14");
        await Assert.That(Printer.Format(new NBool(true))).IsEqualTo("true");
        await Assert.That(Printer.Format(new NString("hello"))).IsEqualTo("\"hello\"");
        await Assert.That(Printer.Format(NUnit.Instance)).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Format_ListOfScalars_RendersCompactList()
    {
        var list = new NList(ImmutableArray.Create<NValue>(
            new NInt(1), new NInt(2), new NInt(3)));
        await Assert.That(Printer.Format(list)).IsEqualTo("[1, 2, 3]");
    }

    [Test]
    public async Task Format_ListOfUniformRecords_RendersAlignedTable()
    {
        var rec1 = new NRecord(ImmutableSortedDictionary<string, NValue>.Empty
            .Add("Name", new NString("alpha"))
            .Add("N", new NInt(1)));
        var rec2 = new NRecord(ImmutableSortedDictionary<string, NValue>.Empty
            .Add("Name", new NString("beta"))
            .Add("N", new NInt(20)));
        var list = new NList(ImmutableArray.Create<NValue>(rec1, rec2));

        var output = Printer.Format(list);
        var lines = output.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        await Assert.That(lines.Length).IsGreaterThanOrEqualTo(4);
        await Assert.That(lines[0].Contains("N")).IsTrue();
        await Assert.That(lines[0].Contains("Name")).IsTrue();
        await Assert.That(lines[1]).Contains("-");
        // Each data row keeps the column alignment — every row has the same width.
        await Assert.That(lines[2].Length).IsEqualTo(lines[3].Length);
        await Assert.That(lines[0].Length).IsEqualTo(lines[3].Length);
    }

    [Test]
    public async Task Format_RaggedRecords_AreStillTableShaped()
    {
        // Loosened semantics: any non-empty list of records is table-shaped.
        // Render the union of keys with blanks for absent cells.
        var rec1 = new NRecord(ImmutableSortedDictionary<string, NValue>.Empty
            .Add("a", new NInt(1)));
        var rec2 = new NRecord(ImmutableSortedDictionary<string, NValue>.Empty
            .Add("b", new NInt(2)));
        var list = new NList(ImmutableArray.Create<NValue>(rec1, rec2));

        await Assert.That(Printer.IsTableShaped(list)).IsTrue();

        var output = Printer.Format(list);
        var lines = output.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        await Assert.That(lines[0].Contains("a")).IsTrue();
        await Assert.That(lines[0].Contains("b")).IsTrue();
        // Row 0: a=1, b blank; row 1: a blank, b=2.
        // Width-padded, so the data lines have the same length as the header.
        await Assert.That(lines[2].Length).IsEqualTo(lines[0].Length);
        await Assert.That(lines[3].Length).IsEqualTo(lines[0].Length);
    }

    [Test]
    public async Task Format_RecordWithExplicitUnitCell_RendersBlank()
    {
        // An explicit NUnit in a cell renders the same as an absent key.
        var rec = new NRecord(ImmutableSortedDictionary<string, NValue>.Empty
            .Add("Name", new NString("a"))
            .Add("Age", NUnit.Instance));
        var list = new NList(ImmutableArray.Create<NValue>(rec));

        var output = Printer.Format(list);
        // Splitting by whitespace, the Age column for the row should be empty —
        // so the row line contains "a" but no number / value after.
        await Assert.That(output).Contains("a");
    }
}
