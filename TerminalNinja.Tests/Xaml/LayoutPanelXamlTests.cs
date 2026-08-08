namespace TerminalNinja.Tests.Xaml;

/// <summary>
/// XAML parsing tests for the DockPanel, UniformGrid and WrapPanel layout containers —
/// the panels are only useful if they can be declared, so the attached property, the enum
/// conversions and the integer properties are all exercised through the loader.
/// </summary>
public class LayoutPanelXamlTests
{
    [Test]
    public async Task DockPanel_ParsesDockAttachedPropertyAndLastChildFill()
    {
        var xaml = """
            <DockPanel xmlns="http://schemas.terminalninja.dev/xaml" LastChildFill="False">
                <TextBlock DockPanel.Dock="Top" Text="header" />
                <TextBlock DockPanel.Dock="Bottom" Text="footer" />
                <TextBlock DockPanel.Dock="Left" Text="side" />
                <TextBlock DockPanel.Dock="Right" Text="aside" />
                <TextBlock Text="content" />
            </DockPanel>
            """;

        var panel = TerminalXaml.Load<DockPanel>(xaml);

        await Assert.That(panel.LastChildFill).IsFalse();
        await Assert.That(panel.Children.Count).IsEqualTo(5);
        await Assert.That(DockPanel.GetDock(panel.Children[0])).IsEqualTo(Dock.Top);
        await Assert.That(DockPanel.GetDock(panel.Children[1])).IsEqualTo(Dock.Bottom);
        await Assert.That(DockPanel.GetDock(panel.Children[2])).IsEqualTo(Dock.Left);
        await Assert.That(DockPanel.GetDock(panel.Children[3])).IsEqualTo(Dock.Right);
        // Not specified — falls back to the default.
        await Assert.That(DockPanel.GetDock(panel.Children[4])).IsEqualTo(Dock.Left);
    }

    [Test]
    public async Task DockPanel_ArrangesParsedChildren()
    {
        var xaml = """
            <DockPanel xmlns="http://schemas.terminalninja.dev/xaml">
                <TextBlock DockPanel.Dock="Top" Text="header" />
                <Border DockPanel.Dock="Left" Width="6" />
                <Border />
            </DockPanel>
            """;

        var panel = TerminalXaml.Load<DockPanel>(xaml);
        var rects = panel.CalculateChildBounds(new Rect(0, 0, 20, 10));

        await Assert.That(rects[0]).IsEqualTo(new Rect(0, 0, 20, 1));
        await Assert.That(rects[1]).IsEqualTo(new Rect(0, 1, 6, 9));
        await Assert.That(rects[2]).IsEqualTo(new Rect(6, 1, 14, 9));
    }

    [Test]
    public async Task UniformGrid_ParsesRowsAndColumns()
    {
        var xaml = """
            <UniformGrid xmlns="http://schemas.terminalninja.dev/xaml" Rows="2" Columns="3">
                <TextBlock Text="a" />
                <TextBlock Text="b" />
            </UniformGrid>
            """;

        var grid = TerminalXaml.Load<UniformGrid>(xaml);

        await Assert.That(grid.Rows).IsEqualTo(2);
        await Assert.That(grid.Columns).IsEqualTo(3);
        await Assert.That(grid.Children.Count).IsEqualTo(2);
    }

    [Test]
    public async Task UniformGrid_OmittedShape_DerivesFromChildCount()
    {
        var xaml = """
            <UniformGrid xmlns="http://schemas.terminalninja.dev/xaml">
                <TextBlock Text="a" />
                <TextBlock Text="b" />
                <TextBlock Text="c" />
                <TextBlock Text="d" />
            </UniformGrid>
            """;

        var grid = TerminalXaml.Load<UniformGrid>(xaml);
        var rects = grid.CalculateChildBounds(new Rect(0, 0, 10, 10));

        await Assert.That(rects[0]).IsEqualTo(new Rect(0, 0, 5, 5));
        await Assert.That(rects[3]).IsEqualTo(new Rect(5, 5, 5, 5));
    }

    [Test]
    public async Task WrapPanel_ParsesOrientationAndWraps()
    {
        var xaml = """
            <WrapPanel xmlns="http://schemas.terminalninja.dev/xaml" Orientation="Vertical">
                <Border Width="4" Height="3" />
                <Border Width="4" Height="3" />
                <Border Width="4" Height="3" />
            </WrapPanel>
            """;

        var panel = TerminalXaml.Load<WrapPanel>(xaml);

        await Assert.That(panel.Orientation).IsEqualTo(Orientation.Vertical);

        var rects = panel.CalculateChildBounds(new Rect(0, 0, 20, 7));

        await Assert.That(rects[0]).IsEqualTo(new Rect(0, 0, 4, 3));
        await Assert.That(rects[1]).IsEqualTo(new Rect(0, 3, 4, 3));
        await Assert.That(rects[2]).IsEqualTo(new Rect(4, 0, 4, 3));
    }

    [Test]
    public async Task WrapPanel_OmittedOrientation_DefaultsToHorizontal()
    {
        var xaml = """
            <WrapPanel xmlns="http://schemas.terminalninja.dev/xaml">
                <Border Width="4" Height="2" />
            </WrapPanel>
            """;

        var panel = TerminalXaml.Load<WrapPanel>(xaml);

        await Assert.That(panel.Orientation).IsEqualTo(Orientation.Horizontal);
    }
}
