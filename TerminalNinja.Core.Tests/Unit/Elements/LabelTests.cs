namespace TerminalNinja.Core.Tests.Unit.Elements;

/// <summary>
/// Comprehensive tests for Label element covering:
/// - Basic text rendering
/// - Horizontal text alignment (Start, Center, End)
/// - Vertical text alignment (Start, Center, End)
/// - Text wrapping
/// - Text truncation with ellipsis
/// - Padding
/// - GetPreferredSize
/// - Empty text handling
/// - Edge cases
/// </summary>
public class LabelTests
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

    #region Basic Rendering

    [Test]
    public async Task Render_SimpleText_DisplaysCorrectly()
    {
        // Arrange
        var label = new Label 
        { 
            Text = "Hello",
            Width = Size.Absolute(10),
            Height = Size.Absolute(3)
        };
        var bounds = new Rect(0, 0, 80, 24);

        // Act
        label.Render(_buffer, bounds);

        // Assert
        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('H');
        await Assert.That(_buffer.GetCell(1, 0).Character).IsEqualTo('e');
        await Assert.That(_buffer.GetCell(2, 0).Character).IsEqualTo('l');
        await Assert.That(_buffer.GetCell(3, 0).Character).IsEqualTo('l');
        await Assert.That(_buffer.GetCell(4, 0).Character).IsEqualTo('o');
    }

    [Test]
    public async Task Render_EmptyText_FillsBackground()
    {
        // Arrange
        var label = new Label 
        { 
            Text = "",
            Width = Size.Absolute(10),
            Height = Size.Absolute(3),
            BackgroundColor = Color.Red
        };
        var bounds = new Rect(0, 0, 80, 24);

        // Act
        label.Render(_buffer, bounds);

        // Assert
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo(' ');
    }

    [Test]
    public async Task Render_WithColors_AppliesCorrectly()
    {
        // Arrange
        var label = new Label 
        { 
            Text = "Test",
            Width = Size.Absolute(10),
            Height = Size.Absolute(3),
            ForegroundColor = Color.Cyan,
            BackgroundColor = Color.Magenta
        };
        var bounds = new Rect(0, 0, 80, 24);

        // Act
        label.Render(_buffer, bounds);

        // Assert
        await Assert.That(_buffer.GetCell(0, 0).Foreground).IsEqualTo(Color.Cyan);
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Magenta);
    }

    #endregion

    #region Horizontal Alignment

    [Test]
    public async Task Render_HorizontalAlignmentStart_AlignsLeft()
    {
        // Arrange
        var label = new Label 
        { 
            Text = "Hi",
            Width = Size.Absolute(10),
            Height = Size.Absolute(1),
            HorizontalTextAlignment = TextAlignment.Start
        };
        var bounds = new Rect(0, 0, 80, 24);

        // Act
        label.Render(_buffer, bounds);

        // Assert
        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('H');
        await Assert.That(_buffer.GetCell(1, 0).Character).IsEqualTo('i');
        await Assert.That(_buffer.GetCell(2, 0).Character).IsEqualTo(' ');
    }

    [Test]
    public async Task Render_HorizontalAlignmentCenter_CentersText()
    {
        // Arrange
        var label = new Label 
        { 
            Text = "Hi",
            Width = Size.Absolute(10),
            Height = Size.Absolute(1),
            HorizontalTextAlignment = TextAlignment.Center
        };
        var bounds = new Rect(0, 0, 80, 24);

        // Act
        label.Render(_buffer, bounds);

        // Assert - "Hi" (2 chars) in 10 width should be at position 4 (10-2)/2
        await Assert.That(_buffer.GetCell(4, 0).Character).IsEqualTo('H');
        await Assert.That(_buffer.GetCell(5, 0).Character).IsEqualTo('i');
    }

    [Test]
    public async Task Render_HorizontalAlignmentEnd_AlignsRight()
    {
        // Arrange
        var label = new Label 
        { 
            Text = "Hi",
            Width = Size.Absolute(10),
            Height = Size.Absolute(1),
            HorizontalTextAlignment = TextAlignment.End
        };
        var bounds = new Rect(0, 0, 80, 24);

        // Act
        label.Render(_buffer, bounds);

        // Assert - "Hi" (2 chars) in 10 width should be at position 8 (10-2)
        await Assert.That(_buffer.GetCell(8, 0).Character).IsEqualTo('H');
        await Assert.That(_buffer.GetCell(9, 0).Character).IsEqualTo('i');
    }

    #endregion

    #region Vertical Alignment

    [Test]
    public async Task Render_VerticalAlignmentStart_AlignsTop()
    {
        // Arrange
        var label = new Label 
        { 
            Text = "Test",
            Width = Size.Absolute(10),
            Height = Size.Absolute(5),
            VerticalTextAlignment = TextAlignment.Start
        };
        var bounds = new Rect(0, 0, 80, 24);

        // Act
        label.Render(_buffer, bounds);

        // Assert
        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('T');
    }

    [Test]
    public async Task Render_VerticalAlignmentCenter_CentersText()
    {
        // Arrange
        var label = new Label 
        { 
            Text = "Test",
            Width = Size.Absolute(10),
            Height = Size.Absolute(5),
            VerticalTextAlignment = TextAlignment.Center
        };
        var bounds = new Rect(0, 0, 80, 24);

        // Act
        label.Render(_buffer, bounds);

        // Assert - 1 line in 5 height should be at row 2 (5-1)/2
        await Assert.That(_buffer.GetCell(0, 2).Character).IsEqualTo('T');
    }

    [Test]
    public async Task Render_VerticalAlignmentEnd_AlignsBottom()
    {
        // Arrange
        var label = new Label 
        { 
            Text = "Test",
            Width = Size.Absolute(10),
            Height = Size.Absolute(5),
            VerticalTextAlignment = TextAlignment.End
        };
        var bounds = new Rect(0, 0, 80, 24);

        // Act
        label.Render(_buffer, bounds);

        // Assert - 1 line in 5 height should be at row 4 (5-1)
        await Assert.That(_buffer.GetCell(0, 4).Character).IsEqualTo('T');
    }

    #endregion

    #region Text Wrapping

    [Test]
    public async Task Render_NoWrap_ClipsLongText()
    {
        // Arrange
        var label = new Label 
        { 
            Text = "This is a very long text that should not wrap",
            Width = Size.Absolute(10),
            Height = Size.Absolute(3),
            TextWrapping = TextWrapping.NoWrap
        };
        var bounds = new Rect(0, 0, 80, 24);

        // Act
        label.Render(_buffer, bounds);

        // Assert - Only first 10 characters should be visible on row 0
        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('T');
        await Assert.That(_buffer.GetCell(9, 0).Character).IsEqualTo(' ');
        await Assert.That(_buffer.GetCell(0, 1).Character).IsEqualTo(' '); // Row 1 should be empty
    }

    [Test]
    public async Task Render_Wrap_WrapsToMultipleLines()
    {
        // Arrange
        var label = new Label 
        { 
            Text = "Hello world test",
            Width = Size.Absolute(10),
            Height = Size.Absolute(3),
            TextWrapping = TextWrapping.Wrap
        };
        var bounds = new Rect(0, 0, 80, 24);

        // Act
        label.Render(_buffer, bounds);

        // Assert - "Hello" on line 1, "world" on line 2, "test" on line 3
        var line1 = new string(new[] { 
            _buffer.GetCell(0, 0).Character, 
            _buffer.GetCell(1, 0).Character,
            _buffer.GetCell(2, 0).Character,
            _buffer.GetCell(3, 0).Character,
            _buffer.GetCell(4, 0).Character
        });
        await Assert.That(line1).IsEqualTo("Hello");
        
        var line2 = new string(new[] { 
            _buffer.GetCell(0, 1).Character, 
            _buffer.GetCell(1, 1).Character,
            _buffer.GetCell(2, 1).Character,
            _buffer.GetCell(3, 1).Character,
            _buffer.GetCell(4, 1).Character
        });
        await Assert.That(line2).IsEqualTo("world");
    }

    #endregion

    #region Text Truncation

    [Test]
    public async Task Render_TruncationNone_ClipsAtBoundary()
    {
        // Arrange
        var label = new Label 
        { 
            Text = "VeryLongWord",
            Width = Size.Absolute(8),
            Height = Size.Absolute(1),
            TextTrimming = TextTrimming.None
        };
        var bounds = new Rect(0, 0, 80, 24);

        // Act
        label.Render(_buffer, bounds);

        // Assert - First 8 characters
        var rendered = new string(new[] {
            _buffer.GetCell(0, 0).Character,
            _buffer.GetCell(1, 0).Character,
            _buffer.GetCell(2, 0).Character,
            _buffer.GetCell(3, 0).Character,
            _buffer.GetCell(4, 0).Character,
            _buffer.GetCell(5, 0).Character,
            _buffer.GetCell(6, 0).Character,
            _buffer.GetCell(7, 0).Character
        });
        await Assert.That(rendered).IsEqualTo("VeryLong");
    }

    [Test]
    public async Task Render_TruncationEllipsis_AddsEllipsis()
    {
        // Arrange
        var label = new Label 
        { 
            Text = "VeryLongWord",
            Width = Size.Absolute(8),
            Height = Size.Absolute(1),
            TextTrimming = TextTrimming.Ellipsis
        };
        var bounds = new Rect(0, 0, 80, 24);

        // Act
        label.Render(_buffer, bounds);

        // Assert - First 5 chars + "..."
        var rendered = new string(new[] {
            _buffer.GetCell(0, 0).Character,
            _buffer.GetCell(1, 0).Character,
            _buffer.GetCell(2, 0).Character,
            _buffer.GetCell(3, 0).Character,
            _buffer.GetCell(4, 0).Character,
            _buffer.GetCell(5, 0).Character,
            _buffer.GetCell(6, 0).Character,
            _buffer.GetCell(7, 0).Character
        });
        await Assert.That(rendered).IsEqualTo("VeryL...");
    }

    #endregion

    #region Padding

    [Test]
    public async Task Render_WithPadding_OffsetsText()
    {
        // Arrange
        var label = new Label 
        { 
            Text = "Hi",
            Width = Size.Absolute(10),
            Height = Size.Absolute(5),
            Padding = new Thickness(2, 1, 2, 1) // left, top, right, bottom
        };
        var bounds = new Rect(0, 0, 80, 24);

        // Act
        label.Render(_buffer, bounds);

        // Assert - Text should start at (2, 1) due to padding
        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo(' '); // Padding area
        await Assert.That(_buffer.GetCell(2, 1).Character).IsEqualTo('H'); // Text starts here
        await Assert.That(_buffer.GetCell(3, 1).Character).IsEqualTo('i');
    }

    [Test]
    public async Task GetPreferredSize_WithText_ReturnsTextLengthPlusPadding()
    {
        // Arrange
        var label = new Label 
        { 
            Text = "Hello",
            Padding = new Thickness(2, 1, 2, 1)
        };
        var parent = new Rect(0, 0, 100, 50);

        // Act
        var size = label.GetPreferredSize(parent);

        // Assert - "Hello" is 5 chars + 4 horizontal padding + 2 vertical padding
        await Assert.That(size.Width).IsEqualTo(9);  // 5 + 2 + 2
        await Assert.That(size.Height).IsEqualTo(3); // 1 + 1 + 1
    }

    [Test]
    public async Task GetPreferredSize_EmptyText_ReturnsPaddingOnly()
    {
        // Arrange
        var label = new Label 
        { 
            Text = "",
            Padding = new Thickness(3, 2, 3, 2)
        };
        var parent = new Rect(0, 0, 100, 50);

        // Act
        var size = label.GetPreferredSize(parent);

        // Assert
        await Assert.That(size.Width).IsEqualTo(6);  // 3 + 3
        await Assert.That(size.Height).IsEqualTo(4); // 2 + 2
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task Render_NullText_DoesNotCrash()
    {
        // Arrange
        var label = new Label 
        { 
            Text = null!,
            Width = Size.Absolute(10),
            Height = Size.Absolute(3)
        };
        var bounds = new Rect(0, 0, 80, 24);

        // Act & Assert - Should not throw
        label.Render(_buffer, bounds);
        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo(' ');
    }

    [Test]
    public async Task Render_ZeroWidthBounds_DoesNotRender()
    {
        // Arrange
        var label = new Label 
        { 
            Text = "Test",
            Width = Size.Absolute(0),
            Height = Size.Absolute(3)
        };
        var bounds = new Rect(0, 0, 80, 24);

        // Act
        label.Render(_buffer, bounds);

        // Assert - Nothing should be rendered (all cells remain black/empty)
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Black);
    }

    [Test]
    public async Task Render_PaddingLargerThanBounds_DoesNotRenderText()
    {
        // Arrange
        var label = new Label 
        { 
            Text = "Test",
            Width = Size.Absolute(5),
            Height = Size.Absolute(5),
            Padding = new Thickness(10) // Padding larger than bounds
        };
        var bounds = new Rect(0, 0, 80, 24);

        // Act
        label.Render(_buffer, bounds);

        // Assert - Only background should be filled, no text
        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo(' ');
    }

    [Test]
    public async Task CalculateBounds_WithAlignment_PositionsCorrectly()
    {
        // Arrange
        var label = new Label 
        { 
            Text = "Test",
            Width = Size.Absolute(10),
            Height = Size.Absolute(5),
            HorizontalAlignment = Alignment.Center,
            VerticalAlignment = Alignment.Center
        };
        var parent = new Rect(0, 0, 40, 20);

        // Act
        var bounds = label.CalculateBounds(parent);

        // Assert - Should be centered in parent
        await Assert.That(bounds.X).IsEqualTo(15); // (40-10)/2
        await Assert.That(bounds.Y).IsEqualTo(7);  // (20-5)/2
        await Assert.That(bounds.Width).IsEqualTo(10);
        await Assert.That(bounds.Height).IsEqualTo(5);
    }

    #endregion
}
