namespace TerminalNinja.Tests.Unit.Controls;

public class RadioButtonTests
{
    #region Default Values

    [Test]
    public async Task IsChecked_Default_IsFalse()
    {
        var rb = new RadioButton();
        await Assert.That(rb.IsChecked).IsFalse();
    }

    [Test]
    public async Task GroupName_Default_IsEmpty()
    {
        var rb = new RadioButton();
        await Assert.That(rb.GroupName).IsEqualTo("");
    }

    [Test]
    public async Task Focusable_Default_IsTrue()
    {
        var rb = new RadioButton();
        await Assert.That(rb.Focusable).IsTrue();
    }

    #endregion

    #region Check Behavior

    [Test]
    public async Task OnKeyEvent_Space_ChecksRadioButton()
    {
        var rb = new RadioButton();

        rb.OnKeyEvent(new KeyEvent(ConsoleKey.Spacebar, ' ', false, false, false));

        await Assert.That(rb.IsChecked).IsTrue();
    }

    [Test]
    public async Task OnMouseEvent_LeftClick_ChecksRadioButton()
    {
        var rb = new RadioButton();

        rb.OnMouseEvent(new MouseEvent(0, 0, MouseButton.Left, MouseAction.Press));

        await Assert.That(rb.IsChecked).IsTrue();
    }

    [Test]
    public async Task Space_OnAlreadyChecked_StaysChecked()
    {
        // RadioButton doesn't toggle off (WPF behavior)
        var rb = new RadioButton { IsChecked = true };

        rb.OnKeyEvent(new KeyEvent(ConsoleKey.Spacebar, ' ', false, false, false));

        await Assert.That(rb.IsChecked).IsTrue();
    }

    [Test]
    public async Task Disabled_DoesNotCheck()
    {
        var rb = new RadioButton { IsEnabled = false };

        rb.OnKeyEvent(new KeyEvent(ConsoleKey.Spacebar, ' ', false, false, false));

        await Assert.That(rb.IsChecked).IsFalse();
    }

    #endregion

    #region Group Behavior

    [Test]
    public async Task CheckingSibling_UnchecksOther_SameGroup()
    {
        var panel = new StackPanel();
        var rb1 = new RadioButton { GroupName = "G1", IsChecked = true };
        var rb2 = new RadioButton { GroupName = "G1" };
        panel.Children.Add(rb1);
        panel.Children.Add(rb2);

        rb2.IsChecked = true;

        await Assert.That(rb1.IsChecked).IsFalse();
        await Assert.That(rb2.IsChecked).IsTrue();
    }

    [Test]
    public async Task DifferentGroupName_DoesNotAffectOther()
    {
        var panel = new StackPanel();
        var rb1 = new RadioButton { GroupName = "G1", IsChecked = true };
        var rb2 = new RadioButton { GroupName = "G2" };
        panel.Children.Add(rb1);
        panel.Children.Add(rb2);

        rb2.IsChecked = true;

        await Assert.That(rb1.IsChecked).IsTrue();
        await Assert.That(rb2.IsChecked).IsTrue();
    }

    [Test]
    public async Task EmptyGroupName_FormsImplicitGroup()
    {
        var panel = new StackPanel();
        var rb1 = new RadioButton { IsChecked = true };
        var rb2 = new RadioButton();
        panel.Children.Add(rb1);
        panel.Children.Add(rb2);

        rb2.IsChecked = true;

        await Assert.That(rb1.IsChecked).IsFalse();
        await Assert.That(rb2.IsChecked).IsTrue();
    }

    [Test]
    public async Task ThreeRadioButtons_OnlyOneChecked()
    {
        var panel = new StackPanel();
        var rb1 = new RadioButton { GroupName = "G" };
        var rb2 = new RadioButton { GroupName = "G" };
        var rb3 = new RadioButton { GroupName = "G" };
        panel.Children.Add(rb1);
        panel.Children.Add(rb2);
        panel.Children.Add(rb3);

        rb1.IsChecked = true;
        await Assert.That(rb1.IsChecked).IsTrue();

        rb3.IsChecked = true;
        await Assert.That(rb1.IsChecked).IsFalse();
        await Assert.That(rb2.IsChecked).IsFalse();
        await Assert.That(rb3.IsChecked).IsTrue();
    }

