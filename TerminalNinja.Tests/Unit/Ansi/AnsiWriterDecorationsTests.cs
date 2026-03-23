using TerminalNinja.Ansi;

namespace TerminalNinja.Tests.Unit.Ansi;

public class AnsiWriterDecorationsTests
{
    // ─── SetDecorations Tests ───────────────────────────────────────

    [Test]
    public async Task SetDecorations_Bold_EmitsBoldOnCode()
    {
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.SetDecorations(TextDecorations.Bold));
        await Assert.That(output).IsEqualTo("\e[1m");
    }

    [Test]
    public async Task SetDecorations_Italic_EmitsItalicOnCode()
    {
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.SetDecorations(TextDecorations.Italic));
        await Assert.That(output).IsEqualTo("\e[3m");
    }

    [Test]
    public async Task SetDecorations_Underline_EmitsUnderlineOnCode()
    {
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.SetDecorations(TextDecorations.Underline));
        await Assert.That(output).IsEqualTo("\e[4m");
    }

    [Test]
    public async Task SetDecorations_Strikethrough_EmitsStrikethroughOnCode()
    {
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.SetDecorations(TextDecorations.Strikethrough));
        await Assert.That(output).IsEqualTo("\e[9m");
    }

    [Test]
    public async Task SetDecorations_Dim_EmitsDimOnCode()
    {
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.SetDecorations(TextDecorations.Dim));
        await Assert.That(output).IsEqualTo("\e[2m");
    }

    [Test]
    public async Task SetDecorations_Blink_EmitsBlinkOnCode()
    {
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.SetDecorations(TextDecorations.Blink));
        await Assert.That(output).IsEqualTo("\e[5m");
    }

    [Test]
    public async Task SetDecorations_Inverse_EmitsInverseOnCode()
    {
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.SetDecorations(TextDecorations.Inverse));
        await Assert.That(output).IsEqualTo("\e[7m");
    }

    [Test]
    public async Task SetDecorations_BoldAndItalic_EmitsBothOnCodes()
    {
        var output = AnsiTestHelpers.CaptureAnsiOutput(w =>
            w.SetDecorations(TextDecorations.Bold | TextDecorations.Italic));

        await Assert.That(output).Contains("\e[1m");
        await Assert.That(output).Contains("\e[3m");
    }

    [Test]
    public async Task SetDecorations_None_AfterBold_EmitsOff()
    {
        var output = AnsiTestHelpers.CaptureAnsiOutput(w =>
        {
            w.SetDecorations(TextDecorations.Bold);
            w.SetDecorations(TextDecorations.None);
        });

        // Should contain bold on, then bold off (\e[22m)
        await Assert.That(output).Contains("\e[1m");
        await Assert.That(output).Contains("\e[22m");
    }

    [Test]
    public async Task SetDecorations_None_AfterItalic_EmitsItalicOff()
    {
        var output = AnsiTestHelpers.CaptureAnsiOutput(w =>
        {
            w.SetDecorations(TextDecorations.Italic);
            w.SetDecorations(TextDecorations.None);
        });

        await Assert.That(output).Contains("\e[3m");
        await Assert.That(output).Contains("\e[23m");
    }

    [Test]
    public async Task SetDecorations_SameValueTwice_OnlyEmitsOnce()
    {
        var output = AnsiTestHelpers.CaptureAnsiOutput(w =>
        {
            w.SetDecorations(TextDecorations.Bold);
            w.SetDecorations(TextDecorations.Bold); // Should be skipped
        });

        // Only one \e[1m
        await Assert.That(output).IsEqualTo("\e[1m");
    }

    [Test]
    public async Task SetDecorations_ChangeBoldToDim_EmitsResetAndDim()
    {
        // Bold and Dim share the same "off" code (\e[22m)
        var output = AnsiTestHelpers.CaptureAnsiOutput(w =>
        {
            w.SetDecorations(TextDecorations.Bold);
            w.SetDecorations(TextDecorations.Dim);
        });

        // Should emit: \e[1m (bold on), then \e[22m (both off), then \e[2m (dim on)
        await Assert.That(output).Contains("\e[1m");
        await Assert.That(output).Contains("\e[22m");
        await Assert.That(output).Contains("\e[2m");
    }

    [Test]
    public async Task SetDecorations_None_AfterUnderline_EmitsUnderlineOff()
    {
        var output = AnsiTestHelpers.CaptureAnsiOutput(w =>
        {
            w.SetDecorations(TextDecorations.Underline);
            w.SetDecorations(TextDecorations.None);
        });

        await Assert.That(output).Contains("\e[4m");
        await Assert.That(output).Contains("\e[24m");
    }

    // ─── WriteCell with Decorations ─────────────────────────────────

    [Test]
    public async Task WriteCell_WithDecorations_EmitsDecorationCodes()
    {
        var cell = new Cell('A', Color.White, Color.Black, TextDecorations.Bold);
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.WriteCell(0, 0, cell));

        await Assert.That(output).Contains("\e[1m"); // Bold on
        await Assert.That(output).Contains("A");     // Character
    }

    [Test]
    public async Task WriteCell_NoDecorations_DoesNotEmitDecorationCodes()
    {
        var cell = new Cell('A', Color.White, Color.Black);
        var output = AnsiTestHelpers.CaptureAnsiOutput(w => w.WriteCell(0, 0, cell));

        // Should not contain any SGR decoration codes (only color codes)
        await Assert.That(output).DoesNotContain("\e[1m");
        await Assert.That(output).DoesNotContain("\e[3m");
        await Assert.That(output).DoesNotContain("\e[4m");
    }

    [Test]
    public async Task WriteCell_DecorationsChangesBetweenCells_EmitsCorrectCodes()
    {
        var boldCell = new Cell('B', Color.White, Color.Black, TextDecorations.Bold);
        var italicCell = new Cell('I', Color.White, Color.Black, TextDecorations.Italic);

        var output = AnsiTestHelpers.CaptureAnsiOutput(w =>
        {
            w.WriteCell(0, 0, boldCell);
            w.WriteCell(1, 0, italicCell);
        });

        // Should contain: bold on, then bold off + italic on
        await Assert.That(output).Contains("\e[1m");
        await Assert.That(output).Contains("\e[22m"); // Bold off
        await Assert.That(output).Contains("\e[3m");  // Italic on
    }

    // ─── AnsiStyle Tests ────────────────────────────────────────────

    [Test]
    public async Task AnsiStyle_NeedsDecorations_TrueWhenNotSet()
    {
        var style = new AnsiStyle();
        await Assert.That(style.NeedsDecorations(TextDecorations.Bold)).IsTrue();
    }

    [Test]
    public async Task AnsiStyle_NeedsDecorations_FalseWhenSameValue()
    {
        var style = new AnsiStyle { Decorations = TextDecorations.Bold, DecorationsSet = true };
        await Assert.That(style.NeedsDecorations(TextDecorations.Bold)).IsFalse();
    }

    [Test]
    public async Task AnsiStyle_NeedsDecorations_TrueWhenDifferentValue()
    {
        var style = new AnsiStyle { Decorations = TextDecorations.Bold, DecorationsSet = true };
        await Assert.That(style.NeedsDecorations(TextDecorations.Italic)).IsTrue();
    }

    [Test]
    public async Task AnsiStyle_Reset_ClearsDecorations()
    {
        var style = new AnsiStyle { Decorations = TextDecorations.Bold, DecorationsSet = true };
        style.Reset();

        await Assert.That(style.DecorationsSet).IsFalse();
        await Assert.That(style.Decorations).IsEqualTo(TextDecorations.None);
    }
}
