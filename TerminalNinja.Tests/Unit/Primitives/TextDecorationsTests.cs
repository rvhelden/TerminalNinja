using TerminalNinja.Xaml.TypeConverters;

namespace TerminalNinja.Tests.Unit.Primitives;

public class TextDecorationsTests
{
    [Test]
    public async Task TextDecorations_None_HasValueZero()
    {
        byte value = (byte)TextDecorations.None;
        await Assert.That(value).IsEqualTo((byte)0);
    }

    [Test]
    public async Task TextDecorations_FlagsCombination_WorksWithBitwiseOr()
    {
        var combined = TextDecorations.Bold | TextDecorations.Underline;

        await Assert.That(combined.HasFlag(TextDecorations.Bold)).IsTrue();
        await Assert.That(combined.HasFlag(TextDecorations.Underline)).IsTrue();
        await Assert.That(combined.HasFlag(TextDecorations.Italic)).IsFalse();
    }

    [Test]
    public async Task TextDecorations_AllFlags_AreDistinct()
    {
        var all = TextDecorations.Bold | TextDecorations.Dim | TextDecorations.Italic |
                  TextDecorations.Underline | TextDecorations.Blink | TextDecorations.Inverse |
                  TextDecorations.Strikethrough;

        await Assert.That(all.HasFlag(TextDecorations.Bold)).IsTrue();
        await Assert.That(all.HasFlag(TextDecorations.Dim)).IsTrue();
        await Assert.That(all.HasFlag(TextDecorations.Italic)).IsTrue();
        await Assert.That(all.HasFlag(TextDecorations.Underline)).IsTrue();
        await Assert.That(all.HasFlag(TextDecorations.Blink)).IsTrue();
        await Assert.That(all.HasFlag(TextDecorations.Inverse)).IsTrue();
        await Assert.That(all.HasFlag(TextDecorations.Strikethrough)).IsTrue();
    }

    [Test]
    public async Task Cell_FourParamConstructor_SetsDecorations()
    {
        var cell = new Cell('X', Color.Red, Color.Blue, TextDecorations.Bold | TextDecorations.Italic);

        await Assert.That(cell.Character).IsEqualTo('X');
        await Assert.That(cell.Foreground).IsEqualTo(Color.Red);
        await Assert.That(cell.Background).IsEqualTo(Color.Blue);
        await Assert.That(cell.Decorations).IsEqualTo(TextDecorations.Bold | TextDecorations.Italic);
    }

    [Test]
    public async Task Cell_ThreeParamConstructor_DecorationsDefaultToNone()
    {
        var cell = new Cell('A', Color.White, Color.Black);

        await Assert.That(cell.Decorations).IsEqualTo(TextDecorations.None);
    }

    [Test]
    public async Task Cell_Empty_HasNoDecorations()
    {
        await Assert.That(Cell.Empty.Decorations).IsEqualTo(TextDecorations.None);
    }

    [Test]
    public async Task Cell_DifferentDecorations_AreNotEqual()
    {
        var cell1 = new Cell('A', Color.White, Color.Black, TextDecorations.Bold);
        var cell2 = new Cell('A', Color.White, Color.Black, TextDecorations.Italic);

        await Assert.That(cell1).IsNotEqualTo(cell2);
    }

    [Test]
    public async Task Cell_SameDecorations_AreEqual()
    {
        var cell1 = new Cell('A', Color.White, Color.Black, TextDecorations.Bold);
        var cell2 = new Cell('A', Color.White, Color.Black, TextDecorations.Bold);

        await Assert.That(cell1).IsEqualTo(cell2);
    }

    // ─── TypeConverter Tests ────────────────────────────────────────

    [Test]
    public async Task TypeConverter_SingleValue_ParsesBold()
    {
        var converter = new TextDecorationsTypeConverter();
        var result = (TextDecorations)converter.ConvertFrom(null, null, "Bold")!;

        await Assert.That(result).IsEqualTo(TextDecorations.Bold);
    }

