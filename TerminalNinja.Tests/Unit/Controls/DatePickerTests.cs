namespace TerminalNinja.Tests.Unit.Controls;

public class DatePickerTests
{
    #region Default Values

    [Test]
    public async Task SelectedDate_Default_IsNull()
    {
        var dp = new DatePicker();
        await Assert.That(dp.SelectedDate).IsNull();
    }

    [Test]
    public async Task Focusable_Default_IsTrue()
    {
        var dp = new DatePicker();
        await Assert.That(dp.Focusable).IsTrue();
    }

    [Test]
    public async Task DateFormat_Default_IsYmd()
    {
        var dp = new DatePicker();
        await Assert.That(dp.DateFormat).IsEqualTo("yyyy-MM-dd");
    }

    [Test]
    public async Task Icon_Default_IsCalendar()
    {
        var dp = new DatePicker();
        await Assert.That(dp.Icon).IsEqualTo("\uF073");
    }

    [Test]
    public async Task FocusColor_Default_IsCyan()
    {
        var dp = new DatePicker();
        await Assert.That(dp.FocusColor).IsEqualTo(Color.Cyan);
    }

    [Test]
    public async Task HoverColor_Default_IsYellow()
    {
        var dp = new DatePicker();
        await Assert.That(dp.HoverColor).IsEqualTo(Color.Yellow);
    }

    [Test]
    public async Task PlaceholderText_Default_IsSelectDate()
    {
        var dp = new DatePicker();
        await Assert.That(dp.PlaceholderText).IsEqualTo("Select date...");
    }

    #endregion

    #region Keyboard Navigation - Field Movement

    [Test]
    public async Task RightArrow_MovesFromYearToMonth()
    {
        var dp = new DatePicker { SelectedDate = new DateTime(2026, 3, 15) };
        dp.OnGotFocus(); // resets _editField to 0 (year)

        dp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        // Now on field 1 (month). Up should increment month.
        dp.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(dp.SelectedDate!.Value.Month).IsEqualTo(4);
    }

    [Test]
    public async Task RightArrow_Twice_MovesToDay()
    {
        var dp = new DatePicker { SelectedDate = new DateTime(2026, 3, 15) };
        dp.OnGotFocus();

        dp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        dp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        // Now on field 2 (day). Up should increment day.
        dp.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(dp.SelectedDate!.Value.Day).IsEqualTo(16);
    }

