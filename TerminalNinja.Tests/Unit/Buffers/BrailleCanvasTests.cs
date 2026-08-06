namespace TerminalNinja.Tests.Unit.Buffers;

/// <summary>
/// Tests for <see cref="BrailleCanvas"/> — the 2×4 dot drawing surface used by charts.
/// </summary>
public class BrailleCanvasTests
{
    private const uint BrailleBase = 0x2800;

    [Test]
    public async Task Dimensions_AreFourAndTwoDotsPerCell()
    {
        var canvas = new BrailleCanvas(10, 5);

        await Assert.That(canvas.PixelWidth).IsEqualTo(20);
        await Assert.That(canvas.PixelHeight).IsEqualTo(20);
    }

    [Test]
    public async Task Plot_TopLeftDot_SetsBit1()
    {
        var canvas = new BrailleCanvas(1, 1);
        canvas.Plot(0, 0);

        using var buffer = new CellBuffer(1, 1);
        canvas.Blit(buffer, 0, 0, Color.White);

        // Dot 1 (top-left) has bit value 0x01.
        await Assert.That(buffer.GetCell(0, 0).Codepoint).IsEqualTo(BrailleBase + 0x01);
    }

    [Test]
    public async Task Plot_BottomRightDot_SetsBit8()
    {
        var canvas = new BrailleCanvas(1, 1);
        canvas.Plot(1, 3);

        using var buffer = new CellBuffer(1, 1);
        canvas.Blit(buffer, 0, 0, Color.White);

        // Dot 8 (bottom-right) has bit value 0x80.
        await Assert.That(buffer.GetCell(0, 0).Codepoint).IsEqualTo(BrailleBase + 0x80);
    }

    [Test]
    public async Task Plot_AllEightDots_ProducesFullBrailleCell()
    {
        var canvas = new BrailleCanvas(1, 1);
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 2; x++)
            {
                canvas.Plot(x, y);
            }
        }

        using var buffer = new CellBuffer(1, 1);
        canvas.Blit(buffer, 0, 0, Color.White);

        // All eight dots set → U+28FF.
        await Assert.That(buffer.GetCell(0, 0).Codepoint).IsEqualTo(BrailleBase + 0xFF);
    }

    [Test]
    public async Task Plot_OutOfRange_IsIgnored()
    {
        var canvas = new BrailleCanvas(1, 1);
        canvas.Plot(-1, 0);
        canvas.Plot(0, -1);
        canvas.Plot(2, 0);
        canvas.Plot(0, 4);

        using var buffer = new CellBuffer(1, 1);
        canvas.Blit(buffer, 0, 0, Color.White);

        // Nothing plotted in range → empty cells are not written (stays default space).
        await Assert.That(buffer.GetCell(0, 0).Codepoint).IsNotEqualTo(BrailleBase);
    }

    [Test]
    public async Task Line_Horizontal_SetsDotsAcrossRow()
    {
        var canvas = new BrailleCanvas(4, 1); // 8 dots wide
        canvas.Line(0, 0, 7, 0);

        using var buffer = new CellBuffer(4, 1);
        canvas.Blit(buffer, 0, 0, Color.White);

        // Every cell along the row should carry the top-row dots (bits 0x01 | 0x08 = 0x09).
        for (var x = 0; x < 4; x++)
        {
            await Assert.That(buffer.GetCell(x, 0).Codepoint).IsEqualTo(BrailleBase + 0x09);
        }
    }

    [Test]
    public async Task Blit_EmptyCanvas_LeavesBufferUntouched()
    {
        var canvas = new BrailleCanvas(3, 3);
        using var buffer = new CellBuffer(3, 3);

        canvas.Blit(buffer, 0, 0, Color.White);

        // No dots plotted → no braille glyphs written.
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                await Assert.That(buffer.GetCell(x, y).Codepoint).IsNotEqualTo(BrailleBase);
            }
        }
    }

    [Test]
    public async Task Blit_UsesForegroundColorAndPreservesExistingBackground()
    {
        var canvas = new BrailleCanvas(1, 1);
        canvas.Plot(0, 0);

        using var buffer = new CellBuffer(1, 1);
        // Paint a known background first; the braille dot should overlay it without
        // replacing it (BrailleCanvas blits with a transparent background).
        var underlying = new Color(50, 60, 70);
        buffer.SetChar(0, 0, ' ', Color.White, underlying);
        canvas.Blit(buffer, 0, 0, new Color(10, 200, 30));

        var cell = buffer.GetCell(0, 0);
        await Assert.That(cell.Foreground).IsEqualTo(new Color(10, 200, 30));
        await Assert.That(cell.Background).IsEqualTo(underlying);
    }

    [Test]
    public async Task Blit_Origin_OffsetsIntoBuffer()
    {
        var canvas = new BrailleCanvas(1, 1);
        canvas.Plot(0, 0);

        using var buffer = new CellBuffer(5, 5);
        canvas.Blit(buffer, 2, 3, Color.White);

        await Assert.That(buffer.GetCell(2, 3).Codepoint).IsEqualTo(BrailleBase + 0x01);
        await Assert.That(buffer.GetCell(0, 0).Codepoint).IsNotEqualTo(BrailleBase + 0x01);
    }
}
