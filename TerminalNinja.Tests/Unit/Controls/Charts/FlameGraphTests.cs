using TerminalNinja.Controls.Charts;

namespace TerminalNinja.Tests.Unit.Controls.Charts;

/// <summary>Tests for <see cref="FlameGraph"/> rendering and XAML instantiation.</summary>
public class FlameGraphTests
{
    private const int W = 40;
    private const int H = 10;

    private static bool ContainsChar(CellBuffer buffer, char c)
    {
        for (var y = 0; y < buffer.Height; y++)
        {
            for (var x = 0; x < buffer.Width; x++)
            {
                if (buffer.GetCell(x, y).Codepoint == c)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int BlockCountInRow(CellBuffer buffer, int row)
    {
        var count = 0;
        for (var x = 0; x < buffer.Width; x++)
        {
            if (buffer.GetCell(x, row).Codepoint == '█')
            {
                count++;
            }
        }

        return count;
    }

    [Test]
    public async Task Render_WithTree_DrawsFrames()
    {
        using var buffer = new CellBuffer(W, H);
        var root = new FlameNode { Name = "main", Value = 100 };
        root.Children.Add(new FlameNode { Name = "a", Value = 60 });
        root.Children.Add(new FlameNode { Name = "b", Value = 40 });
        var chart = new FlameGraph { Root = root, Title = "" };

        chart.Render(buffer, new Rect(0, 0, W, H));

        await Assert.That(ContainsChar(buffer, '█')).IsTrue();
    }

    [Test]
    public async Task Render_RootRow_SpansFullWidth()
    {
        using var buffer = new CellBuffer(W, H);
        var root = new FlameNode { Name = "root", Value = 100 };
        root.Children.Add(new FlameNode { Name = "child", Value = 50 });
        var chart = new FlameGraph { Root = root, Title = "" };

        chart.Render(buffer, new Rect(0, 0, W, H));

        // Root occupies most of row 0 (some cells hold the overlaid label); the child row,
        // sized to 50% of the root's value, is clearly narrower.
        await Assert.That(BlockCountInRow(buffer, 0)).IsGreaterThan(W / 2);
        await Assert.That(BlockCountInRow(buffer, 1)).IsLessThan(BlockCountInRow(buffer, 0));
    }

    [Test]
    public async Task Render_NoRoot_ShowsNoDataMessage()
    {
        using var buffer = new CellBuffer(W, H);
        var chart = new FlameGraph();

        chart.Render(buffer, new Rect(0, 0, W, H));

        await Assert.That(ContainsChar(buffer, '█')).IsFalse();
    }

    [Test]
    public async Task Xaml_WithNestedNodes_BuildsTree()
    {
        const string xaml = """
            <FlameGraph xmlns="http://schemas.terminalninja.dev/xaml">
                <FlameNode Name="main" Value="100">
                    <FlameNode Name="parse" Value="40" />
                    <FlameNode Name="exec" Value="60" />
                </FlameNode>
            </FlameGraph>
            """;

        var chart = TerminalXaml.Load<FlameGraph>(xaml);

        await Assert.That(chart.Root).IsNotNull();
        await Assert.That(chart.Root!.Name).IsEqualTo("main");
        await Assert.That(chart.Root!.Children.Count).IsEqualTo(2);
        await Assert.That(chart.Root!.Children[0].Name).IsEqualTo("parse");
    }
}