    [Test]
    public async Task LeftArrow_MovesFromMonthToYear()
    {
        var dp = new DatePicker { SelectedDate = new DateTime(2026, 3, 15) };
        dp.OnGotFocus();

        dp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false)); // month
        dp.OnKeyEvent(new KeyEvent(ConsoleKey.LeftArrow, '\0', false, false, false));  // back to year
        dp.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(dp.SelectedDate!.Value.Year).IsEqualTo(2027);
    }

    [Test]
    public async Task LeftArrow_AtYear_StaysAtYear()
    {
        var dp = new DatePicker { SelectedDate = new DateTime(2026, 3, 15) };
        dp.OnGotFocus();

        dp.OnKeyEvent(new KeyEvent(ConsoleKey.LeftArrow, '\0', false, false, false));
        dp.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(dp.SelectedDate!.Value.Year).IsEqualTo(2027);
    }

    [Test]
    public async Task RightArrow_AtDay_StaysAtDay()
    {
        var dp = new DatePicker { SelectedDate = new DateTime(2026, 3, 15) };
        dp.OnGotFocus();

        dp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        dp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        dp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false)); // still day
        dp.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(dp.SelectedDate!.Value.Day).IsEqualTo(16);
    }

    #endregion

    #region Keyboard Navigation - Value Adjustment

    [Test]
    public async Task UpArrow_OnYear_IncrementsYear()
    {
        var dp = new DatePicker { SelectedDate = new DateTime(2026, 3, 15) };
        dp.OnGotFocus(); // field 0 = year

        dp.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(dp.SelectedDate!.Value.Year).IsEqualTo(2027);
    }

    [Test]
    public async Task DownArrow_OnYear_DecrementsYear()
    {
        var dp = new DatePicker { SelectedDate = new DateTime(2026, 3, 15) };
        dp.OnGotFocus();

        dp.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false));

        await Assert.That(dp.SelectedDate!.Value.Year).IsEqualTo(2025);
    }

    [Test]
    public async Task UpArrow_OnDay_IncrementsDay()
    {
        var dp = new DatePicker { SelectedDate = new DateTime(2026, 3, 15) };
        dp.OnGotFocus();

        // Navigate to day field
        dp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        dp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        dp.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(dp.SelectedDate!.Value.Day).IsEqualTo(16);
    }

    [Test]
    public async Task DownArrow_OnDay_DecrementsDay()
    {
        var dp = new DatePicker { SelectedDate = new DateTime(2026, 3, 15) };
        dp.OnGotFocus();

        dp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        dp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        dp.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false));

        await Assert.That(dp.SelectedDate!.Value.Day).IsEqualTo(14);
    }

    [Test]
    public async Task UpArrow_OnMonth_IncrementsMonth()
    {
        var dp = new DatePicker { SelectedDate = new DateTime(2026, 6, 15) };
        dp.OnGotFocus();

        dp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        dp.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(dp.SelectedDate!.Value.Month).IsEqualTo(7);
    }

    #endregion

    #region Numeric Direct Entry

    [Test]
    public async Task NumericEntry_OnDayField_SetsDay()
    {
        var dp = new DatePicker { SelectedDate = new DateTime(2026, 3, 1) };
        dp.OnGotFocus();

        // Navigate to day field
        dp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        dp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));

        // Type "15"
        dp.OnKeyEvent(new KeyEvent(ConsoleKey.D1, '1', false, false, false));
        dp.OnKeyEvent(new KeyEvent(ConsoleKey.D5, '5', false, false, false));

        await Assert.That(dp.SelectedDate!.Value.Day).IsEqualTo(15);
    }

    [Test]
    public async Task NumericEntry_OnMonthField_SetsMonth()
    {
        var dp = new DatePicker { SelectedDate = new DateTime(2026, 1, 15) };
        dp.OnGotFocus();

        dp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false)); // month

        // Type "08"
        dp.OnKeyEvent(new KeyEvent(ConsoleKey.D0, '0', false, false, false));
        dp.OnKeyEvent(new KeyEvent(ConsoleKey.D8, '8', false, false, false));

        await Assert.That(dp.SelectedDate!.Value.Month).IsEqualTo(8);
    }

    [Test]
    public async Task NumericEntry_OnYearField_SetsYear()
    {
        var dp = new DatePicker { SelectedDate = new DateTime(2026, 3, 15) };
        dp.OnGotFocus(); // field 0 = year

        // Type "2030"
        dp.OnKeyEvent(new KeyEvent(ConsoleKey.D2, '2', false, false, false));
        dp.OnKeyEvent(new KeyEvent(ConsoleKey.D0, '0', false, false, false));
        dp.OnKeyEvent(new KeyEvent(ConsoleKey.D3, '3', false, false, false));
        dp.OnKeyEvent(new KeyEvent(ConsoleKey.D0, '0', false, false, false));

        await Assert.That(dp.SelectedDate!.Value.Year).IsEqualTo(2030);
    }

    #endregion

    #region Auto-Create on Edit

    [Test]
    public async Task OnKeyEvent_NullDate_AutoCreatesToday()
    {
        var dp = new DatePicker();
        dp.OnGotFocus();

        dp.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(dp.SelectedDate).IsNotNull();
    }

    #endregion

    #region Events

    [Test]
    public async Task SelectedDateChanged_Fires_OnPropertySet()
    {
        var dp = new DatePicker();
        var fired = false;
        dp.SelectedDateChanged += (_, _) => fired = true;

        dp.SelectedDate = new DateTime(2026, 4, 12);

        await Assert.That(fired).IsTrue();
    }

    [Test]
    public async Task SelectedDateChanged_Fires_OnKeyAdjust()
    {
        var dp = new DatePicker { SelectedDate = new DateTime(2026, 3, 15) };
        dp.OnGotFocus();

        var fired = false;
        dp.SelectedDateChanged += (_, _) => fired = true;

        dp.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(fired).IsTrue();
    }

    #endregion

    #region Rendering

    [Test]
    public async Task Render_NullDate_Unfocused_ShowsPlaceholder()
    {
        var dp = new DatePicker();

        using var buffer = new CellBuffer(30, 3);
        dp.Render(buffer, new Rect(0, 0, 30, 3));

        // Placeholder "Select date..." should appear starting at x=1, y=1
        await Assert.That(buffer.GetCell(1, 1).Codepoint).IsEqualTo('S');
        await Assert.That(buffer.GetCell(2, 1).Codepoint).IsEqualTo('e');
    }

    [Test]
    public async Task Render_WithDate_ShowsFormattedDate()
    {
        var dp = new DatePicker { SelectedDate = new DateTime(2026, 3, 15) };

        using var buffer = new CellBuffer(30, 3);
        dp.Render(buffer, new Rect(0, 0, 30, 3));

        // Date "2026-03-15" should appear starting at x=1, y=1
        await Assert.That(buffer.GetCell(1, 1).Codepoint).IsEqualTo('2');
        await Assert.That(buffer.GetCell(2, 1).Codepoint).IsEqualTo('0');
        await Assert.That(buffer.GetCell(3, 1).Codepoint).IsEqualTo('2');
        await Assert.That(buffer.GetCell(4, 1).Codepoint).IsEqualTo('6');
        await Assert.That(buffer.GetCell(5, 1).Codepoint).IsEqualTo('-');
    }

    [Test]
    public async Task Render_DrawsBorder()
    {
        var dp = new DatePicker();

        using var buffer = new CellBuffer(20, 3);
        dp.Render(buffer, new Rect(0, 0, 20, 3));

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
            <DatePicker xmlns="http://schemas.terminalninja.dev/xaml"
                        DateFormat="dd/MM/yyyy"
                        PlaceholderText="Pick a date" />
            """;
        var dp = TerminalXaml.Load<DatePicker>(xaml);

        await Assert.That(dp.DateFormat).IsEqualTo("dd/MM/yyyy");
        await Assert.That(dp.PlaceholderText).IsEqualTo("Pick a date");
    }

    [Test]
    public async Task Xaml_DefaultValues_Preserved()
    {
        var xaml = """
            <DatePicker xmlns="http://schemas.terminalninja.dev/xaml" />
            """;
        var dp = TerminalXaml.Load<DatePicker>(xaml);

        await Assert.That(dp.SelectedDate).IsNull();
        await Assert.That(dp.DateFormat).IsEqualTo("yyyy-MM-dd");
    }

    #endregion
}
