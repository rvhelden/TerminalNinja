namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Comprehensive tests for Rectangle control covering:
/// - Bounds calculation with absolute, percent, and stretch sizing
/// - Horizontal and vertical alignment (Start, Center, End)
/// - Position offsets with different alignments
/// - Border rendering (Single, Double, Rounded, Ascii)
/// - Background fill
/// - Clipping to buffer bounds
/// - Nested parent bounds
/// </summary>
public class RectangleTests
{
    private CellBuffer _buffer = null!;
    private const int BufferWidth = 80;
    private const int BufferHeight = 24;

    [Before(Test)]
    public Task Setup()
    {
        _buffer = new CellBuffer(BufferWidth, BufferHeight);
        return Task.CompletedTask;
    }

    #region CalculateBounds - Basic

    [Test]
    public async Task CalculateBounds_DefaultRectangle_FillsParent()
    {
        var rect = new Rectangle(); // All defaults: Stretch width/height
        var parent = new Rect(0, 0, 100, 50);

        var bounds = rect.CalculateBounds(parent);

        await Assert.That(bounds.X).IsEqualTo(0);
        await Assert.That(bounds.Y).IsEqualTo(0);
        await Assert.That(bounds.Width).IsEqualTo(100);
        await Assert.That(bounds.Height).IsEqualTo(50);
    }

    [Test]
    public async Task CalculateBounds_AbsoluteSize_UsesExactDimensions()
    {
        var rect = new Rectangle
        {
            Width = Size.Absolute(30),
            Height = Size.Absolute(10)
        };
        var parent = new Rect(0, 0, 100, 50);

        var bounds = rect.CalculateBounds(parent);

        await Assert.That(bounds.Width).IsEqualTo(30);
        await Assert.That(bounds.Height).IsEqualTo(10);
    }

    #endregion

    #region CalculateBounds - Alignment (Data-Driven)

    [Test]
    [MethodDataSource(nameof(GetHorizontalAlignmentCases))]
    public async Task CalculateBounds_HorizontalAlignment_PositionsCorrectly(
        Alignment alignment, int expectedX)
    {
        var rect = new Rectangle
        {
            Width = Size.Absolute(20),
            Height = Size.Absolute(10),
            HorizontalAlignment = alignment
        };
        var parent = new Rect(0, 0, 100, 50);

        var bounds = rect.CalculateBounds(parent);

        await Assert.That(bounds.X).IsEqualTo(expectedX);
    }

    public static IEnumerable<(Alignment, int)> GetHorizontalAlignmentCases()
    {
        yield return (Alignment.Start, 0);    // Left edge
        yield return (Alignment.Center, 40);  // (100-20)/2 = 40
        yield return (Alignment.End, 80);     // 100-20 = 80
    }

    [Test]
    [MethodDataSource(nameof(GetVerticalAlignmentCases))]
    public async Task CalculateBounds_VerticalAlignment_PositionsCorrectly(
        Alignment alignment, int expectedY)
    {
        var rect = new Rectangle
        {
            Width = Size.Absolute(20),
            Height = Size.Absolute(10),
            VerticalAlignment = alignment
        };
        var parent = new Rect(0, 0, 100, 50);

        var bounds = rect.CalculateBounds(parent);

        await Assert.That(bounds.Y).IsEqualTo(expectedY);
    }

    public static IEnumerable<(Alignment, int)> GetVerticalAlignmentCases()
    {
        yield return (Alignment.Start, 0);    // Top edge
        yield return (Alignment.Center, 20);  // (50-10)/2 = 20
        yield return (Alignment.End, 40);     // 50-10 = 40
    }

    [Test]
    [MethodDataSource(nameof(GetCombinedAlignmentCases))]
    public async Task CalculateBounds_CombinedAlignment_PositionsCorrectly(
        Alignment horizontal, Alignment vertical, int expectedX, int expectedY)
    {
        var rect = new Rectangle
        {
            Width = Size.Absolute(20),
            Height = Size.Absolute(10),
            HorizontalAlignment = horizontal,
            VerticalAlignment = vertical
        };
        var parent = new Rect(0, 0, 100, 50);

        var bounds = rect.CalculateBounds(parent);

        await Assert.That(bounds.X).IsEqualTo(expectedX);
        await Assert.That(bounds.Y).IsEqualTo(expectedY);
    }

