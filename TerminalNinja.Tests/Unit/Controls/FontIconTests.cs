namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Tests for the FontIcon control covering:
/// - Symbol enum to glyph conversion
/// - Direct glyph rendering
/// - Foreground and background colors
/// - Alignment and sizing
/// - Preferred size calculation
/// - Edge cases (empty glyph, None symbol)
/// </summary>
public class FontIconTests
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

    [After(Test)]
    public Task Cleanup()
    {
        _buffer.Dispose();
        return Task.CompletedTask;
    }

    #region Symbol to Glyph Conversion

    [Test]
    public async Task Symbol_Check_SetsGlyphToCorrectCharacter()
    {
        // Arrange & Act
        var icon = new FontIcon { Symbol = Symbol.Check };

        // Assert — Check is U+F00C
        await Assert.That(icon.Glyph).IsEqualTo("\uF00C");
    }

    [Test]
    public async Task Symbol_Home_SetsGlyphToCorrectCharacter()
    {
        // Arrange & Act
        var icon = new FontIcon { Symbol = Symbol.Home };

        // Assert — Home is U+F015
        await Assert.That(icon.Glyph).IsEqualTo("\uF015");
    }

    [Test]
    public async Task Symbol_Branch_SetsGlyphToCorrectCharacter()
    {
        // Arrange & Act
        var icon = new FontIcon { Symbol = Symbol.Branch };

        // Assert — Branch is U+E0A0
        await Assert.That(icon.Glyph).IsEqualTo("\uE0A0");
    }

    [Test]
    public async Task Symbol_None_SetsGlyphToEmpty()
    {
        // Arrange & Act
        var icon = new FontIcon { Symbol = Symbol.None };

        // Assert
        await Assert.That(icon.Glyph).IsEqualTo("");
    }

    [Test]
    public async Task Symbol_ChangeFromOneToAnother_UpdatesGlyph()
    {
        // Arrange
        var icon = new FontIcon { Symbol = Symbol.Check };
        await Assert.That(icon.Glyph).IsEqualTo("\uF00C");

        // Act
        icon.Symbol = Symbol.Warning;

        // Assert — Warning is U+F071
        await Assert.That(icon.Glyph).IsEqualTo("\uF071");
    }

    [Test]
    public async Task Symbol_ChangeToNone_ClearsGlyph()
    {
        // Arrange
        var icon = new FontIcon { Symbol = Symbol.Star };
        await Assert.That(icon.Glyph).IsNotEqualTo("");

        // Act
        icon.Symbol = Symbol.None;

        // Assert
        await Assert.That(icon.Glyph).IsEqualTo("");
    }

    #endregion

    #region Basic Rendering

    [Test]
    public async Task Render_SymbolCheck_DisplaysCorrectCharacter()
    {
        // Arrange
        var icon = new FontIcon { Symbol = Symbol.Check };
        var bounds = new Rect(0, 0, 1, 1);

        // Act
        icon.Render(_buffer, bounds);

        // Assert — U+F00C
        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('\uF00C');
    }

    [Test]
    public async Task Render_DirectGlyph_DisplaysCorrectCharacter()
    {
        // Arrange
        var icon = new FontIcon { Glyph = "\uE702" }; // Git icon
        var bounds = new Rect(0, 0, 1, 1);

        // Act
        icon.Render(_buffer, bounds);

        // Assert
        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('\uE702');
    }

    [Test]
    public async Task Render_EmptyGlyph_DoesNotWriteCharacter()
    {
        // Arrange
        var icon = new FontIcon { Glyph = "" };
        var bounds = new Rect(0, 0, 1, 1);
        var emptyChar = _buffer.GetCell(0, 0).Character;

        // Act
        icon.Render(_buffer, bounds);

        // Assert — cell should remain unchanged
        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo(emptyChar);
    }

    [Test]
    public async Task Render_NoneSymbol_DoesNotWriteCharacter()
    {
        // Arrange
        var icon = new FontIcon { Symbol = Symbol.None };
        var bounds = new Rect(0, 0, 1, 1);
        var emptyChar = _buffer.GetCell(0, 0).Character;

        // Act
        icon.Render(_buffer, bounds);

        // Assert
        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo(emptyChar);
    }

    #endregion

    #region Colors

    [Test]
    public async Task Render_CustomForeground_AppliesColor()
    {
        // Arrange
        var icon = new FontIcon
        {
            Symbol = Symbol.Star,
            Foreground = Color.Yellow,
            Background = Color.Black
        };
        var bounds = new Rect(0, 0, 1, 1);

        // Act
        icon.Render(_buffer, bounds);

        // Assert
        await Assert.That(_buffer.GetCell(0, 0).Foreground).IsEqualTo(Color.Yellow);
    }

    [Test]
    public async Task Render_CustomBackground_AppliesColor()
    {
        // Arrange
        var icon = new FontIcon
        {
            Symbol = Symbol.Warning,
            Foreground = Color.Red,
            Background = Color.Yellow
        };
        var bounds = new Rect(0, 0, 1, 1);

        // Act
        icon.Render(_buffer, bounds);

        // Assert
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Yellow);
    }

    [Test]
    public async Task Render_TransparentBackground_PreservesExistingBackground()
    {
        // Arrange — pre-fill buffer cell with a background
        _buffer.SetChar(5, 5, ' ', Color.White, Color.Blue);
        var icon = new FontIcon
        {
            Symbol = Symbol.Check,
            Foreground = Color.Green,
            Background = Color.Transparent
        };
        var bounds = new Rect(5, 5, 1, 1);

        // Act
        icon.Render(_buffer, bounds);

        // Assert — icon should be rendered but background stays Blue
        await Assert.That(_buffer.GetCell(5, 5).Character).IsEqualTo('\uF00C');
        await Assert.That(_buffer.GetCell(5, 5).Foreground).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(5, 5).Background).IsEqualTo(Color.Blue);
    }

    #endregion

    #region Sizing

    [Test]
    public async Task GetPreferredSize_Always1x1()
    {
        // Arrange
        var icon = new FontIcon { Symbol = Symbol.Home };
        var parent = new Rect(0, 0, 80, 24);

        // Act
        var size = icon.GetPreferredSize(parent);

        // Assert
        await Assert.That(size.Width).IsEqualTo(1);
        await Assert.That(size.Height).IsEqualTo(1);
    }

    [Test]
    public async Task CalculateBounds_AutoSize_Returns1x1()
    {
        // Arrange
        var icon = new FontIcon { Symbol = Symbol.Home };
        var parent = new Rect(0, 0, 80, 24);

        // Act
        var bounds = icon.CalculateBounds(parent);

        // Assert
        await Assert.That(bounds.Width).IsEqualTo(1);
        await Assert.That(bounds.Height).IsEqualTo(1);
    }

    [Test]
    public async Task CalculateBounds_ExplicitSize_UsesResolvedDimensions()
    {
        // Arrange
        var icon = new FontIcon
        {
            Symbol = Symbol.Home,
            Width = Size.Absolute(5),
            Height = Size.Absolute(3)
        };
        var parent = new Rect(0, 0, 80, 24);

        // Act
        var bounds = icon.CalculateBounds(parent);

        // Assert
        await Assert.That(bounds.Width).IsEqualTo(5);
        await Assert.That(bounds.Height).IsEqualTo(3);
    }

    #endregion

    #region Alignment

    [Test]
    public async Task Render_HorizontalCenterInLargerBounds_CentersGlyph()
    {
        // Arrange
        var icon = new FontIcon
        {
            Symbol = Symbol.Star,
            Width = Size.Absolute(5),
            Height = Size.Absolute(1)
        };
        var bounds = new Rect(0, 0, 5, 1);

        // Act
        icon.Render(_buffer, bounds);

        // Assert — glyph should be centered at x=2
        await Assert.That(_buffer.GetCell(2, 0).Character).IsEqualTo('\uF005');
        // Adjacent cells should not have the glyph
        await Assert.That(_buffer.GetCell(0, 0).Character).IsNotEqualTo('\uF005');
        await Assert.That(_buffer.GetCell(4, 0).Character).IsNotEqualTo('\uF005');
    }

    [Test]
    public async Task Render_VerticalCenterInLargerBounds_CentersGlyph()
    {
        // Arrange
        var icon = new FontIcon
        {
            Symbol = Symbol.Star,
            Width = Size.Absolute(1),
            Height = Size.Absolute(5)
        };
        var bounds = new Rect(0, 0, 1, 5);

        // Act
        icon.Render(_buffer, bounds);

        // Assert — glyph should be centered at y=2
        await Assert.That(_buffer.GetCell(0, 2).Character).IsEqualTo('\uF005');
        // Adjacent cells should not have the glyph
        await Assert.That(_buffer.GetCell(0, 0).Character).IsNotEqualTo('\uF005');
        await Assert.That(_buffer.GetCell(0, 4).Character).IsNotEqualTo('\uF005');
    }

    [Test]
    public async Task Render_HorizontalAlignmentEnd_PositionsAtEnd()
    {
        // Arrange
        var icon = new FontIcon
        {
            Symbol = Symbol.Check,
            HorizontalAlignment = Alignment.End
        };
        var bounds = new Rect(0, 0, 10, 1);

        // Act
        icon.Render(_buffer, bounds);

        // Assert — Auto size is 1x1, aligned to the end of 10-wide area
        await Assert.That(_buffer.GetCell(9, 0).Character).IsEqualTo('\uF00C');
    }

    [Test]
    public async Task Render_HorizontalAlignmentCenter_PositionsAtCenter()
    {
        // Arrange
        var icon = new FontIcon
        {
            Symbol = Symbol.Check,
            HorizontalAlignment = Alignment.Center
        };
        var bounds = new Rect(0, 0, 10, 1);

        // Act
        icon.Render(_buffer, bounds);

        // Assert — centered: (10-1)/2 = 4 (integer division)
        await Assert.That(_buffer.GetCell(4, 0).Character).IsEqualTo('\uF00C');
    }

    [Test]
    public async Task Render_VerticalAlignmentEnd_PositionsAtBottom()
    {
        // Arrange
        var icon = new FontIcon
        {
            Symbol = Symbol.Check,
            VerticalAlignment = Alignment.End
        };
        var bounds = new Rect(0, 0, 1, 10);

        // Act
        icon.Render(_buffer, bounds);

        // Assert — Auto size is 1x1, aligned to end of 10-high area
        await Assert.That(_buffer.GetCell(0, 9).Character).IsEqualTo('\uF00C');
    }

    #endregion

    #region Rendering with Background Fill

    [Test]
    public async Task Render_OpaqueBackgroundInLargerBounds_FillsEntireArea()
    {
        // Arrange
        var icon = new FontIcon
        {
            Symbol = Symbol.Star,
            Width = Size.Absolute(3),
            Height = Size.Absolute(3),
            Foreground = Color.Yellow,
            Background = Color.Blue
        };
        var bounds = new Rect(0, 0, 3, 3);

        // Act
        icon.Render(_buffer, bounds);

        // Assert — background should fill all 9 cells
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                await Assert.That(_buffer.GetCell(x, y).Background).IsEqualTo(Color.Blue);
            }
        }

        // Glyph should be centered at (1,1)
        await Assert.That(_buffer.GetCell(1, 1).Character).IsEqualTo('\uF005');
    }

    [Test]
    public async Task Render_TransparentBackgroundInLargerBounds_DoesNotFillArea()
    {
        // Arrange — pre-fill with red
        var bgCell = new Cell(' ', Color.White, Color.Red);
        _buffer.FillRect(new Rect(0, 0, 3, 3), bgCell);

        var icon = new FontIcon
        {
            Symbol = Symbol.Star,
            Width = Size.Absolute(3),
            Height = Size.Absolute(3),
            Foreground = Color.Yellow,
            Background = Color.Transparent
        };
        var bounds = new Rect(0, 0, 3, 3);

        // Act
        icon.Render(_buffer, bounds);

        // Assert — non-glyph cells should still have red background
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        // Glyph cell background is preserved from existing
        await Assert.That(_buffer.GetCell(1, 1).Background).IsEqualTo(Color.Red);
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task Render_OutOfBoundsPosition_DoesNotCrash()
    {
        // Arrange
        var icon = new FontIcon { Symbol = Symbol.Check };
        var bounds = new Rect(-5, -5, 1, 1);

        // Act — should not throw
        icon.Render(_buffer, bounds);

        // Assert — buffer is still valid (no crash)
        await Assert.That(_buffer.Width).IsEqualTo(BufferWidth);
    }

    [Test]
    public async Task Render_ZeroSizeBounds_DoesNotCrash()
    {
        // Arrange
        var icon = new FontIcon
        {
            Symbol = Symbol.Check,
            Width = Size.Absolute(0),
            Height = Size.Absolute(0)
        };
        var bounds = new Rect(0, 0, 0, 0);

        // Act — should not throw
        icon.Render(_buffer, bounds);

        // Assert — buffer is still valid (no crash)
        await Assert.That(_buffer.Width).IsEqualTo(BufferWidth);
    }

    [Test]
    public async Task Render_AtBufferEdge_DisplaysCorrectly()
    {
        // Arrange
        var icon = new FontIcon { Symbol = Symbol.Check };
        var bounds = new Rect(BufferWidth - 1, BufferHeight - 1, 1, 1);

        // Act
        icon.Render(_buffer, bounds);

        // Assert
        await Assert.That(_buffer.GetCell(BufferWidth - 1, BufferHeight - 1).Character)
            .IsEqualTo('\uF00C');
    }

    [Test]
    public async Task DefaultProperties_AreCorrect()
    {
        // Arrange & Act
        var icon = new FontIcon();

        // Assert
        await Assert.That(icon.Symbol).IsEqualTo(Symbol.None);
        await Assert.That(icon.Glyph).IsEqualTo("");
        await Assert.That(icon.Foreground).IsEqualTo(Color.White);
        await Assert.That(icon.Background).IsEqualTo(Color.Transparent);
        await Assert.That(icon.Width).IsEqualTo(Size.Auto);
        await Assert.That(icon.Height).IsEqualTo(Size.Auto);
    }

    [Test]
    public async Task Glyph_SetDirectly_OverridesSymbol()
    {
        // Arrange
        var icon = new FontIcon { Symbol = Symbol.Check };
        await Assert.That(icon.Glyph).IsEqualTo("\uF00C");

        // Act — set glyph directly (does not change Symbol enum)
        icon.Glyph = "\uE702";

        // Assert
        await Assert.That(icon.Glyph).IsEqualTo("\uE702");
    }

    [Test]
    public async Task Render_MultiCharGlyph_OnlyRendersFirstChar()
    {
        // Arrange
        var icon = new FontIcon { Glyph = "AB" };
        var bounds = new Rect(0, 0, 1, 1);

        // Act
        icon.Render(_buffer, bounds);

        // Assert — only 'A' should be rendered
        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('A');
    }

    #endregion

    #region Symbol Enum Values

    [Test]
    public async Task Symbol_PowerlineIcons_HaveCorrectCodepoints()
    {
        var branch = GetCodepoint(Symbol.Branch);
        var arrowRight = GetCodepoint(Symbol.ArrowRight);
        var arrowLeft = GetCodepoint(Symbol.ArrowLeft);

        await Assert.That(branch).IsEqualTo(0xE0A0);
        await Assert.That(arrowRight).IsEqualTo(0xE0B0);
        await Assert.That(arrowLeft).IsEqualTo(0xE0B2);
    }

    [Test]
    public async Task Symbol_FontAwesomeIcons_HaveCorrectCodepoints()
    {
        await Assert.That(GetCodepoint(Symbol.Heart)).IsEqualTo(0xF004);
        await Assert.That(GetCodepoint(Symbol.Star)).IsEqualTo(0xF005);
        await Assert.That(GetCodepoint(Symbol.Check)).IsEqualTo(0xF00C);
        await Assert.That(GetCodepoint(Symbol.Close)).IsEqualTo(0xF00D);
        await Assert.That(GetCodepoint(Symbol.Search)).IsEqualTo(0xF002);
        await Assert.That(GetCodepoint(Symbol.Settings)).IsEqualTo(0xF013);
        await Assert.That(GetCodepoint(Symbol.Home)).IsEqualTo(0xF015);
        await Assert.That(GetCodepoint(Symbol.Warning)).IsEqualTo(0xF071);
        await Assert.That(GetCodepoint(Symbol.Bug)).IsEqualTo(0xF188);
    }

    [Test]
    public async Task Symbol_DevIcons_HaveCorrectCodepoints()
    {
        await Assert.That(GetCodepoint(Symbol.Git)).IsEqualTo(0xE702);
        await Assert.That(GetCodepoint(Symbol.Terminal)).IsEqualTo(0xE795);
        await Assert.That(GetCodepoint(Symbol.Docker)).IsEqualTo(0xE7B0);
        await Assert.That(GetCodepoint(Symbol.DotNet)).IsEqualTo(0xE77F);
    }

    [Test]
    public async Task Symbol_Codicons_HaveCorrectCodepoints()
    {
        await Assert.That(GetCodepoint(Symbol.Debug)).IsEqualTo(0xEA87);
        await Assert.That(GetCodepoint(Symbol.Extensions)).IsEqualTo(0xEA78);
        await Assert.That(GetCodepoint(Symbol.SourceControl)).IsEqualTo(0xEA68);
    }

    /// <summary>Helper to extract codepoint as int (avoids TUnit constant-value analyzer error).</summary>
    private static int GetCodepoint(Symbol symbol) => (ushort)symbol;

    #endregion
}
