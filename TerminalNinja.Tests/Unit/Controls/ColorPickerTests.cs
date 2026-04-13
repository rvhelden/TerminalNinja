namespace TerminalNinja.Tests.Unit.Controls;

public class ColorPickerTests
{
    #region Default Values

    [Test]
    public async Task SelectedColor_Default_IsWhite()
    {
        var cp = new ColorPicker();
        await Assert.That(cp.SelectedColor).IsEqualTo(Color.White);
    }

    [Test]
    public async Task Focusable_Default_IsTrue()
    {
        var cp = new ColorPicker();
        await Assert.That(cp.Focusable).IsTrue();
    }

    [Test]
    public async Task FocusColor_Default_IsCyan()
    {
        var cp = new ColorPicker();
        await Assert.That(cp.FocusColor).IsEqualTo(Color.Cyan);
    }

    #endregion

    #region Palette Navigation

    [Test]
    public async Task RightArrow_MovesPaletteRight()
    {
        var cp = new ColorPicker();
        // Start at index 0 (Black), move right to index 1
        cp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        cp.OnKeyEvent(new KeyEvent(ConsoleKey.Enter, '\r', false, false, false));

        // Index 1 should be dark red (128,0,0)
        await Assert.That(cp.SelectedColor).IsNotEqualTo(Color.Black);
    }