    public static IEnumerable<(Alignment, Alignment, int, int)> GetCombinedAlignmentCases()
    {
        // All 9 combinations
        yield return (Alignment.Start, Alignment.Start, 0, 0);
        yield return (Alignment.Start, Alignment.Center, 0, 20);
        yield return (Alignment.Start, Alignment.End, 0, 40);
        yield return (Alignment.Center, Alignment.Start, 40, 0);
        yield return (Alignment.Center, Alignment.Center, 40, 20);
        yield return (Alignment.Center, Alignment.End, 40, 40);
        yield return (Alignment.End, Alignment.Start, 80, 0);
        yield return (Alignment.End, Alignment.Center, 80, 20);
        yield return (Alignment.End, Alignment.End, 80, 40);
    }

    #endregion

    #region CalculateBounds - Offset with Alignment

    [Test]
    public async Task CalculateBounds_OffsetWithStartAlignment_AddsOffset()
    {
        var rect = new Rectangle
        {
            X = Size.Absolute(10),
            Y = Size.Absolute(5),
            Width = Size.Absolute(20),
            Height = Size.Absolute(10),
            HorizontalAlignment = Alignment.Start,
            VerticalAlignment = Alignment.Start
        };
        var parent = new Rect(0, 0, 100, 50);

        var bounds = rect.CalculateBounds(parent);

        await Assert.That(bounds.X).IsEqualTo(10);
        await Assert.That(bounds.Y).IsEqualTo(5);
    }

    [Test]
    public async Task CalculateBounds_OffsetWithCenterAlignment_AddsOffsetToCenteredPosition()
    {
        var rect = new Rectangle
        {
            X = Size.Absolute(5),
            Y = Size.Absolute(3),
            Width = Size.Absolute(20),
            Height = Size.Absolute(10),
            HorizontalAlignment = Alignment.Center,
            VerticalAlignment = Alignment.Center
        };
        var parent = new Rect(0, 0, 100, 50);

        var bounds = rect.CalculateBounds(parent);

        // Center alignment: (100-20)/2 + 5 = 45
        await Assert.That(bounds.X).IsEqualTo(45);
        // Center alignment: (50-10)/2 + 3 = 23
        await Assert.That(bounds.Y).IsEqualTo(23);
    }

    [Test]
    public async Task CalculateBounds_OffsetWithEndAlignment_SubtractsOffset()
    {
        var rect = new Rectangle
        {
            X = Size.Absolute(10),
            Y = Size.Absolute(5),
            Width = Size.Absolute(20),
            Height = Size.Absolute(10),
            HorizontalAlignment = Alignment.End,
            VerticalAlignment = Alignment.End
        };
        var parent = new Rect(0, 0, 100, 50);

        var bounds = rect.CalculateBounds(parent);

        // End alignment: parent.Width - width - offset = 100 - 20 - 10 = 70
        await Assert.That(bounds.X).IsEqualTo(70);
        // End alignment: parent.Height - height - offset = 50 - 10 - 5 = 35
        await Assert.That(bounds.Y).IsEqualTo(35);
    }

    #endregion

    #region CalculateBounds - Size Modes (Data-Driven)

    [Test]
    [MethodDataSource(nameof(GetSizeModeCases))]
    public async Task CalculateBounds_SizeModes_ResolvesCorrectly(
        Size width, Size height, int expectedWidth, int expectedHeight)
    {
        var rect = new Rectangle
        {
            Width = width,
            Height = height
        };
        var parent = new Rect(0, 0, 100, 50);

        var bounds = rect.CalculateBounds(parent);

        await Assert.That(bounds.Width).IsEqualTo(expectedWidth);
        await Assert.That(bounds.Height).IsEqualTo(expectedHeight);
    }

    public static IEnumerable<(Size, Size, int, int)> GetSizeModeCases()
    {
        yield return (Size.Absolute(30), Size.Absolute(15), 30, 15);
        yield return (Size.Percent(50), Size.Percent(50), 50, 25);
        yield return (Size.Stretch, Size.Stretch, 100, 50);
        yield return (Size.Percent(100), Size.Stretch, 100, 50);
        yield return (Size.Absolute(30), Size.Percent(50), 30, 25);
        yield return (Size.Percent(25), Size.Absolute(10), 25, 10);
        yield return (Size.Percent(75), Size.Percent(80), 75, 40);
    }

    #endregion

    #region Render - Background Fill