    [Test]
    public async Task TypeConverter_CommaSeparated_ParsesMultiple()
    {
        var converter = new TextDecorationsTypeConverter();
        var result = (TextDecorations)converter.ConvertFrom(null, null, "Bold,Underline")!;

        await Assert.That(result).IsEqualTo(TextDecorations.Bold | TextDecorations.Underline);
    }

    [Test]
    public async Task TypeConverter_WithSpaces_ParsesCorrectly()
    {
        var converter = new TextDecorationsTypeConverter();
        var result = (TextDecorations)converter.ConvertFrom(null, null, "Italic, Strikethrough")!;

        await Assert.That(result).IsEqualTo(TextDecorations.Italic | TextDecorations.Strikethrough);
    }

    [Test]
    public async Task TypeConverter_None_ReturnsNone()
    {
        var converter = new TextDecorationsTypeConverter();
        var result = (TextDecorations)converter.ConvertFrom(null, null, "None")!;

        await Assert.That(result).IsEqualTo(TextDecorations.None);
    }

    [Test]
    public async Task TypeConverter_EmptyString_ReturnsNone()
    {
        var converter = new TextDecorationsTypeConverter();
        var result = (TextDecorations)converter.ConvertFrom(null, null, "")!;

        await Assert.That(result).IsEqualTo(TextDecorations.None);
    }

    [Test]
    public async Task TypeConverter_CaseInsensitive_ParsesCorrectly()
    {
        var converter = new TextDecorationsTypeConverter();
        var result = (TextDecorations)converter.ConvertFrom(null, null, "BOLD,italic,UNDERLINE")!;

        await Assert.That(result).IsEqualTo(TextDecorations.Bold | TextDecorations.Italic | TextDecorations.Underline);
    }

    [Test]
    public async Task TypeConverter_InvalidValue_ThrowsFormatException()
    {
        var converter = new TextDecorationsTypeConverter();

        await Assert.That(() => converter.ConvertFrom(null, null, "NotADecoration"))
            .ThrowsExactly<FormatException>();
    }

    [Test]
    public async Task TypeConverter_ConvertTo_ProducesCommaSeparatedString()
    {
        var converter = new TextDecorationsTypeConverter();
        var result = (string)converter.ConvertTo(null, null, TextDecorations.Bold | TextDecorations.Underline, typeof(string))!;

        await Assert.That(result).IsEqualTo("Bold,Underline");
    }

    [Test]
    public async Task TypeConverter_ConvertTo_NoneProducesNoneString()
    {
        var converter = new TextDecorationsTypeConverter();
        var result = (string)converter.ConvertTo(null, null, TextDecorations.None, typeof(string))!;

        await Assert.That(result).IsEqualTo("None");
    }

    // ─── CellBuffer SetChar with Decorations ────────────────────────

    [Test]
    public async Task CellBuffer_SetChar_WithDecorations_StoresCorrectCell()
    {
        var buffer = new CellBuffer(10, 5);
        buffer.SetChar(3, 2, 'X', Color.Red, Color.Blue, TextDecorations.Bold);

        var cell = buffer.GetCell(3, 2);
        await Assert.That(cell.Character).IsEqualTo('X');
        await Assert.That(cell.Foreground).IsEqualTo(Color.Red);
        await Assert.That(cell.Background).IsEqualTo(Color.Blue);
        await Assert.That(cell.Decorations).IsEqualTo(TextDecorations.Bold);
    }

    [Test]
    public async Task CellBuffer_SetChar_WithDecorations_TransparentBgPreservesExisting()
    {
        var buffer = new CellBuffer(10, 5);
        // First set a cell with a specific background
        buffer.SetCell(3, 2, new Cell(' ', Color.White, Color.Green));
        // Now set with transparent bg and decorations
        buffer.SetChar(3, 2, 'Y', Color.Red, Color.Transparent, TextDecorations.Italic);

        var cell = buffer.GetCell(3, 2);
        await Assert.That(cell.Character).IsEqualTo('Y');
        await Assert.That(cell.Background).IsEqualTo(Color.Green); // Preserved
        await Assert.That(cell.Decorations).IsEqualTo(TextDecorations.Italic);
    }
}