    [Test]
    public async Task NonRadioButtonSiblings_Ignored()
    {
        var panel = new StackPanel();
        var rb1 = new RadioButton { IsChecked = true };
        var tb = new TextBlock { Text = "label" };
        var rb2 = new RadioButton();
        panel.Children.Add(rb1);
        panel.Children.Add(tb);
        panel.Children.Add(rb2);

        rb2.IsChecked = true;

        await Assert.That(rb1.IsChecked).IsFalse();
        await Assert.That(rb2.IsChecked).IsTrue();
    }

    #endregion

    #region Events

    [Test]
    public async Task Checked_RaisedWhenIsCheckedBecomesTrue()
    {
        var rb = new RadioButton();
        var raised = false;
        rb.Checked += () => raised = true;

        rb.IsChecked = true;

        await Assert.That(raised).IsTrue();
    }

    [Test]
    public async Task Unchecked_RaisedWhenIsCheckedBecomesFalse()
    {
        var rb = new RadioButton { IsChecked = true };
        var raised = false;
        rb.Unchecked += () => raised = true;

        rb.IsChecked = false;

        await Assert.That(raised).IsTrue();
    }

    [Test]
    public async Task Click_RaisedOnCheck()
    {
        var rb = new RadioButton();
        var clicked = false;
        rb.Click += () => clicked = true;

        rb.OnKeyEvent(new KeyEvent(ConsoleKey.Spacebar, ' ', false, false, false));

        await Assert.That(clicked).IsTrue();
    }

    #endregion

    #region Rendering

    [Test]
    public async Task Render_Unchecked_ShowsParens()
    {
        var rb = new RadioButton { Content = "Option" };

        using var buffer = new CellBuffer(20, 1);
        rb.Render(buffer, new Rect(0, 0, 20, 1));

        await Assert.That(buffer.GetCell(0, 0).Character).IsEqualTo('(');
        await Assert.That(buffer.GetCell(1, 0).Character).IsEqualTo(' ');
        await Assert.That(buffer.GetCell(2, 0).Character).IsEqualTo(')');
    }

    [Test]
    public async Task Render_Checked_ShowsAsterisk()
    {
        var rb = new RadioButton { IsChecked = true, Content = "Option" };

        using var buffer = new CellBuffer(20, 1);
        rb.Render(buffer, new Rect(0, 0, 20, 1));

        await Assert.That(buffer.GetCell(0, 0).Character).IsEqualTo('(');
        await Assert.That(buffer.GetCell(1, 0).Character).IsEqualTo('*');
        await Assert.That(buffer.GetCell(2, 0).Character).IsEqualTo(')');
    }

    [Test]
    public async Task Render_Content_RendersAfterIndicator()
    {
        var rb = new RadioButton { Content = "Test" };

        using var buffer = new CellBuffer(20, 1);
        rb.Render(buffer, new Rect(0, 0, 20, 1));

        await Assert.That(buffer.GetCell(4, 0).Character).IsEqualTo('T');
    }

    #endregion

    #region XAML

    [Test]
    public async Task Xaml_ParsesProperties()
    {
        var xaml = """
            <RadioButton xmlns="http://schemas.terminalninja.dev/xaml"
                         IsChecked="True" GroupName="Size" Content="Large" />
            """;

        var rb = TerminalXaml.Load<RadioButton>(xaml);

        await Assert.That(rb.IsChecked).IsTrue();
        await Assert.That(rb.GroupName).IsEqualTo("Size");
    }

    [Test]
    public async Task Xaml_GroupBehavior_WorksAfterLoad()
    {
        var xaml = """
            <StackPanel xmlns="http://schemas.terminalninja.dev/xaml">
                <RadioButton GroupName="Size" Content="Small" IsChecked="True" />
                <RadioButton GroupName="Size" Content="Large" />
            </StackPanel>
            """;

        var panel = TerminalXaml.Load<StackPanel>(xaml);
        var rb1 = (RadioButton)panel.Children[0];
        var rb2 = (RadioButton)panel.Children[1];

        rb2.IsChecked = true;

        await Assert.That(rb1.IsChecked).IsFalse();
        await Assert.That(rb2.IsChecked).IsTrue();
    }

    #endregion
}
