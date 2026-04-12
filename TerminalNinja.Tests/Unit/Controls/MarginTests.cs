namespace TerminalNinja.Tests.Unit.Controls;

public class MarginTests
{
    #region Default Values

    [Test]
    public async Task Margin_Default_IsZeroThickness()
    {
        var tb = new TextBlock();
        await Assert.That(tb.Margin).IsEqualTo(new Thickness(0));
    }

    #endregion

    #region ApplyAlignment with Margin

    [Test]
    public async Task Margin_Uniform_OffsetsBounds()
    {
        var tb = new TextBlock { Text = "Hi", Margin = new Thickness(2) };

        var bounds = tb.CalculateBounds(new Rect(0, 0, 20, 10));

        // With margin 2 on all sides: x=2, y=2
        await Assert.That(bounds.X).IsEqualTo(2);
        await Assert.That(bounds.Y).IsEqualTo(2);
        // Width reduced: 20 - 4 = 16
        await Assert.That(bounds.Width).IsLessThanOrEqualTo(16);
    }

    [Test]
    public async Task Margin_Asymmetric_AppliedCorrectly()
    {
        var tb = new TextBlock { Text = "X", Margin = new Thickness(1, 2, 3, 4) };

        var bounds = tb.CalculateBounds(new Rect(0, 0, 20, 20));

        await Assert.That(bounds.X).IsEqualTo(1);
        await Assert.That(bounds.Y).IsEqualTo(2);
    }

    [Test]
    public async Task Margin_WithCenterAlignment_CentersWithinReducedArea()
    {
        var border = new Border
        {
            Width = Size.Absolute(6),
            Height = Size.Absolute(4),
            Margin = new Thickness(2),
            HorizontalAlignment = Alignment.Center,
            VerticalAlignment = Alignment.Center
        };

        // Parent 20x10, margin 2 all sides → available 16x6, center 6x4 in 16x6
        var bounds = border.CalculateBounds(new Rect(0, 0, 20, 10));

        // Centered in reduced area: x = 2 + (16-6)/2 = 7, y = 2 + (6-4)/2 = 3
        await Assert.That(bounds.X).IsEqualTo(7);
        await Assert.That(bounds.Y).IsEqualTo(3);
        await Assert.That(bounds.Width).IsEqualTo(6);
        await Assert.That(bounds.Height).IsEqualTo(4);
    }

    [Test]
    public async Task Margin_WithEndAlignment_AlignsToEndOfReducedArea()
    {
        var border = new Border
        {
            Width = Size.Absolute(5),
            Height = Size.Absolute(3),
            Margin = new Thickness(1),
            HorizontalAlignment = Alignment.End,
            VerticalAlignment = Alignment.End
        };

        // Parent 20x10, margin 1 → available area 1..18 x 1..8 (18x8)
        var bounds = border.CalculateBounds(new Rect(0, 0, 20, 10));

        // End-aligned: x = 1 + 18 - 5 = 14, y = 1 + 8 - 3 = 6
        await Assert.That(bounds.X).IsEqualTo(14);
        await Assert.That(bounds.Y).IsEqualTo(6);
    }

    [Test]
    public async Task Margin_ExceedsParent_ClampedToZeroSize()
    {
        var tb = new TextBlock { Text = "X", Margin = new Thickness(50) };

        var bounds = tb.CalculateBounds(new Rect(0, 0, 20, 10));

        await Assert.That(bounds.Width).IsEqualTo(0);
        await Assert.That(bounds.Height).IsEqualTo(0);
    }

    #endregion

    #region Margin in Rendering

    [Test]
    public async Task Render_WithMargin_TextOffsetByMargin()
    {
        var tb = new TextBlock { Text = "Hi", Margin = new Thickness(3, 1, 0, 0) };

        using var buffer = new CellBuffer(20, 5);
        tb.Render(buffer, new Rect(0, 0, 20, 5));

        // Text starts at (3, 1) due to margin
        await Assert.That(buffer.GetCell(3, 1).Character).IsEqualTo('H');
        await Assert.That(buffer.GetCell(4, 1).Character).IsEqualTo('i');
        // Position (0, 0) should be empty
        await Assert.That(buffer.GetCell(0, 0).Character).IsNotEqualTo('H');
    }

    #endregion

    #region Margin in StackPanel

    [Test]
    public async Task StackPanel_ChildWithMargin_AllocatesExtraSpace()
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical };
        var tb1 = new TextBlock { Text = "A", Margin = new Thickness(0, 0, 0, 1) }; // 1 bottom margin
        StackPanel.SetSizeMode(tb1, ChildSizeMode.Auto);
        var tb2 = new TextBlock { Text = "B" };
        StackPanel.SetSizeMode(tb2, ChildSizeMode.Auto);
        panel.Children.Add(tb1);
        panel.Children.Add(tb2);

        var sizes = panel.CalculateChildSizes(new Rect(0, 0, 20, 10));

        // First child: preferred height 1 + margin bottom 1 = 2
        await Assert.That(sizes[0]).IsEqualTo(2);
    }

    #endregion

    #region XAML Parsing

    [Test]
    public async Task Xaml_Margin_Uniform()
    {
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml" Text="X" Margin="5" />
            """;
        var tb = TerminalXaml.Load<TextBlock>(xaml);

        await Assert.That(tb.Margin).IsEqualTo(new Thickness(5));
    }

    [Test]
    public async Task Xaml_Margin_HorizontalVertical()
    {
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml" Text="X" Margin="2,3" />
            """;
        var tb = TerminalXaml.Load<TextBlock>(xaml);

        await Assert.That(tb.Margin).IsEqualTo(new Thickness(2, 3));
    }

    [Test]
    public async Task Xaml_Margin_AllFourSides()
    {
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml" Text="X" Margin="1,2,3,4" />
            """;
        var tb = TerminalXaml.Load<TextBlock>(xaml);

        await Assert.That(tb.Margin).IsEqualTo(new Thickness(1, 2, 3, 4));
    }

    #endregion
}
