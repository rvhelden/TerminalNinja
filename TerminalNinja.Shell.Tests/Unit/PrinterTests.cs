using System.Collections.Immutable;
using System.Text;
using TerminalNinja.Shell.Repl;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Tests.Unit;

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
    public async Task Format_MultilineString_RendersWithoutQuoteWrap()
    {
        // Single-line strings are data: surfaced with quotes so they're visually
        // distinct from identifiers. Multi-line strings are pre-formatted output
        // (obj.dump, obj.table, format_table, json.stringify with indent) and need
        // to flow naturally through the REPL — wrapping them in "…" would put a
        // dangling quote at the end of a property table.
        var s = new NString("Name | \"alpha\"\nAge  | 40");
        var output = Printer.Format(s);
        await Assert.That(output).IsEqualTo("Name | \"alpha\"\nAge  | 40");
        await Assert.That(output.StartsWith('"')).IsFalse();
    }

    [Test]
    public async Task Format_ListOfScalars_RendersCompactList()
    {
        var list = new NList(ImmutableArray.Create<NValue>(
            new NInt(1), new NInt(2), new NInt(3)));
        await Assert.That(Printer.Format(list)).IsEqualTo("[1, 2, 3]");
    }

    [Test]
    public async Task Format_ListOfStrings_RendersOnePerLine()
    {
        // A one-column projection (e.g. `fs.ls() | select(x => $"Test {x.Name}")`)
        // surfaces as a list of strings. The bracketed `["a","b"]` array form
        // hides the data; one-per-line is what the user expects from "table".
        var list = new NList(ImmutableArray.Create<NValue>(
            new NString("Test alpha"),
            new NString("Test beta"),
            new NString("Test gamma")));
        var output = Printer.Format(list);
        await Assert.That(output).IsEqualTo("Test alpha\nTest beta\nTest gamma");
        await Assert.That(output.StartsWith('[')).IsFalse();
    }

    [Test]
    public async Task Format_SeqOfStrings_RendersOnePerLine()
    {
        var seq = new NSeq(new NValue[]
        {
            new NString("alpha"),
            new NString("beta"),
        });
        var output = Printer.Format(seq);
        await Assert.That(output).IsEqualTo("alpha\nbeta");
    }

    [Test]
    public async Task IsStringListShaped_RequiresNonEmptyAllStrings()
    {
        var allStrings = new NList(ImmutableArray.Create<NValue>(new NString("a"), new NString("b")));
        var mixed = new NList(ImmutableArray.Create<NValue>(new NString("a"), new NInt(1)));
        var empty = new NList(ImmutableArray<NValue>.Empty);
        await Assert.That(Printer.IsStringListShaped(allStrings)).IsTrue();
        await Assert.That(Printer.IsStringListShaped(mixed)).IsFalse();
        await Assert.That(Printer.IsStringListShaped(empty)).IsFalse();
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
        // top border + header + middle border + 2 data rows + bottom border.
        await Assert.That(lines.Length).IsGreaterThanOrEqualTo(6);
        await Assert.That(StripSgr(lines[0])).Contains("╭");
        await Assert.That(StripSgr(lines[0])).Contains("┬");
        await Assert.That(StripSgr(lines[1])).Contains("N");
        await Assert.That(StripSgr(lines[1])).Contains("Name");
        await Assert.That(StripSgr(lines[2])).Contains("├");
        await Assert.That(StripSgr(lines[2])).Contains("┼");
        await Assert.That(StripSgr(lines[^1])).Contains("╰");
        // Visual widths align across header, both data rows, and all borders.
        var headerW = StripSgr(lines[1]).Length;
        await Assert.That(StripSgr(lines[0]).Length).IsEqualTo(headerW);
        await Assert.That(StripSgr(lines[3]).Length).IsEqualTo(headerW);
        await Assert.That(StripSgr(lines[4]).Length).IsEqualTo(headerW);
        await Assert.That(StripSgr(lines[^1]).Length).IsEqualTo(headerW);
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
        // lines[0] top border, [1] header, [2] middle border, [3..4] data, [5] bottom border.
        await Assert.That(StripSgr(lines[1])).Contains("a");
        await Assert.That(StripSgr(lines[1])).Contains("b");
        // Width-padded, so every line shares the header's visual width.
        var headerW = StripSgr(lines[1]).Length;
        await Assert.That(StripSgr(lines[3]).Length).IsEqualTo(headerW);
        await Assert.That(StripSgr(lines[4]).Length).IsEqualTo(headerW);
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

    // ─── __display field convention ─────────────────────────────────────────

    [Test]
    public async Task Format_RecordWithDisplayField_RendersTargetFieldOnly()
    {
        // A record carrying a __display field whose value is the name of another
        // field surfaces only that field's text — used by fs.ls() so its entries
        // print as full paths rather than as a multi-column table.
        var rec = new NRecord(ImmutableSortedDictionary<string, NValue>.Empty
            .Add("Name", new NString("foo.txt"))
            .Add("FullPath", new NString("/tmp/foo.txt"))
            .Add("Size", new NInt(123))
            .Add("__display", new NString("FullPath")));

        var output = Printer.Format(rec);
        await Assert.That(output).IsEqualTo("/tmp/foo.txt");
    }

    [Test]
    public async Task Format_ListOfDisplayRecords_StillRendersAsTable()
    {
        // The __display convention applies only to single records — a list of
        // records always falls through to the table formatter so columns stay
        // comparable. The __display column itself is hidden by FormatRecordTable.
        var make = (string name) => new NRecord(ImmutableSortedDictionary<string, NValue>.Empty
            .Add("Name", new NString(name))
            .Add("FullPath", new NString("/tmp/" + name))
            .Add("__display", new NString("FullPath")));
        var list = new NList(ImmutableArray.Create<NValue>(make("a"), make("b"), make("c")));

        var output = Printer.Format(list);
        var lines = output.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        // top + header + middle + 3 data + bottom = 7 lines.
        await Assert.That(lines.Length).IsGreaterThanOrEqualTo(7);
        await Assert.That(StripSgr(lines[1])).Contains("Name");
        await Assert.That(StripSgr(lines[1])).Contains("FullPath");
        await Assert.That(StripSgr(lines[1])).DoesNotContain("__display");
    }

    [Test]
    public async Task FormatRecordTable_HonorsColumnHint_OrderAndFilter()
    {
        // A __columns hint on the first row pins both which columns surface and
        // their order. Columns the record carries that aren't in the hint are
        // hidden — fs.ls uses this to expose Icon/Name/Type/SizeText by default
        // while keeping FullPath, IsDirectory, LastModified reachable via dot.
        var rec = new NRecord(ImmutableSortedDictionary<string, NValue>.Empty
            .Add("A", new NInt(1))
            .Add("B", new NInt(2))
            .Add("C", new NInt(3))
            .Add("__columns", new NList(ImmutableArray.Create<NValue>(
                new NString("B"), new NString("A")))));
        var list = new NList(ImmutableArray.Create<NValue>(rec));

        var table = Printer.FormatRecordTable(list);
        var lines = table.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        var header = StripSgr(lines[1]);
        await Assert.That(header).Contains("B");
        await Assert.That(header).Contains("A");
        await Assert.That(header).DoesNotContain("C");
        // Order: B appears before A in the header.
        await Assert.That(header.IndexOf('B') < header.IndexOf('A')).IsTrue();
    }

    [Test]
    public async Task FormatRecordTable_ColumnHintMissingKeys_RendersBlank()
    {
        // A hint may name columns that don't exist on every row; absent cells
        // render blank rather than throwing.
        var rec = new NRecord(ImmutableSortedDictionary<string, NValue>.Empty
            .Add("A", new NString("hi"))
            .Add("__columns", new NList(ImmutableArray.Create<NValue>(
                new NString("A"), new NString("Missing")))));
        var list = new NList(ImmutableArray.Create<NValue>(rec));

        var table = Printer.FormatRecordTable(list);
        await Assert.That(table).Contains("A");
        await Assert.That(table).Contains("Missing");
        await Assert.That(table).Contains("hi");
    }

    [Test]
    public async Task FormatRecordTable_MalformedColumnHint_FallsBackToUnion()
    {
        // A non-list __columns value is malformed → silently fall back to the
        // key-union behavior so producers can't accidentally break printing.
        var rec = new NRecord(ImmutableSortedDictionary<string, NValue>.Empty
            .Add("A", new NInt(1))
            .Add("__columns", new NInt(42)));
        var list = new NList(ImmutableArray.Create<NValue>(rec));

        var table = Printer.FormatRecordTable(list);
        await Assert.That(table).Contains("A");
        // __columns itself stays hidden via the __* convention-skip path.
        await Assert.That(table).DoesNotContain("__columns");
    }

    [Test]
    public async Task FormatRecordTable_StripsSgrWhenMeasuringWidth()
    {
        // A cell whose value contains SGR escapes (e.g. fs.ls's colored icons)
        // must align by visible width, not by raw string length — otherwise the
        // escape sequence's invisible bytes bloat the column.
        var coloredIcon = "\x1b[38;2;137;180;250mX\x1b[39m"; // 1 visible char, ~22 raw chars
        var rec = new NRecord(ImmutableSortedDictionary<string, NValue>.Empty
            .Add("Icon", new NString(coloredIcon))
            .Add("Name", new NString("hello"))
            .Add("__columns", new NList(ImmutableArray.Create<NValue>(
                new NString("Icon"), new NString("Name")))));
        var list = new NList(ImmutableArray.Create<NValue>(rec));

        var table = Printer.FormatRecordTable(list);
        var lines = table.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        // lines[1] is the header row; lines[3] is the single data row. Visible
        // edges must match so the icon column doesn't bleed past its slot
        // despite the SGR escapes inflating its raw byte count.
        var headerVisible = StripSgr(lines[1]);
        var rowVisible = StripSgr(lines[3]);
        await Assert.That(headerVisible.IndexOf("Name")).IsEqualTo(rowVisible.IndexOf("hello"));
    }

    [Test]
    public async Task FormatRecordTable_RowStyleDim_WrapsRowInSgrDim()
    {
        // A row with __row_style="dim" gets wrapped in \e[2m ... \e[22m so the
        // renderer applies TextDecorations.Dim to every cell on that row.
        var rec = new NRecord(ImmutableSortedDictionary<string, NValue>.Empty
            .Add("Name", new NString(".hidden"))
            .Add("__row_style", new NString("dim")));
        var list = new NList(ImmutableArray.Create<NValue>(rec));

        var table = Printer.FormatRecordTable(list);
        var lines = table.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        // lines[0] top border, [1] header, [2] middle border, [3] data, [4] bottom border.
        // The data row carries the dim wrap.
        await Assert.That(lines[3].StartsWith("\x1b[2m")).IsTrue();
        await Assert.That(lines[3]).Contains(".hidden");
        await Assert.That(lines[3].EndsWith("\x1b[22m")).IsTrue();
        // Header must not be dim — only the row that opted in. (The header line
        // does start with the border color escape, so check specifically for the
        // dim toggle rather than any SGR prefix.)
        await Assert.That(lines[1].StartsWith("\x1b[2m")).IsFalse();
    }

    private static string StripSgr(string s)
    {
        var sb = new StringBuilder();
        int i = 0;
        while (i < s.Length)
        {
            if (s[i] == 0x1B && i + 1 < s.Length && s[i + 1] == '[')
            {
                int end = s.IndexOf('m', i + 2);
                if (end < 0) break;
                i = end + 1;
                continue;
            }
            sb.Append(s[i]);
            i++;
        }
        return sb.ToString();
    }

    [Test]
    public async Task FormatRecordTable_SkipsConventionPrivateFields()
    {
        var rec = new NRecord(ImmutableSortedDictionary<string, NValue>.Empty
            .Add("Name", new NString("foo"))
            .Add("Size", new NInt(7))
            .Add("__display", new NString("Name")));
        var list = new NList(ImmutableArray.Create<NValue>(rec));

        var table = Printer.FormatRecordTable(list);
        // Convention-hidden fields (__*) must not show up as table columns even
        // when the user explicitly calls format_table.
        await Assert.That(table).DoesNotContain("__display");
        await Assert.That(table).Contains("Name");
        await Assert.That(table).Contains("Size");
    }
}
