namespace TerminalNinja.Tests.Unit.Controls;

public class TimePickerTests
{
    #region Default Values

    [Test]
    public async Task SelectedTime_Default_IsNull()
    {
        var tp = new TimePicker();
        await Assert.That(tp.SelectedTime).IsNull();
    }

    [Test]
    public async Task ShowSeconds_Default_IsFalse()
    {
        var tp = new TimePicker();
        await Assert.That(tp.ShowSeconds).IsFalse();
    }

    [Test]
    public async Task Icon_Default_IsClock()
    {
        var tp = new TimePicker();
        await Assert.That(tp.Icon).IsEqualTo("\uF017");
    }

    [Test]
    public async Task Focusable_Default_IsTrue()
    {
        var tp = new TimePicker();
        await Assert.That(tp.Focusable).IsTrue();
    }

    [Test]
    public async Task FocusColor_Default_IsCyan()
    {
        var tp = new TimePicker();
        await Assert.That(tp.FocusColor).IsEqualTo(Color.Cyan);
    }

    [Test]
    public async Task HoverColor_Default_IsYellow()
    {
        var tp = new TimePicker();
        await Assert.That(tp.HoverColor).IsEqualTo(Color.Yellow);
    }

    [Test]
    public async Task PlaceholderText_Default_IsSelectTime()
    {
        var tp = new TimePicker();
        await Assert.That(tp.PlaceholderText).IsEqualTo("Select time...");
    }

    #endregion

    #region Keyboard Navigation - Value Adjustment

    [Test]
    public async Task UpArrow_OnHours_IncrementsHour()
    {
        var tp = new TimePicker { SelectedTime = new TimeSpan(10, 30, 0) };
        tp.OnGotFocus(); // field 0 = hours

        tp.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(tp.SelectedTime!.Value.Hours).IsEqualTo(11);
    }

