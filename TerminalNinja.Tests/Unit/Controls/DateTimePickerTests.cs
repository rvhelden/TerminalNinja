namespace TerminalNinja.Tests.Unit.Controls;

public class DateTimePickerTests
{
    #region Default Values

    [Test]
    public async Task SelectedDateTime_Default_IsNull()
    {
        var dtp = new DateTimePicker();
        await Assert.That(dtp.SelectedDateTime).IsNull();
    }

    [Test]
    public async Task ShowSeconds_Default_IsFalse()
    {
        var dtp = new DateTimePicker();
        await Assert.That(dtp.ShowSeconds).IsFalse();
    }

    [Test]
    public async Task Icon_Default_IsCalendar()
    {
        var dtp = new DateTimePicker();
        await Assert.That(dtp.Icon).IsEqualTo("\uF073");
    }

    [Test]
    public async Task Focusable_Default_IsTrue()
    {
        var dtp = new DateTimePicker();
        await Assert.That(dtp.Focusable).IsTrue();
    }

    [Test]
    public async Task FocusColor_Default_IsCyan()
    {
        var dtp = new DateTimePicker();
        await Assert.That(dtp.FocusColor).IsEqualTo(Color.Cyan);
    }

    [Test]
    public async Task PlaceholderText_Default_IsSelectDateTime()
    {
        var dtp = new DateTimePicker();
        await Assert.That(dtp.PlaceholderText).IsEqualTo("Select date/time...");
    }

    #endregion

    #region Keyboard Navigation - Date Fields

    [Test]
    public async Task UpArrow_OnYear_IncrementsYear()
    {
        var dtp = new DateTimePicker { SelectedDateTime = new DateTime(2026, 3, 15, 10, 30, 0) };
        dtp.OnGotFocus(); // field 0 = year

        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(dtp.SelectedDateTime!.Value.Year).IsEqualTo(2027);
    }