    [Test]
    public async Task Render_FillsBackgroundWithColor()
    {
        var rect = new Rectangle
        {
            Width = Size.Absolute(5),
            Height = Size.Absolute(3),
            BackgroundColor = Color.Blue
        };
        var parent = new Rect(0, 0, 10, 10);

        rect.Render(_buffer, parent);

        // Check center cell has correct background
        var cell = _buffer.GetCell(2, 1);
        await Assert.That(cell.Background).IsEqualTo(Color.Blue);
        await Assert.That(cell.Character).IsEqualTo(' ');
    }

    [Test]
    public async Task Render_FillsEntireRectangleWithBackground()
    {
        var rect = new Rectangle
        {
            Width = Size.Absolute(4),
            Height = Size.Absolute(4),
            BackgroundColor = Color.Red
        };

        rect.Render(_buffer, new Rect(0, 0, BufferWidth, BufferHeight));

        // Check all corners
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(3, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(0, 3).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(3, 3).Background).IsEqualTo(Color.Red);
        
        // Check outside is untouched
        await Assert.That(_buffer.GetCell(4, 0).Background).IsEqualTo(Color.Black);
    }

    #endregion

    #region Render - Border

    [Test]
    public async Task Render_SingleBorder_DrawsCorrectCorners()
    {
        var rect = new Rectangle
        {
            Width = Size.Absolute(10),
            Height = Size.Absolute(5),
            Border = Border.Single(Color.White)
        };

        rect.Render(_buffer, new Rect(0, 0, BufferWidth, BufferHeight));

        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('┌');
        await Assert.That(_buffer.GetCell(9, 0).Character).IsEqualTo('┐');
        await Assert.That(_buffer.GetCell(0, 4).Character).IsEqualTo('└');
        await Assert.That(_buffer.GetCell(9, 4).Character).IsEqualTo('┘');
    }

    [Test]
    public async Task Render_SingleBorder_DrawsCorrectEdges()
    {
        var rect = new Rectangle
        {
            Width = Size.Absolute(10),
            Height = Size.Absolute(5),
            Border = Border.Single(Color.White)
        };

        rect.Render(_buffer, new Rect(0, 0, BufferWidth, BufferHeight));

        // Top edge
        await Assert.That(_buffer.GetCell(5, 0).Character).IsEqualTo('─');
        // Bottom edge
        await Assert.That(_buffer.GetCell(5, 4).Character).IsEqualTo('─');
        // Left edge
        await Assert.That(_buffer.GetCell(0, 2).Character).IsEqualTo('│');
        // Right edge
        await Assert.That(_buffer.GetCell(9, 2).Character).IsEqualTo('│');
    }

    [Test]
    [MethodDataSource(nameof(GetBorderStyleCases))]
    public async Task Render_BorderStyle_DrawsCorrectTopLeftCorner(
        Border border, char expectedCorner)
    {
        var rect = new Rectangle
        {
            Width = Size.Absolute(10),
            Height = Size.Absolute(5),
            Border = border
        };

        rect.Render(_buffer, new Rect(0, 0, BufferWidth, BufferHeight));

        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo(expectedCorner);
    }

    public static IEnumerable<(Border, char)> GetBorderStyleCases()
    {
        yield return (Border.Single(Color.White), '┌');
        yield return (Border.Double(Color.White), '╔');
        yield return (Border.Rounded(Color.White), '╭');
        yield return (Border.Ascii(Color.White), '+');
    }

