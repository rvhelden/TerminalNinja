namespace TerminalNinja.Tests.Unit.Controls;

public class CheckBoxTests
{
    #region Default Values

    [Test]
    public async Task IsChecked_Default_IsFalse()
    {
        var cb = new CheckBox();
        await Assert.That(cb.IsChecked).IsFalse();
    }

    [Test]
    public async Task Focusable_Default_IsTrue()
    {
        var cb = new CheckBox();
        await Assert.That(cb.Focusable).IsTrue();
    }

    [Test]
    public async Task FocusColor_Default_IsCyan()
    {
        var cb = new CheckBox();
        await Assert.That(cb.FocusColor).IsEqualTo(Color.Cyan);
    }

    #endregion

    #region Toggle Behavior

    [Test]
    public async Task OnKeyEvent_Space_TogglesIsChecked()
    {
        var cb = new CheckBox();

        cb.OnKeyEvent(new KeyEvent(ConsoleKey.Spacebar, ' ', false, false, false));

        await Assert.That(cb.IsChecked).IsTrue();
    }

    [Test]
    public async Task OnKeyEvent_Enter_TogglesIsChecked()
    {
        var cb = new CheckBox();

        cb.OnKeyEvent(new KeyEvent(ConsoleKey.Enter, '\r', false, false, false));

        await Assert.That(cb.IsChecked).IsTrue();
    }

    [Test]
    public async Task OnKeyEvent_Space_TogglesBackToFalse()
    {
        var cb = new CheckBox { IsChecked = true };

        cb.OnKeyEvent(new KeyEvent(ConsoleKey.Spacebar, ' ', false, false, false));

        await Assert.That(cb.IsChecked).IsFalse();
    }

    [Test]
    public async Task OnMouseEvent_LeftClick_TogglesIsChecked()
    {
        var cb = new CheckBox();

        cb.OnMouseEvent(new MouseEvent(0, 0, MouseButton.Left, MouseAction.Press));

        await Assert.That(cb.IsChecked).IsTrue();
    }

    [Test]
    public async Task Disabled_DoesNotToggle()
    {
        var cb = new CheckBox { IsEnabled = false };

        cb.OnKeyEvent(new KeyEvent(ConsoleKey.Spacebar, ' ', false, false, false));

        await Assert.That(cb.IsChecked).IsFalse();
    }

    #endregion

    #region Events

    [Test]
    public async Task Checked_RaisedWhenIsCheckedBecomesTrue()
    {
        var cb = new CheckBox();
        var raised = false;
        cb.Checked += () => raised = true;

        cb.IsChecked = true;

        await Assert.That(raised).IsTrue();
    }

    [Test]
    public async Task Unchecked_RaisedWhenIsCheckedBecomesFalse()
    {
        var cb = new CheckBox { IsChecked = true };
        var raised = false;
        cb.Unchecked += () => raised = true;

        cb.IsChecked = false;

        await Assert.That(raised).IsTrue();
    }

    [Test]
    public async Task Click_RaisedOnToggle()
    {
        var cb = new CheckBox();
        var clicked = false;
        cb.Click += () => clicked = true;

        cb.OnKeyEvent(new KeyEvent(ConsoleKey.Spacebar, ' ', false, false, false));

        await Assert.That(clicked).IsTrue();
    }

    #endregion

    #region Rendering

    [Test]
    public async Task Render_Unchecked_ShowsBrackets()
    {
        var cb = new CheckBox { Content = "Option" };

        using var buffer = new CellBuffer(20, 1);
        cb.Render(buffer, new Rect(0, 0, 20, 1));

        await Assert.That(buffer.GetCell(0, 0).Codepoint).IsEqualTo('[');
        await Assert.That(buffer.GetCell(1, 0).Codepoint).IsEqualTo(' ');
        await Assert.That(buffer.GetCell(2, 0).Codepoint).IsEqualTo(']');
    }

    [Test]
    public async Task Render_Checked_ShowsX()
    {
        var cb = new CheckBox { IsChecked = true, Content = "Option" };

        using var buffer = new CellBuffer(20, 1);
        cb.Render(buffer, new Rect(0, 0, 20, 1));

        await Assert.That(buffer.GetCell(0, 0).Codepoint).IsEqualTo('[');
        await Assert.That(buffer.GetCell(1, 0).Codepoint).IsEqualTo('x');
        await Assert.That(buffer.GetCell(2, 0).Codepoint).IsEqualTo(']');
    }

    [Test]
    public async Task Render_Content_RendersAfterIndicator()
    {
        var cb = new CheckBox { Content = "Test" };

        using var buffer = new CellBuffer(20, 1);
        cb.Render(buffer, new Rect(0, 0, 20, 1));

        // Content starts at offset 4 (after "[ ] ")
        await Assert.That(buffer.GetCell(4, 0).Codepoint).IsEqualTo('T');
    }

    [Test]
    public async Task GetPreferredSize_IncludesIndicatorWidth()
    {
        var cb = new CheckBox { Content = "AB" };
        var size = cb.GetPreferredSize(new Rect(0, 0, 40, 10));

        // 4 (indicator) + content width
        await Assert.That(size.Width).IsGreaterThanOrEqualTo(4);
        await Assert.That(size.Height).IsGreaterThanOrEqualTo(1);
    }

    #endregion

    #region XAML

    [Test]
    public async Task Xaml_ParsesIsChecked()
    {
        var xaml = """
            <CheckBox xmlns="http://schemas.terminalninja.dev/xaml"
                      IsChecked="True" Content="Option A" />
            """;

        var cb = TerminalXaml.Load<CheckBox>(xaml);

        await Assert.That(cb.IsChecked).IsTrue();
    }

    #endregion
}