    [Test]
    public async Task DownArrow_OnMonth_DecrementsMonth()
    {
        var dtp = new DateTimePicker { SelectedDateTime = new DateTime(2026, 3, 15, 10, 30, 0) };
        dtp.OnGotFocus();

        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false)); // month
        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false));

        await Assert.That(dtp.SelectedDateTime!.Value.Month).IsEqualTo(2);
    }

    [Test]
    public async Task UpArrow_OnDay_IncrementsDay()
    {
        var dtp = new DateTimePicker { SelectedDateTime = new DateTime(2026, 3, 15, 10, 30, 0) };
        dtp.OnGotFocus();

        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false)); // month
        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false)); // day
        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(dtp.SelectedDateTime!.Value.Day).IsEqualTo(16);
    }

    #endregion

    #region Keyboard Navigation - Time Fields

    [Test]
    public async Task Navigate_ToHoursField_UpIncrementsHour()
    {
        var dtp = new DateTimePicker { SelectedDateTime = new DateTime(2026, 3, 15, 10, 30, 0) };
        dtp.OnGotFocus();

        // Navigate: year -> month -> day -> hours (3 Right presses)
        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(dtp.SelectedDateTime!.Value.Hour).IsEqualTo(11);
    }

    [Test]
    public async Task Navigate_ToMinutesField_UpIncrementsMinute()
    {
        var dtp = new DateTimePicker { SelectedDateTime = new DateTime(2026, 3, 15, 10, 30, 0) };
        dtp.OnGotFocus();

        // Navigate: year -> month -> day -> hours -> minutes (4 Right presses)
        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(dtp.SelectedDateTime!.Value.Minute).IsEqualTo(31);
    }

    [Test]
    public async Task Navigate_LeftFromHours_GoesToDay()
    {
        var dtp = new DateTimePicker { SelectedDateTime = new DateTime(2026, 3, 15, 10, 30, 0) };
        dtp.OnGotFocus();

        // Go to hours field
        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        // Go back to day
        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.LeftArrow, '\0', false, false, false));
        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(dtp.SelectedDateTime!.Value.Day).IsEqualTo(16);
        await Assert.That(dtp.SelectedDateTime!.Value.Hour).IsEqualTo(10); // hour unchanged
    }

    #endregion

    #region Numeric Direct Entry

    [Test]
    public async Task NumericEntry_OnYearField_SetsYear()
    {
        var dtp = new DateTimePicker { SelectedDateTime = new DateTime(2026, 3, 15, 10, 30, 0) };
        dtp.OnGotFocus();

        // Type "2030"
        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.D2, '2', false, false, false));
        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.D0, '0', false, false, false));
        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.D3, '3', false, false, false));
        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.D0, '0', false, false, false));

        await Assert.That(dtp.SelectedDateTime!.Value.Year).IsEqualTo(2030);
    }

    [Test]
    public async Task NumericEntry_OnHoursField_SetsHour()
    {
        var dtp = new DateTimePicker { SelectedDateTime = new DateTime(2026, 3, 15, 10, 30, 0) };
        dtp.OnGotFocus();

        // Navigate to hours field (3 Right presses)
        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));

        // Type "18"
        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.D1, '1', false, false, false));
        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.D8, '8', false, false, false));

        await Assert.That(dtp.SelectedDateTime!.Value.Hour).IsEqualTo(18);
    }

    #endregion

    #region Auto-Create on Edit

    [Test]
    public async Task OnKeyEvent_NullDateTime_AutoCreatesToToday()
    {
        var dtp = new DateTimePicker();
        dtp.OnGotFocus();

        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(dtp.SelectedDateTime).IsNotNull();
    }

    #endregion

    #region Events

    [Test]
    public async Task SelectedDateTimeChanged_Fires_OnPropertySet()
    {
        var dtp = new DateTimePicker();
        var fired = false;
        dtp.SelectedDateTimeChanged += (_, _) => fired = true;

        dtp.SelectedDateTime = new DateTime(2026, 4, 12, 14, 30, 0);

        await Assert.That(fired).IsTrue();
    }

    [Test]
    public async Task SelectedDateTimeChanged_Fires_OnKeyAdjust()
    {
        var dtp = new DateTimePicker { SelectedDateTime = new DateTime(2026, 3, 15, 10, 30, 0) };
        dtp.OnGotFocus();

        var fired = false;
        dtp.SelectedDateTimeChanged += (_, _) => fired = true;

        dtp.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(fired).IsTrue();
    }

    #endregion

    #region Rendering

    [Test]
    public async Task Render_NullDateTime_Unfocused_ShowsPlaceholder()
    {
        var dtp = new DateTimePicker();

        using var buffer = new CellBuffer(30, 3);
        dtp.Render(buffer, new Rect(0, 0, 30, 3));

        // Placeholder "Select date/time..." starts at x=1, y=1
        await Assert.That(buffer.GetCell(1, 1).Codepoint).IsEqualTo('S');
        await Assert.That(buffer.GetCell(2, 1).Codepoint).IsEqualTo('e');
    }

    [Test]
    public async Task Render_WithDateTime_ShowsFormattedValue()
    {
        var dtp = new DateTimePicker { SelectedDateTime = new DateTime(2026, 3, 15, 14, 30, 0) };

        using var buffer = new CellBuffer(30, 3);
        dtp.Render(buffer, new Rect(0, 0, 30, 3));

        // "2026-03-15 14:30" starting at x=1, y=1
        await Assert.That(buffer.GetCell(1, 1).Codepoint).IsEqualTo('2');
        await Assert.That(buffer.GetCell(2, 1).Codepoint).IsEqualTo('0');
        await Assert.That(buffer.GetCell(3, 1).Codepoint).IsEqualTo('2');
        await Assert.That(buffer.GetCell(4, 1).Codepoint).IsEqualTo('6');
        await Assert.That(buffer.GetCell(5, 1).Codepoint).IsEqualTo('-');
    }

    [Test]
    public async Task Render_DrawsBorder()
    {
        var dtp = new DateTimePicker();

        using var buffer = new CellBuffer(30, 3);
        dtp.Render(buffer, new Rect(0, 0, 30, 3));

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
            <DateTimePicker xmlns="http://schemas.terminalninja.dev/xaml"
                            ShowSeconds="True"
                            PlaceholderText="Pick date/time" />
            """;
        var dtp = TerminalXaml.Load<DateTimePicker>(xaml);

        await Assert.That(dtp.ShowSeconds).IsTrue();
        await Assert.That(dtp.PlaceholderText).IsEqualTo("Pick date/time");
    }

    [Test]
    public async Task Xaml_DefaultValues_Preserved()
    {
        var xaml = """
            <DateTimePicker xmlns="http://schemas.terminalninja.dev/xaml" />
            """;
        var dtp = TerminalXaml.Load<DateTimePicker>(xaml);

        await Assert.That(dtp.SelectedDateTime).IsNull();
        await Assert.That(dtp.ShowSeconds).IsFalse();
    }

    #endregion
}