    [Test]
    public async Task Render_NoBorder_DoesNotDrawBorderCharacters()
    {
        var rect = new Rectangle
        {
            Width = Size.Absolute(10),
            Height = Size.Absolute(5),
            Border = Border.None,
            BackgroundColor = Color.Blue
        };

        rect.Render(_buffer, new Rect(0, 0, BufferWidth, BufferHeight));

        // Corner should be a space (background fill), not a border character
        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo(' ');
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Blue);
    }

    [Test]
    public async Task Render_Border_UsesBorderColor()
    {
        var rect = new Rectangle
        {
            Width = Size.Absolute(10),
            Height = Size.Absolute(5),
            Border = Border.Single(Color.Cyan),
            BackgroundColor = Color.Black
        };

        rect.Render(_buffer, new Rect(0, 0, BufferWidth, BufferHeight));

        var cornerCell = _buffer.GetCell(0, 0);
        await Assert.That(cornerCell.Foreground).IsEqualTo(Color.Cyan);
        await Assert.That(cornerCell.Background).IsEqualTo(Color.Black);
    }

    #endregion

    #region Render - Clipping

    [Test]
    public async Task Render_PartiallyOutOfBounds_ClipsToBuffer()
    {
        var rect = new Rectangle
        {
            X = Size.Absolute(-5),
            Width = Size.Absolute(20),
            Height = Size.Absolute(10),
            BackgroundColor = Color.Red
        };

        rect.Render(_buffer, new Rect(0, 0, BufferWidth, BufferHeight));

        // Cell at x=0 should have the rectangle's background (clipped portion)
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        
        // Cell at x=14 should have the rectangle's background
        await Assert.That(_buffer.GetCell(14, 5).Background).IsEqualTo(Color.Red);
        
        // Cell beyond the rectangle should be empty
        await Assert.That(_buffer.GetCell(15, 5).Background).IsEqualTo(Color.Black);
    }

    [Test]
    public async Task Render_CompletelyOutOfBounds_DoesNotRender()
    {
        var rect = new Rectangle
        {
            X = Size.Absolute(100), // Beyond buffer
            Width = Size.Absolute(20),
            Height = Size.Absolute(10),
            BackgroundColor = Color.Red
        };

        rect.Render(_buffer, new Rect(0, 0, BufferWidth, BufferHeight));

        // No cells should be modified
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Black);
        await Assert.That(_buffer.GetCell(BufferWidth - 1, 0).Background).IsEqualTo(Color.Black);
    }

    [Test]
    public async Task Render_MinimumSizeForBorder_DrawsBorderCorrectly()
    {
        // Minimum 2x2 for border
        var rect = new Rectangle
        {
            Width = Size.Absolute(2),
            Height = Size.Absolute(2),
            Border = Border.Single(Color.White)
        };

        rect.Render(_buffer, new Rect(0, 0, BufferWidth, BufferHeight));

        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('┌');
        await Assert.That(_buffer.GetCell(1, 0).Character).IsEqualTo('┐');
        await Assert.That(_buffer.GetCell(0, 1).Character).IsEqualTo('└');
        await Assert.That(_buffer.GetCell(1, 1).Character).IsEqualTo('┘');
    }

    [Test]
    public async Task Render_TooSmallForBorder_SkipsBorder()
    {
        // 1x1 is too small for border
        var rect = new Rectangle
        {
            Width = Size.Absolute(1),
            Height = Size.Absolute(1),
            Border = Border.Single(Color.White),
            BackgroundColor = Color.Blue
        };

        rect.Render(_buffer, new Rect(0, 0, BufferWidth, BufferHeight));

        // Should just fill background, no border
        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo(' ');
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Blue);
    }

    #endregion

    #region Render - Nested Parent Bounds

    [Test]
    public async Task Render_WithParentOffset_PositionsRelativeToParent()
    {
        var rect = new Rectangle
        {
            Width = Size.Absolute(10),
            Height = Size.Absolute(5),
            BackgroundColor = Color.Green
        };
        var parent = new Rect(20, 10, 40, 20); // Parent at offset

        rect.Render(_buffer, parent);

        // Rectangle should start at parent's position
        await Assert.That(_buffer.GetCell(20, 10).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(29, 14).Background).IsEqualTo(Color.Green);
        
        // Outside parent's area should be untouched
        await Assert.That(_buffer.GetCell(19, 10).Background).IsEqualTo(Color.Black);
        await Assert.That(_buffer.GetCell(30, 10).Background).IsEqualTo(Color.Black);
    }

    [Test]
    public async Task Render_PercentSizeWithinNestedParent_CalculatesFromParent()
    {
        var rect = new Rectangle
        {
            Width = Size.Percent(50),  // 50% of parent (40) = 20
            Height = Size.Percent(50), // 50% of parent (20) = 10
            BackgroundColor = Color.Yellow
        };
        var parent = new Rect(10, 5, 40, 20);

        rect.Render(_buffer, parent);

        // Check that it's 20 wide (50% of 40)
        await Assert.That(_buffer.GetCell(10, 5).Background).IsEqualTo(Color.Yellow);
        await Assert.That(_buffer.GetCell(29, 5).Background).IsEqualTo(Color.Yellow);
        await Assert.That(_buffer.GetCell(30, 5).Background).IsEqualTo(Color.Black);
    }

    #endregion

    #region Render - Integration Tests

    [Test]
    public async Task Render_CenteredRectangleWithBorder_RendersCorrectly()
    {
        var rect = new Rectangle
        {
            Width = Size.Absolute(20),
            Height = Size.Absolute(10),
            HorizontalAlignment = Alignment.Center,
            VerticalAlignment = Alignment.Center,
            Border = Border.Single(Color.White),
            BackgroundColor = Color.Blue
        };
        var parent = new Rect(0, 0, 100, 50);

        var bounds = rect.CalculateBounds(parent);
        
        // Verify positioning
        await Assert.That(bounds.X).IsEqualTo(40); // (100-20)/2
        await Assert.That(bounds.Y).IsEqualTo(20); // (50-10)/2
        
        // Render and verify
        rect.Render(_buffer, parent);
        
        await Assert.That(_buffer.GetCell(40, 20).Character).IsEqualTo('┌');
        await Assert.That(_buffer.GetCell(41, 21).Background).IsEqualTo(Color.Blue);
    }

    [Test]
    public async Task Render_StretchWithOffset_FillsRemainingSpace()
    {
        var rect = new Rectangle
        {
            X = Size.Absolute(10),
            Y = Size.Absolute(5),
            Width = Size.Stretch,
            Height = Size.Stretch,
            BackgroundColor = Color.Magenta
        };
        var parent = new Rect(0, 0, 50, 30);

        var bounds = rect.CalculateBounds(parent);
        
        // Should fill from offset to parent edge
        await Assert.That(bounds.X).IsEqualTo(10);
        await Assert.That(bounds.Y).IsEqualTo(5);
        await Assert.That(bounds.Width).IsEqualTo(50);  // Stretch fills entire parent width
        await Assert.That(bounds.Height).IsEqualTo(30); // Stretch fills entire parent height
    }

    #endregion

    #region Render - Child Element

    [Test]
    public async Task Render_WithNullChild_DoesNotCrash()
    {
        var rect = new Rectangle
        {
            Width = Size.Absolute(10),
            Height = Size.Absolute(5),
            BackgroundColor = Color.Blue,
            Child = null
        };

        rect.Render(_buffer, new Rect(0, 0, BufferWidth, BufferHeight));

        // Should render normally without child
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Blue);
    }

    [Test]
    public async Task Render_WithLabelChild_RendersTextInsideRectangle()
    {
        var label = new Label
        {
            Text = "Test",
            ForegroundColor = Color.White,
            BackgroundColor = Color.Blue
        };
        var rect = new Rectangle
        {
            Width = Size.Absolute(10),
            Height = Size.Absolute(5),
            BackgroundColor = Color.Blue,
            Child = label
        };

        rect.Render(_buffer, new Rect(0, 0, BufferWidth, BufferHeight));

        // Verify label text is rendered
        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('T');
        await Assert.That(_buffer.GetCell(1, 0).Character).IsEqualTo('e');
        await Assert.That(_buffer.GetCell(2, 0).Character).IsEqualTo('s');
        await Assert.That(_buffer.GetCell(3, 0).Character).IsEqualTo('t');
    }

    [Test]
    public async Task Render_ChildWithBorder_RendersInInnerBounds()
    {
        var label = new Label
        {
            Text = "X",
            ForegroundColor = Color.White,
            BackgroundColor = Color.Blue
        };
        var rect = new Rectangle
        {
            Width = Size.Absolute(5),
            Height = Size.Absolute(3),
            Border = Border.Single(Color.Cyan),
            BackgroundColor = Color.Blue,
            Child = label
        };

        rect.Render(_buffer, new Rect(0, 0, BufferWidth, BufferHeight));

        // Border characters should be at edges
        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('┌');
        await Assert.That(_buffer.GetCell(4, 0).Character).IsEqualTo('┐');
        
        // Label should render inside border (at position 1,1 instead of 0,0)
        await Assert.That(_buffer.GetCell(1, 1).Character).IsEqualTo('X');
        
        // Border edges should not be overwritten by label
        await Assert.That(_buffer.GetCell(0, 1).Character).IsEqualTo('│');
        await Assert.That(_buffer.GetCell(4, 1).Character).IsEqualTo('│');
    }

    [Test]
    public async Task Render_ChildWithoutBorder_UsesFullRectangleBounds()
    {
        var label = new Label
        {
            Text = "ABC",
            ForegroundColor = Color.White,
            BackgroundColor = Color.Red
        };
        var rect = new Rectangle
        {
            Width = Size.Absolute(5),
            Height = Size.Absolute(3),
            BackgroundColor = Color.Red,
            Border = Border.None,
            Child = label
        };

        rect.Render(_buffer, new Rect(0, 0, BufferWidth, BufferHeight));

        // Label should start at (0,0) - full bounds
        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('A');
        await Assert.That(_buffer.GetCell(1, 0).Character).IsEqualTo('B');
        await Assert.That(_buffer.GetCell(2, 0).Character).IsEqualTo('C');
    }

    [Test]
    public async Task Render_NestedRectangles_RendersCorrectly()
    {
        var innerLabel = new Label
        {
            Text = "Inner",
            ForegroundColor = Color.Yellow,
            BackgroundColor = Color.Red
        };
        var innerRect = new Rectangle
        {
            Width = Size.Absolute(7),
            Height = Size.Absolute(3),
            BackgroundColor = Color.Red,
            Border = Border.Single(Color.Yellow),
            Child = innerLabel
        };
        var outerRect = new Rectangle
        {
            Width = Size.Absolute(15),
            Height = Size.Absolute(7),
            BackgroundColor = Color.Blue,
            Border = Border.Double(Color.Cyan),
            Child = innerRect
        };

        outerRect.Render(_buffer, new Rect(0, 0, BufferWidth, BufferHeight));

        // Outer border (double)
        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('╔');
        await Assert.That(_buffer.GetCell(14, 0).Character).IsEqualTo('╗');
        
        // Inner rect starts at (1,1) because of outer border
        // Inner border (single) at (1,1)
        await Assert.That(_buffer.GetCell(1, 1).Character).IsEqualTo('┌');
        
        // Inner label text starts at (2,2) because of both borders
        await Assert.That(_buffer.GetCell(2, 2).Character).IsEqualTo('I');
        await Assert.That(_buffer.GetCell(3, 2).Character).IsEqualTo('n');
    }

    [Test]
    public async Task Render_ChildWithCenteredAlignment_RespectsBounds()
    {
        var label = new Label
        {
            Text = "Hi",
            ForegroundColor = Color.White,
            BackgroundColor = Color.Green,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };
        var rect = new Rectangle
        {
            Width = Size.Absolute(10),
            Height = Size.Absolute(5),
            BackgroundColor = Color.Green,
            Border = Border.Single(Color.White),
            Child = label
        };

        rect.Render(_buffer, new Rect(0, 0, BufferWidth, BufferHeight));

        // Border takes up edges, inner is 8x3
        // "Hi" (2 chars) centered in 8 width = (8-2)/2 + 1 (border offset) = 4
        // Centered in 3 height = (3-1)/2 + 1 (border offset) = 2
        await Assert.That(_buffer.GetCell(4, 2).Character).IsEqualTo('H');
        await Assert.That(_buffer.GetCell(5, 2).Character).IsEqualTo('i');
    }

    [Test]
    public async Task Render_ChildTooSmallForBorder_GetsEmptyBounds()
    {
        var label = new Label
        {
            Text = "X",
            ForegroundColor = Color.White
        };
        var rect = new Rectangle
        {
            Width = Size.Absolute(1),
            Height = Size.Absolute(1),
            Border = Border.Single(Color.White),
            Child = label
        };

        rect.Render(_buffer, new Rect(0, 0, BufferWidth, BufferHeight));

        // Rectangle is 1x1, which is too small for border (needs 2x2 minimum)
        // Border is skipped, background fills, but child would get 1x1 bounds
        // Label should still attempt to render in the 1x1 space
        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('X');
    }

    [Test]
    public async Task Render_ChildWithMinimumBorderSize_GetsZeroInnerBounds()
    {
        var label = new Label
        {
            Text = "Test",
            ForegroundColor = Color.White,
            BackgroundColor = Color.Blue
        };
        var rect = new Rectangle
        {
            Width = Size.Absolute(2),
            Height = Size.Absolute(2),
            Border = Border.Single(Color.White),
            BackgroundColor = Color.Blue,
            Child = label
        };

        rect.Render(_buffer, new Rect(0, 0, BufferWidth, BufferHeight));

        // 2x2 rectangle with border leaves 0x0 inner space
        // Border should render
        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('┌');
        await Assert.That(_buffer.GetCell(1, 1).Character).IsEqualTo('┘');
        
        // Child gets bounds of (1, 1, 0, 0) - should not render anything
        // No label text should be visible
    }

    #endregion

    #region Render - Stack Child Composition

    [Test]
    public async Task Render_WithStackChild_RendersAllStackChildren()
    {
        // Create a Rectangle with a horizontal StackPanel containing 3 Labels
        var labelA = new Label { Text = "A", ForegroundColor = Color.Red };
        var labelB = new Label { Text = "B", ForegroundColor = Color.Green };
        var labelC = new Label { Text = "C", ForegroundColor = Color.Blue };
        
        StackPanel.SetSizeMode(labelA, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(labelA, 5);
        StackPanel.SetSizeMode(labelB, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(labelB, 5);
        StackPanel.SetSizeMode(labelC, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(labelC, 5);
        
        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { labelA, labelB, labelC }
        };

        var rect = new Rectangle
        {
            Width = Size.Absolute(15),
            Height = Size.Absolute(3),
            BackgroundColor = Color.Black,
            Child = stackPanel
        };

        rect.Render(_buffer, new Rect(0, 0, BufferWidth, BufferHeight));

        // Verify all three labels are rendered
        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('A');
        await Assert.That(_buffer.GetCell(0, 0).Foreground).IsEqualTo(Color.Red);
        
        await Assert.That(_buffer.GetCell(5, 0).Character).IsEqualTo('B');
        await Assert.That(_buffer.GetCell(5, 0).Foreground).IsEqualTo(Color.Green);
        
        await Assert.That(_buffer.GetCell(10, 0).Character).IsEqualTo('C');
        await Assert.That(_buffer.GetCell(10, 0).Foreground).IsEqualTo(Color.Blue);
    }

    [Test]
    public async Task Render_StackChildAutoAndStretch_DistributesSpaceCorrectly()
    {
        // Create a toolbar-like pattern: Auto | Stretch | Auto
        var leftLabel = new Label { Text = "Left", BackgroundColor = Color.Red };
        var centerLabel = new Label { Text = "Center", BackgroundColor = Color.Green };
        var rightLabel = new Label { Text = "Right", BackgroundColor = Color.Blue };

        StackPanel.SetSizeMode(leftLabel, ChildSizeMode.Auto);
        StackPanel.SetSizeMode(centerLabel, ChildSizeMode.Stretch);
        StackPanel.SetSizeMode(rightLabel, ChildSizeMode.Auto);

        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { leftLabel, centerLabel, rightLabel }
        };

        var rect = new Rectangle
        {
            Width = Size.Absolute(20),
            Height = Size.Absolute(1),
            Child = stackPanel
        };

        rect.Render(_buffer, new Rect(0, 0, BufferWidth, BufferHeight));

        // Left label should be at x=0, width=4
        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('L');
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(3, 0).Background).IsEqualTo(Color.Red);
        
        // Center should start at x=4 and stretch to x=14 (width = 20 - 4 - 5 = 11)
        await Assert.That(_buffer.GetCell(4, 0).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(14, 0).Background).IsEqualTo(Color.Green);
        
        // Right label should be at x=15, width=5
        await Assert.That(_buffer.GetCell(15, 0).Character).IsEqualTo('R');
        await Assert.That(_buffer.GetCell(15, 0).Background).IsEqualTo(Color.Blue);
        await Assert.That(_buffer.GetCell(19, 0).Background).IsEqualTo(Color.Blue);
    }

    [Test]
    public async Task Render_NestedRectangleStackRectangles_RendersCorrectly()
    {
        // Create the full pattern: Rectangle → Stack → [Rectangle, Rectangle, Rectangle]
        var leftRect = new Rectangle
        {
            BackgroundColor = Color.Red,
            Border = Border.Single(Color.Red)
        };

        var centerRect = new Rectangle
        {
            BackgroundColor = Color.Green,
            Border = Border.Single(Color.Green)
        };

        var rightRect = new Rectangle
        {
            BackgroundColor = Color.Blue,
            Border = Border.Single(Color.Blue)
        };

        StackPanel.SetSizeMode(leftRect, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(leftRect, 5);
        StackPanel.SetSizeMode(centerRect, ChildSizeMode.Stretch);
        StackPanel.SetSizeMode(rightRect, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(rightRect, 5);

        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { leftRect, centerRect, rightRect }
        };

        var outerRect = new Rectangle
        {
            Width = Size.Absolute(20),
            Height = Size.Absolute(5),
            BackgroundColor = Color.Black,
            Border = Border.Double(Color.White),
            Child = stackPanel
        };

        outerRect.Render(_buffer, new Rect(0, 0, BufferWidth, BufferHeight));

        // Outer border should be double-line
        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('╔');
        await Assert.That(_buffer.GetCell(19, 0).Character).IsEqualTo('╗');

        // Left rectangle (inside outer border, at x=1, width=5)
        await Assert.That(_buffer.GetCell(1, 1).Character).IsEqualTo('┌');
        await Assert.That(_buffer.GetCell(1, 1).Foreground).IsEqualTo(Color.Red);

        // Center rectangle should start at x=6 (1 + 5) with stretch width
        await Assert.That(_buffer.GetCell(6, 1).Character).IsEqualTo('┌');
        await Assert.That(_buffer.GetCell(6, 1).Foreground).IsEqualTo(Color.Green);

        // Right rectangle should be at x=14 (20 - 1 - 5 = 14), width=5
        await Assert.That(_buffer.GetCell(14, 1).Character).IsEqualTo('┌');
        await Assert.That(_buffer.GetCell(14, 1).Foreground).IsEqualTo(Color.Blue);
    }

    [Test]
    public async Task Render_StackChildInsideBorder_RespectsInnerBounds()
    {
        // StackPanel should get inner bounds (after border subtraction)
        var label = new Label
        {
            Text = "Test",
            ForegroundColor = Color.White,
            BackgroundColor = Color.Blue
        };

        StackPanel.SetSizeMode(label, ChildSizeMode.Stretch);

        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { label }
        };

        var rect = new Rectangle
        {
            Width = Size.Absolute(10),
            Height = Size.Absolute(3),
            Border = Border.Single(Color.Cyan),
            BackgroundColor = Color.Blue,
            Child = stackPanel
        };

        rect.Render(_buffer, new Rect(0, 0, BufferWidth, BufferHeight));

        // Border should be at edges
        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('┌');
        await Assert.That(_buffer.GetCell(9, 0).Character).IsEqualTo('┐');

        // Label text should start at x=1, y=1 (inside border)
        await Assert.That(_buffer.GetCell(1, 1).Character).IsEqualTo('T');
        await Assert.That(_buffer.GetCell(2, 1).Character).IsEqualTo('e');
        await Assert.That(_buffer.GetCell(3, 1).Character).IsEqualTo('s');
        await Assert.That(_buffer.GetCell(4, 1).Character).IsEqualTo('t');

        // Border edges should not be overwritten
        await Assert.That(_buffer.GetCell(0, 1).Character).IsEqualTo('│');
        await Assert.That(_buffer.GetCell(9, 1).Character).IsEqualTo('│');
    }

    #endregion

    #region Dependency-Style Properties

    [Test]
    public async Task LayoutProperties_AfterConstruction_UpdatesBounds()
    {
        var rect = new Rectangle();
        var parent = new Rect(0, 0, 100, 50);

        var initial = rect.CalculateBounds(parent);
        await Assert.That(initial.Width).IsEqualTo(100);
        await Assert.That(initial.Height).IsEqualTo(50);

        rect.Width = Size.Absolute(20);
        rect.Height = Size.Absolute(10);
        rect.X = Size.Absolute(5);
        rect.Y = Size.Absolute(3);
        rect.HorizontalAlignment = Alignment.Center;
        rect.VerticalAlignment = Alignment.End;

        var updated = rect.CalculateBounds(parent);

        await Assert.That(updated.X).IsEqualTo(45); // (100 - 20) / 2 + 5
        await Assert.That(updated.Y).IsEqualTo(37); // 50 - 10 - 3
        await Assert.That(updated.Width).IsEqualTo(20);
        await Assert.That(updated.Height).IsEqualTo(10);
    }

    [Test]
    public async Task LayoutProperty_ChangedValue_TriggersInvalidationOnce()
    {
        var rect = new Rectangle();
        var invalidationCount = 0;
        rect.InvalidationCallback = () => invalidationCount++;

        rect.Width = Size.Absolute(12);
        rect.Width = Size.Absolute(12); // Same value should not invalidate again

        await Assert.That(invalidationCount).IsEqualTo(1);
    }

    [Test]
    public async Task Child_SetAndReplace_MaintainsParentReferences()
    {
        var rect = new Rectangle();
        var firstChild = new Label { Text = "First" };
        var secondChild = new Label { Text = "Second" };

        rect.Child = firstChild;
        await Assert.That(firstChild.Parent).IsEqualTo(rect);

        rect.Child = secondChild;

        await Assert.That(firstChild.Parent).IsNull();
        await Assert.That(secondChild.Parent).IsEqualTo(rect);

        rect.Child = null;
        await Assert.That(secondChild.Parent).IsNull();
    }

    #endregion
}
