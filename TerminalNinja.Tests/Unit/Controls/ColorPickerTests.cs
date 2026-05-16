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

    [Test]
    public async Task GetPreferredSize_HeightIs3()
    {
        var cp = new ColorPicker();
        var size = cp.GetPreferredSize(new Rect(0, 0, 40, 10));
        await Assert.That(size.Height).IsEqualTo(3);
    }

    #endregion

    #region Hex Entry

    [Test]
    public async Task HexDigits_SetColor()
    {
        var cp = new ColorPicker();
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

        cp.OnKeyEvent(new KeyEvent(ConsoleKey.A, 'A', false, false, false));
        cp.OnKeyEvent(new KeyEvent(ConsoleKey.B, 'B', false, false, false));
        cp.OnKeyEvent(new KeyEvent(ConsoleKey.Escape, '\0', false, false, false));

        await Assert.That(cp.SelectedColor).IsEqualTo(original);
    }

    [Test]
    public async Task Backspace_DeletesHexDigit()
    {
        var cp = new ColorPicker();
        cp.OnKeyEvent(new KeyEvent(ConsoleKey.A, 'A', false, false, false));
        cp.OnKeyEvent(new KeyEvent(ConsoleKey.Backspace, '\0', false, false, false));

        // Should exit hex mode — color unchanged
        await Assert.That(cp.SelectedColor).IsEqualTo(Color.White);
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

        using var buffer = new CellBuffer(20, 3);
        cp.Render(buffer, new Rect(0, 0, 20, 3));

        var cell = buffer.GetCell(1, 1);
        await Assert.That(cell.Codepoint).IsEqualTo('\u2588');
        await Assert.That(cell.Foreground).IsEqualTo(Color.Red);
    }

    [Test]
    public async Task Render_ShowsHexValue()
    {
        var cp = new ColorPicker { SelectedColor = Color.Red };

        using var buffer = new CellBuffer(20, 3);
        cp.Render(buffer, new Rect(0, 0, 20, 3));

        await Assert.That(buffer.GetCell(4, 1).Codepoint).IsEqualTo('#');
    }

    [Test]
    public async Task Render_DrawsBorder()
    {
        var cp = new ColorPicker();

        using var buffer = new CellBuffer(20, 3);
        cp.Render(buffer, new Rect(0, 0, 20, 3));

        var corner = buffer.GetCell(0, 0);
        await Assert.That(corner.Codepoint).IsNotEqualTo(' ');
        await Assert.That(corner.Codepoint).IsNotEqualTo('\0');
    }

    #endregion

    #region Dialog

    [Test]
    public async Task ColorPickerDialog_Enter_ClosesWithTrue()
    {
        var dialog = new ColorPickerDialog(Color.Red);

        dialog.OnKeyEvent(new KeyEvent(ConsoleKey.Enter, '\r', false, false, false));

        await Assert.That(dialog.DialogResult).IsNotNull();
        await Assert.That(dialog.DialogResult!.Value).IsTrue();
    }

    [Test]
    public async Task ColorPickerDialog_Escape_ClosesWithFalse()
    {
        var dialog = new ColorPickerDialog(Color.Red);

        dialog.OnKeyEvent(new KeyEvent(ConsoleKey.Escape, '\0', false, false, false));

        await Assert.That(dialog.DialogResult).IsNotNull();
        await Assert.That(dialog.DialogResult!.Value).IsFalse();
    }

    [Test]
    public async Task ColorPickerDialog_ArrowNavigation_ChangesColor()
    {
        var dialog = new ColorPickerDialog(Color.Red);
        var initial = dialog.SelectedColor;

        // Switch to SL mode (Tab), then adjust saturation
        dialog.OnKeyEvent(new KeyEvent(ConsoleKey.Tab, '\0', false, false, false));
        dialog.OnKeyEvent(new KeyEvent(ConsoleKey.LeftArrow, '\0', false, false, false));

        await Assert.That(dialog.SelectedColor).IsNotEqualTo(initial);
    }

    [Test]
    public async Task ColorPickerDialog_HexEntry_SetsColor()
    {
        var dialog = new ColorPickerDialog(Color.White);

        dialog.OnKeyEvent(new KeyEvent(ConsoleKey.D0, '0', false, false, false));
        dialog.OnKeyEvent(new KeyEvent(ConsoleKey.D0, '0', false, false, false));
        dialog.OnKeyEvent(new KeyEvent(ConsoleKey.F, 'F', false, false, false));
        dialog.OnKeyEvent(new KeyEvent(ConsoleKey.F, 'F', false, false, false));
        dialog.OnKeyEvent(new KeyEvent(ConsoleKey.D0, '0', false, false, false));
        dialog.OnKeyEvent(new KeyEvent(ConsoleKey.D0, '0', false, false, false));

        await Assert.That(dialog.SelectedColor).IsEqualTo(Color.Green);
    }

    [Test]
    public async Task ColorPickerDialog_Render_DoesNotThrow()
    {
        var dialog = new ColorPickerDialog(Color.Cyan);

        using var buffer = new CellBuffer(40, 16);
        dialog.Render(buffer, new Rect(0, 0, 40, 16));

        await Assert.That(buffer.GetCell(0, 0).Codepoint).IsNotEqualTo('\0');
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
