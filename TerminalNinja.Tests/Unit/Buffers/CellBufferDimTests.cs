namespace TerminalNinja.Tests.Unit.Buffers;

/// <summary>
/// Tests for CellBuffer.DimRect and DimAll background dimming.
/// </summary>
public class CellBufferDimTests
{
    private CellBuffer _buffer = null!;
    private const int W = 20;
    private const int H = 10;

    [Before(Test)]
    public Task Setup()
    {
        _buffer = new CellBuffer(W, H);
        return Task.CompletedTask;
    }

    [After(Test)]
    public Task Cleanup()
    {
        _buffer.Dispose();
        return Task.CompletedTask;
    }

    [Test]
    public async Task DimAll_HalvesRgbValues()
    {
        // Arrange — fill a cell with a known color
        var original = new Cell('A', new Color(200, 100, 50), new Color(100, 80, 60));
        _buffer.SetCell(0, 0, original);

        // Act
        _buffer.DimAll();

        // Assert — each channel should be halved (bit shift right 1)
        var dimmed = _buffer.GetCell(0, 0);
        await Assert.That(dimmed.Character).IsEqualTo('A');
        await Assert.That(dimmed.Foreground.R).IsEqualTo((byte)100);
        await Assert.That(dimmed.Foreground.G).IsEqualTo((byte)50);
        await Assert.That(dimmed.Foreground.B).IsEqualTo((byte)25);
        await Assert.That(dimmed.Background.R).IsEqualTo((byte)50);
        await Assert.That(dimmed.Background.G).IsEqualTo((byte)40);
        await Assert.That(dimmed.Background.B).IsEqualTo((byte)30);
    }

    [Test]
    public async Task DimRect_OnlyAffectsBoundedRegion()
    {
        // Arrange — fill entire buffer with white-on-red
        var cell = new Cell('X', Color.White, Color.Red);
        _buffer.FillRect(new Rect(0, 0, W, H), cell);

        // Act — dim only a 5x3 region
        _buffer.DimRect(new Rect(2, 1, 5, 3));

        // Assert — inside the dim region
        var dimmed = _buffer.GetCell(2, 1);
        await Assert.That(dimmed.Foreground.R).IsEqualTo((byte)127); // 255 >> 1
        await Assert.That(dimmed.Background.R).IsEqualTo((byte)127);

        // Assert — outside the dim region (untouched)
        var untouched = _buffer.GetCell(0, 0);
        await Assert.That(untouched.Foreground).IsEqualTo(Color.White);
        await Assert.That(untouched.Background).IsEqualTo(Color.Red);
    }

    [Test]
    public async Task DimRect_ClipsToBufferBounds()
    {
        // Arrange
        var cell = new Cell('Z', Color.White, Color.White);
        _buffer.SetCell(W - 1, H - 1, cell);

        // Act — rect extends beyond buffer
        _buffer.DimRect(new Rect(W - 2, H - 2, 100, 100));

        // Assert — last cell should be dimmed, no crash
        var dimmed = _buffer.GetCell(W - 1, H - 1);
        await Assert.That(dimmed.Foreground.R).IsEqualTo((byte)127);
    }

    [Test]
    public async Task DimRect_EmptyRegion_DoesNothing()
    {
        // Arrange
        var cell = new Cell('A', Color.White, Color.Red);
        _buffer.SetCell(0, 0, cell);

        // Act — zero-area rect
        _buffer.DimRect(new Rect(0, 0, 0, 0));

        // Assert — cell unchanged
        var unchanged = _buffer.GetCell(0, 0);
        await Assert.That(unchanged.Foreground).IsEqualTo(Color.White);
        await Assert.That(unchanged.Background).IsEqualTo(Color.Red);
    }

    [Test]
    public async Task DimAll_PreservesTransparentColors()
    {
        // Arrange
        var cell = new Cell('T', Color.Transparent, Color.Red);
        _buffer.SetCell(5, 5, cell);

        // Act
        _buffer.DimAll();

        // Assert — transparent foreground should remain transparent
        var dimmed = _buffer.GetCell(5, 5);
        await Assert.That(dimmed.Foreground).IsEqualTo(Color.Transparent);
        // Background should be dimmed
        await Assert.That(dimmed.Background.R).IsEqualTo((byte)127);
    }

    [Test]
    public async Task DimAll_PreservesTextDecorations()
    {
        // Arrange
        var cell = new Cell('B', Color.White, Color.Blue, TextDecorations.Bold | TextDecorations.Underline);
        _buffer.SetCell(3, 3, cell);

        // Act
        _buffer.DimAll();

        // Assert
        var dimmed = _buffer.GetCell(3, 3);
        await Assert.That(dimmed.Decorations).IsEqualTo(TextDecorations.Bold | TextDecorations.Underline);
    }
}