    [Test]
    public async Task DownArrow_MovesPaletteDown()
    {
        var cp = new ColorPicker();
        // Move down one row (8 columns) then select
        cp.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false));
        cp.OnKeyEvent(new KeyEvent(ConsoleKey.Enter, '\r', false, false, false));

        // Row 1, col 0 should be DarkGray
        await Assert.That(cp.SelectedColor).IsEqualTo(Color.DarkGray);
    }

    [Test]
    public async Task Enter_SelectsPaletteColor()
    {
        var cp = new ColorPicker();
        // Index 0 = Black
        cp.OnKeyEvent(new KeyEvent(ConsoleKey.Enter, '\r', false, false, false));

        await Assert.That(cp.SelectedColor).IsEqualTo(Color.Black);
    }

    [Test]
    public async Task LeftArrow_AtStart_StaysAtZero()
    {
        var cp = new ColorPicker();
        cp.OnKeyEvent(new KeyEvent(ConsoleKey.LeftArrow, '\0', false, false, false));
        cp.OnKeyEvent(new KeyEvent(ConsoleKey.Enter, '\r', false, false, false));

        await Assert.That(cp.SelectedColor).IsEqualTo(Color.Black);
    }

    #endregion

    #region Hex Entry

    [Test]
    public async Task HexDigits_SetColor()
    {
        var cp = new ColorPicker();
        // Type "FF0000" (red)
        cp.OnKeyEvent(new KeyEvent(ConsoleKey.F, 'F', false, false, false));
        cp.OnKeyEvent(new KeyEvent(ConsoleKey.F, 'F', false, false, false));
        cp.OnKeyEvent(new KeyEvent(ConsoleKey.D0, '0', false, false, false));
        cp.OnKeyEvent(new KeyEvent(ConsoleKey.D0, '0', false, false, false));
        cp.OnKeyEvent(new KeyEvent(ConsoleKey.D0, '0', false, false, false));
        cp.OnKeyEvent(new KeyEvent(ConsoleKey.D0, '0', false, false, false));

        await Assert.That(cp.SelectedColor).IsEqualTo(Color.Red);
    }

    [Test]
    public async Task Escape_CancelsHexEntry()
    {
        var cp = new ColorPicker();
        var original = cp.SelectedColor;

        // Start typing hex
        cp.OnKeyEvent(new KeyEvent(ConsoleKey.A, 'A', false, false, false));
        cp.OnKeyEvent(new KeyEvent(ConsoleKey.B, 'B', false, false, false));

        // Cancel
        cp.OnKeyEvent(new KeyEvent(ConsoleKey.Escape, '\0', false, false, false));

        // Color should not have changed
        await Assert.That(cp.SelectedColor).IsEqualTo(original);
    }

    [Test]
    public async Task Backspace_DeletesHexDigit()
    {
        var cp = new ColorPicker();
        cp.OnKeyEvent(new KeyEvent(ConsoleKey.A, 'A', false, false, false));
        cp.OnKeyEvent(new KeyEvent(ConsoleKey.Backspace, '\0', false, false, false));

        // Should exit hex mode (buffer empty)
        // Now arrow keys should work again
        cp.OnKeyEvent(new KeyEvent(ConsoleKey.Enter, '\r', false, false, false));
        await Assert.That(cp.SelectedColor).IsEqualTo(Color.Black); // palette index 0
    }

    #endregion

    #region Events

    [Test]
    public async Task SelectedColorChanged_Fires()
    {
        var cp = new ColorPicker();
        var fired = false;
        cp.SelectedColorChanged += (_, _) => fired = true;

        cp.SelectedColor = Color.Red;

        await Assert.That(fired).IsTrue();
    }

    [Test]
    public async Task SelectedColorChanged_HasCorrectValues()
    {
        var cp = new ColorPicker { SelectedColor = Color.Blue };
        Color oldVal = default, newVal = default;
        cp.SelectedColorChanged += (_, e) => { oldVal = e.OldValue; newVal = e.NewValue; };

        cp.SelectedColor = Color.Green;

        await Assert.That(oldVal).IsEqualTo(Color.Blue);
        await Assert.That(newVal).IsEqualTo(Color.Green);
    }

    #endregion

    #region Rendering

    [Test]
    public async Task Render_ShowsColorSwatch()
    {
        var cp = new ColorPicker { SelectedColor = Color.Red };

        using var buffer = new CellBuffer(20, 5);
        cp.Render(buffer, new Rect(0, 0, 20, 5));

        // Swatch at (1,1) should be full block with Red foreground
        var cell = buffer.GetCell(1, 1);
        await Assert.That(cell.Character).IsEqualTo('\u2588');
        await Assert.That(cell.Foreground).IsEqualTo(Color.Red);
    }

    [Test]
    public async Task Render_ShowsHexValue()
    {
        var cp = new ColorPicker { SelectedColor = Color.Red };

        using var buffer = new CellBuffer(20, 5);
        cp.Render(buffer, new Rect(0, 0, 20, 5));

        // Hex starts at (4,1): "#FF0000"
        await Assert.That(buffer.GetCell(4, 1).Character).IsEqualTo('#');
    }

    [Test]
    public async Task Render_DrawsBorder()
    {
        var cp = new ColorPicker();

        using var buffer = new CellBuffer(20, 5);
        cp.Render(buffer, new Rect(0, 0, 20, 5));

        var corner = buffer.GetCell(0, 0);
        await Assert.That(corner.Character).IsNotEqualTo(' ');
        await Assert.That(corner.Character).IsNotEqualTo('\0');
    }

    [Test]
    public async Task Render_ShowsPalette()
    {
        var cp = new ColorPicker();

        using var buffer = new CellBuffer(20, 5);
        cp.Render(buffer, new Rect(0, 0, 20, 5));

        // Palette row at y=2 (border + preview row)
        var paletteCell = buffer.GetCell(1, 2);
        await Assert.That(paletteCell.Character == '\u2588' || paletteCell.Character == '\u25A0').IsTrue();
    }

    #endregion

    #region XAML

    [Test]
    public async Task Xaml_ParsesSelectedColor()
    {
        var xaml = """
            <ColorPicker xmlns="http://schemas.terminalninja.dev/xaml" SelectedColor="#00FF00" />
            """;
        var cp = TerminalXaml.Load<ColorPicker>(xaml);

        await Assert.That(cp.SelectedColor).IsEqualTo(Color.Green);
    }

    #endregion
}
