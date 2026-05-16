namespace TerminalNinja.Tests.Unit.Controls;

public class ImageTests
{
    #region Default Values

    [Test]
    public async Task Source_Default_IsNull()
    {
        var img = new Image();
        await Assert.That(img.Source).IsNull();
    }

    [Test]
    public async Task Stretch_Default_IsUniform()
    {
        var img = new Image();
        await Assert.That(img.Stretch).IsEqualTo(Stretch.Uniform);
    }

    [Test]
    public async Task Focusable_Default_IsFalse()
    {
        // Image extends FrameworkElement, not Control — Focusable defaults to false
        var img = new Image();
        await Assert.That(img.Focusable).IsFalse();
    }

    #endregion

    #region Rendering

    [Test]
    public async Task Render_NullSource_DoesNotThrow()
    {
        var img = new Image();

        using var buffer = new CellBuffer(10, 5);
        img.Render(buffer, new Rect(0, 0, 10, 5));

        // Should not throw
        await Assert.That(img.Source).IsNull();
    }

    [Test]
    public async Task Render_SingleColorBlock_RendersFullBlock()
    {
        // 1x2 pixel image (1 cell = 2 vertical pixels)
        var pixels = new Color[1, 2];
        pixels[0, 0] = Color.Red;
        pixels[0, 1] = Color.Red;

        var img = new Image { Source = pixels, Stretch = Stretch.None };

        using var buffer = new CellBuffer(5, 3);
        img.Render(buffer, new Rect(0, 0, 5, 3));

        // Both pixels same color → full block █
        var cell = buffer.GetCell(0, 0);
        await Assert.That(cell.Codepoint).IsEqualTo('\u2588');
        await Assert.That(cell.Foreground).IsEqualTo(Color.Red);
    }

    [Test]
    public async Task Render_TwoColors_RendersHalfBlock()
    {
        // 1x2 pixel image: top=Red, bottom=Blue
        var pixels = new Color[1, 2];
        pixels[0, 0] = Color.Red;
        pixels[0, 1] = Color.Blue;

        var img = new Image { Source = pixels, Stretch = Stretch.None };

        using var buffer = new CellBuffer(5, 3);
        img.Render(buffer, new Rect(0, 0, 5, 3));

        var cell = buffer.GetCell(0, 0);
        await Assert.That(cell.Codepoint).IsEqualTo('\u2580'); // ▀
        await Assert.That(cell.Foreground).IsEqualTo(Color.Red);    // top pixel
        await Assert.That(cell.Background).IsEqualTo(Color.Blue);   // bottom pixel
    }

    [Test]
    public async Task Render_2x2Image_RendersSingleRow()
    {
        // 2x2 pixels → 2 cells wide, 1 cell tall
        var pixels = new Color[2, 2];
        pixels[0, 0] = Color.Red;
        pixels[0, 1] = Color.Green;
        pixels[1, 0] = Color.Blue;
        pixels[1, 1] = Color.Yellow;

        var img = new Image { Source = pixels, Stretch = Stretch.None };

        using var buffer = new CellBuffer(10, 5);
        img.Render(buffer, new Rect(0, 0, 10, 5));

        // Cell (0,0): top=Red, bottom=Green
        var cell0 = buffer.GetCell(0, 0);
        await Assert.That(cell0.Foreground).IsEqualTo(Color.Red);
        await Assert.That(cell0.Background).IsEqualTo(Color.Green);

        // Cell (1,0): top=Blue, bottom=Yellow
        var cell1 = buffer.GetCell(1, 0);
        await Assert.That(cell1.Foreground).IsEqualTo(Color.Blue);
        await Assert.That(cell1.Background).IsEqualTo(Color.Yellow);
    }

    #endregion

    #region GetPreferredSize

    [Test]
    public async Task GetPreferredSize_NullSource_Returns1x1()
    {
        var img = new Image();
        var size = img.GetPreferredSize(new Rect(0, 0, 40, 20));

        await Assert.That(size.Width).IsGreaterThanOrEqualTo(1);
        await Assert.That(size.Height).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task GetPreferredSize_4x6Pixels_Returns4x3Cells()
    {
        var pixels = new Color[4, 6]; // 4 wide, 6 tall → 4 cells wide, 3 cells tall (6/2)
        var img = new Image { Source = pixels };

        var size = img.GetPreferredSize(new Rect(0, 0, 40, 20));

        await Assert.That(size.Width).IsEqualTo(4);
        await Assert.That(size.Height).IsEqualTo(3);
    }

    [Test]
    public async Task GetPreferredSize_OddHeight_RoundsUp()
    {
        var pixels = new Color[3, 5]; // 5 pixels tall → ceil(5/2) = 3 cells
        var img = new Image { Source = pixels };

        var size = img.GetPreferredSize(new Rect(0, 0, 40, 20));

        await Assert.That(size.Width).IsEqualTo(3);
        await Assert.That(size.Height).IsEqualTo(3);
    }

    #endregion

    #region Stretch Modes

    [Test]
    public async Task Stretch_Fill_ScalesToFillBounds()
    {
        // Small 2x2 image stretched to fill 4x2 cell area (4x4 pixels)
        var pixels = new Color[2, 2];
        pixels[0, 0] = Color.Red;
        pixels[1, 0] = Color.Red;
        pixels[0, 1] = Color.Red;
        pixels[1, 1] = Color.Red;

        var img = new Image { Source = pixels, Stretch = Stretch.Fill, Width = Size.Absolute(4), Height = Size.Absolute(2) };

        using var buffer = new CellBuffer(10, 5);
        img.Render(buffer, new Rect(0, 0, 10, 5));

        // All 4 cells should have red content
        await Assert.That(buffer.GetCell(0, 0).Foreground).IsEqualTo(Color.Red);
        await Assert.That(buffer.GetCell(3, 0).Foreground).IsEqualTo(Color.Red);
    }

    [Test]
    public async Task Stretch_None_NoScaling()
    {
        var pixels = new Color[2, 2];
        pixels[0, 0] = Color.Green;
        pixels[1, 0] = Color.Green;
        pixels[0, 1] = Color.Green;
        pixels[1, 1] = Color.Green;

        var img = new Image { Source = pixels, Stretch = Stretch.None, Width = Size.Absolute(10), Height = Size.Absolute(5) };

        using var buffer = new CellBuffer(10, 5);
        img.Render(buffer, new Rect(0, 0, 10, 5));

        // Only first 2 columns, 1 row should have content
        await Assert.That(buffer.GetCell(0, 0).Foreground).IsEqualTo(Color.Green);
        // Cell (5,0) should be empty (beyond source)
        await Assert.That(buffer.GetCell(5, 0).Codepoint).IsNotEqualTo('\u2588');
    }

    #endregion

    #region XAML

    [Test]
    public async Task Xaml_ParsesStretch()
    {
        var xaml = """
            <Image xmlns="http://schemas.terminalninja.dev/xaml" Stretch="Fill" />
            """;
        var img = TerminalXaml.Load<Image>(xaml);

        await Assert.That(img.Stretch).IsEqualTo(Stretch.Fill);
    }

    #endregion
}