    [Test]
    public async Task DownArrow_OnHours_DecrementsHour()
    {
        var tp = new TimePicker { SelectedTime = new TimeSpan(10, 30, 0) };
        tp.OnGotFocus();

        tp.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false));

        await Assert.That(tp.SelectedTime!.Value.Hours).IsEqualTo(9);
    }

    [Test]
    public async Task UpArrow_OnHours_WrapsAt23To0()
    {
        var tp = new TimePicker { SelectedTime = new TimeSpan(23, 0, 0) };
        tp.OnGotFocus();

        tp.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(tp.SelectedTime!.Value.Hours).IsEqualTo(0);
    }

    [Test]
    public async Task DownArrow_OnMinutes_WrapsAt0To59()
    {
        var tp = new TimePicker { SelectedTime = new TimeSpan(10, 0, 0) };
        tp.OnGotFocus();

        tp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false)); // minutes
        tp.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false));

        await Assert.That(tp.SelectedTime!.Value.Minutes).IsEqualTo(59);
    }

    [Test]
    public async Task UpArrow_OnMinutes_IncrementsMinute()
    {
        var tp = new TimePicker { SelectedTime = new TimeSpan(10, 30, 0) };
        tp.OnGotFocus();

        tp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false)); // minutes
        tp.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(tp.SelectedTime!.Value.Minutes).IsEqualTo(31);
    }

    [Test]
    public async Task DownArrow_OnHours_WrapsAt0To23()
    {
        var tp = new TimePicker { SelectedTime = new TimeSpan(0, 30, 0) };
        tp.OnGotFocus();

        tp.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false));

        await Assert.That(tp.SelectedTime!.Value.Hours).IsEqualTo(23);
    }

    #endregion

    #region Keyboard Navigation - Field Movement

    [Test]
    public async Task RightArrow_MovesFromHoursToMinutes()
    {
        var tp = new TimePicker { SelectedTime = new TimeSpan(10, 30, 0) };
        tp.OnGotFocus();

        tp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        tp.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(tp.SelectedTime!.Value.Minutes).IsEqualTo(31);
        await Assert.That(tp.SelectedTime!.Value.Hours).IsEqualTo(10);
    }

    [Test]
    public async Task LeftArrow_MovesFromMinutesToHours()
    {
        var tp = new TimePicker { SelectedTime = new TimeSpan(10, 30, 0) };
        tp.OnGotFocus();

        tp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));  // minutes
        tp.OnKeyEvent(new KeyEvent(ConsoleKey.LeftArrow, '\0', false, false, false));   // back to hours
        tp.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(tp.SelectedTime!.Value.Hours).IsEqualTo(11);
    }

    #endregion

    #region Numeric Direct Entry

    [Test]
    public async Task NumericEntry_OnHoursField_SetsHours()
    {
        var tp = new TimePicker { SelectedTime = new TimeSpan(0, 0, 0) };
        tp.OnGotFocus(); // field 0 = hours

        tp.OnKeyEvent(new KeyEvent(ConsoleKey.D1, '1', false, false, false));
        tp.OnKeyEvent(new KeyEvent(ConsoleKey.D4, '4', false, false, false));

        await Assert.That(tp.SelectedTime!.Value.Hours).IsEqualTo(14);
    }

    [Test]
    public async Task NumericEntry_OnMinutesField_SetsMinutes()
    {
        var tp = new TimePicker { SelectedTime = new TimeSpan(10, 0, 0) };
        tp.OnGotFocus();

        tp.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false)); // minutes

        tp.OnKeyEvent(new KeyEvent(ConsoleKey.D4, '4', false, false, false));
        tp.OnKeyEvent(new KeyEvent(ConsoleKey.D5, '5', false, false, false));

        await Assert.That(tp.SelectedTime!.Value.Minutes).IsEqualTo(45);
    }

    #endregion

    #region Auto-Create on Edit

    [Test]
    public async Task OnKeyEvent_NullTime_AutoCreatesToZero()
    {
        var tp = new TimePicker();
        tp.OnGotFocus();

        tp.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(tp.SelectedTime).IsNotNull();
    }

    #endregion

    #region Events

    [Test]
    public async Task SelectedTimeChanged_Fires_OnPropertySet()
    {
        var tp = new TimePicker();
        var fired = false;
        tp.SelectedTimeChanged += (_, _) => fired = true;

        tp.SelectedTime = new TimeSpan(14, 30, 0);

        await Assert.That(fired).IsTrue();
    }

    [Test]
    public async Task SelectedTimeChanged_Fires_OnKeyAdjust()
    {
        var tp = new TimePicker { SelectedTime = new TimeSpan(10, 0, 0) };
        tp.OnGotFocus();

        var fired = false;
        tp.SelectedTimeChanged += (_, _) => fired = true;

        tp.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(fired).IsTrue();
    }

    #endregion

    #region Rendering

    [Test]
    public async Task Render_NullTime_Unfocused_ShowsPlaceholder()
    {
        var tp = new TimePicker();

        using var buffer = new CellBuffer(20, 3);
        tp.Render(buffer, new Rect(0, 0, 20, 3));

        // Placeholder "Select time..." starts at x=1, y=1
        await Assert.That(buffer.GetCell(1, 1).Character).IsEqualTo('S');
        await Assert.That(buffer.GetCell(2, 1).Character).IsEqualTo('e');
    }

    [Test]
    public async Task Render_WithTime_ShowsFormattedTime()
    {
        var tp = new TimePicker { SelectedTime = new TimeSpan(14, 30, 0) };

        using var buffer = new CellBuffer(20, 3);
        tp.Render(buffer, new Rect(0, 0, 20, 3));

        // "14:30" starting at x=1, y=1
        await Assert.That(buffer.GetCell(1, 1).Character).IsEqualTo('1');
        await Assert.That(buffer.GetCell(2, 1).Character).IsEqualTo('4');
        await Assert.That(buffer.GetCell(3, 1).Character).IsEqualTo(':');
        await Assert.That(buffer.GetCell(4, 1).Character).IsEqualTo('3');
        await Assert.That(buffer.GetCell(5, 1).Character).IsEqualTo('0');
    }

    [Test]
    public async Task Render_DrawsBorder()
    {
        var tp = new TimePicker();

        using var buffer = new CellBuffer(20, 3);
        tp.Render(buffer, new Rect(0, 0, 20, 3));

        var corner = buffer.GetCell(0, 0);
        await Assert.That(corner.Character).IsNotEqualTo(' ');
        await Assert.That(corner.Character).IsNotEqualTo('\0');
    }

    #endregion

    #region XAML

    [Test]
    public async Task Xaml_ParsesProperties()
    {
        var xaml = """
            <TimePicker xmlns="http://schemas.terminalninja.dev/xaml"
                        ShowSeconds="True"
                        PlaceholderText="Pick a time" />
            """;
        var tp = TerminalXaml.Load<TimePicker>(xaml);

        await Assert.That(tp.ShowSeconds).IsTrue();
        await Assert.That(tp.PlaceholderText).IsEqualTo("Pick a time");
    }

    [Test]
    public async Task Xaml_DefaultValues_Preserved()
    {
        var xaml = """
            <TimePicker xmlns="http://schemas.terminalninja.dev/xaml" />
            """;
        var tp = TerminalXaml.Load<TimePicker>(xaml);

        await Assert.That(tp.SelectedTime).IsNull();
        await Assert.That(tp.ShowSeconds).IsFalse();
    }

    #endregion
}
