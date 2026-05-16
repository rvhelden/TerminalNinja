namespace TerminalNinja.Tests.Unit.Controls;

public class NumberPickerTests
{
    #region Default Values

    [Test]
    public async Task Value_Default_IsZero()
    {
        var np = new NumberPicker();
        await Assert.That(np.Value).IsEqualTo(0.0);
    }

    [Test]
    public async Task Minimum_Default_IsZero()
    {
        var np = new NumberPicker();
        await Assert.That(np.Minimum).IsEqualTo(0.0);
    }

    [Test]
    public async Task Maximum_Default_Is100()
    {
        var np = new NumberPicker();
        await Assert.That(np.Maximum).IsEqualTo(100.0);
    }

    [Test]
    public async Task Increment_Default_IsOne()
    {
        var np = new NumberPicker();
        await Assert.That(np.Increment).IsEqualTo(1.0);
    }

    [Test]
    public async Task Focusable_Default_IsTrue()
    {
        var np = new NumberPicker();
        await Assert.That(np.Focusable).IsTrue();
    }

    #endregion

    #region Value Coercion

    [Test]
    public async Task Value_ExceedsMaximum_Clamped()
    {
        var np = new NumberPicker { Maximum = 10 };
        np.Value = 50;
        await Assert.That(np.Value).IsEqualTo(10.0);
    }

    [Test]
    public async Task Value_BelowMinimum_Clamped()
    {
        var np = new NumberPicker { Minimum = 5 };
        np.Value = 2;
        await Assert.That(np.Value).IsEqualTo(5.0);
    }

    #endregion

    #region Keyboard Navigation

    [Test]
    public async Task UpArrow_IncrementsValue()
    {
        var np = new NumberPicker { Value = 5 };
        np.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));
        await Assert.That(np.Value).IsEqualTo(6.0);
    }

    [Test]
    public async Task DownArrow_DecrementsValue()
    {
        var np = new NumberPicker { Value = 5, Minimum = 0 };
        np.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false));
        await Assert.That(np.Value).IsEqualTo(4.0);
    }

    [Test]
    public async Task PageUp_IncrementsByTen()
    {
        var np = new NumberPicker { Value = 5 };
        np.OnKeyEvent(new KeyEvent(ConsoleKey.PageUp, '\0', false, false, false));
        await Assert.That(np.Value).IsEqualTo(15.0);
    }

    [Test]
    public async Task Home_SetsToMinimum()
    {
        var np = new NumberPicker { Value = 50, Minimum = 10 };
        np.OnKeyEvent(new KeyEvent(ConsoleKey.Home, '\0', false, false, false));
        await Assert.That(np.Value).IsEqualTo(10.0);
    }

    [Test]
    public async Task End_SetsToMaximum()
    {
        var np = new NumberPicker { Value = 5, Maximum = 99 };
        np.OnKeyEvent(new KeyEvent(ConsoleKey.End, '\0', false, false, false));
        await Assert.That(np.Value).IsEqualTo(99.0);
    }

    [Test]
    public async Task UpArrow_AtMaximum_StaysAtMax()
    {
        var np = new NumberPicker { Value = 100, Maximum = 100 };
        np.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));
        await Assert.That(np.Value).IsEqualTo(100.0);
    }

    #endregion

    #region Numeric Direct Entry

    [Test]
    public async Task NumericKeys_EnterValue()
    {
        var np = new NumberPicker { Maximum = 999 };
        np.OnKeyEvent(new KeyEvent(ConsoleKey.D4, '4', false, false, false));
        np.OnKeyEvent(new KeyEvent(ConsoleKey.D2, '2', false, false, false));
        np.OnKeyEvent(new KeyEvent(ConsoleKey.Enter, '\r', false, false, false));

        await Assert.That(np.Value).IsEqualTo(42.0);
    }

    [Test]
    public async Task NumericEntry_Backspace_DeletesLastDigit()
    {
        var np = new NumberPicker { Maximum = 999 };
        np.OnKeyEvent(new KeyEvent(ConsoleKey.D1, '1', false, false, false));
        np.OnKeyEvent(new KeyEvent(ConsoleKey.D2, '2', false, false, false));
        np.OnKeyEvent(new KeyEvent(ConsoleKey.Backspace, '\0', false, false, false));
        np.OnKeyEvent(new KeyEvent(ConsoleKey.Enter, '\r', false, false, false));

        await Assert.That(np.Value).IsEqualTo(1.0);
    }

    #endregion

    #region Events

    [Test]
    public async Task ValueChanged_Fires()
    {
        var np = new NumberPicker();
        var fired = false;
        np.ValueChanged += (_, _) => fired = true;

        np.Value = 42;

        await Assert.That(fired).IsTrue();
    }

    [Test]
    public async Task ValueChanged_HasCorrectValues()
    {
        var np = new NumberPicker { Value = 10 };
        double oldVal = 0, newVal = 0;
        np.ValueChanged += (_, e) => { oldVal = e.OldValue; newVal = e.NewValue; };

        np.Value = 20;

        await Assert.That(oldVal).IsEqualTo(10.0);
        await Assert.That(newVal).IsEqualTo(20.0);
    }

    #endregion

    #region Rendering

    [Test]
    public async Task Render_ShowsArrows()
    {
        var np = new NumberPicker { Value = 5 };

        using var buffer = new CellBuffer(20, 3);
        np.Render(buffer, new Rect(0, 0, 20, 3));

        // Left arrow ◀ at (1, 1)
        await Assert.That(buffer.GetCell(1, 1).Codepoint).IsEqualTo('\u25C0');
        // Right arrow ▶ somewhere on row 1
        var hasRightArrow = false;
        for (var x = 0; x < 20; x++)
            if (buffer.GetCell(x, 1).Codepoint == '\u25B6') hasRightArrow = true;
        await Assert.That(hasRightArrow).IsTrue();
    }

    [Test]
    public async Task Render_DrawsBorder()
    {
        var np = new NumberPicker();

        using var buffer = new CellBuffer(20, 3);
        np.Render(buffer, new Rect(0, 0, 20, 3));

        var corner = buffer.GetCell(0, 0);
        await Assert.That(corner.Codepoint).IsNotEqualTo(' ');
        await Assert.That(corner.Codepoint).IsNotEqualTo('\0');
    }

    #endregion

    #region XAML

    [Test]
    public async Task Xaml_ParsesProperties()
    {
        var xaml = """
            <NumberPicker xmlns="http://schemas.terminalninja.dev/xaml"
                          Value="42" Minimum="0" Maximum="100" Increment="5" />
            """;
        var np = TerminalXaml.Load<NumberPicker>(xaml);

        await Assert.That(np.Value).IsEqualTo(42.0);
        await Assert.That(np.Minimum).IsEqualTo(0.0);
        await Assert.That(np.Maximum).IsEqualTo(100.0);
        await Assert.That(np.Increment).IsEqualTo(5.0);
    }

    #endregion
}
