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

    private static KeyEvent Key(ConsoleKey key) => new(key, '\0', false, false, false);

    private static (FlameGraph Chart, FlameNode Root, FlameNode A, FlameNode B) TwoChildren()
    {
        var a = new FlameNode { Name = "a", Value = 60 };
        var b = new FlameNode { Name = "b", Value = 40 };
        var root = new FlameNode { Name = "main", Value = 100 };
        root.Children.Add(a);
        root.Children.Add(b);
        return (new FlameGraph { Root = root, Title = "" }, root, a, b);
    }

    [Test]
    public async Task IsFocusable()
    {
        await Assert.That(new FlameGraph().Focusable).IsTrue();
    }

    [Test]
    public async Task Arrows_WalkTheTree()
    {
        var (chart, root, a, b) = TwoChildren();
        using var buffer = new CellBuffer(40, H);
        chart.Render(buffer, new Rect(0, 0, 40, H)); // populates the frame layout

        chart.OnKeyEvent(Key(ConsoleKey.DownArrow)); // no selection → root
        await Assert.That(chart.SelectedNode).IsSameReferenceAs(root);

        chart.OnKeyEvent(Key(ConsoleKey.DownArrow)); // first child
        await Assert.That(chart.SelectedNode).IsSameReferenceAs(a);

        chart.OnKeyEvent(Key(ConsoleKey.RightArrow)); // next sibling
        await Assert.That(chart.SelectedNode).IsSameReferenceAs(b);

        chart.OnKeyEvent(Key(ConsoleKey.LeftArrow)); // previous sibling
        await Assert.That(chart.SelectedNode).IsSameReferenceAs(a);

        chart.OnKeyEvent(Key(ConsoleKey.UpArrow)); // parent
        await Assert.That(chart.SelectedNode).IsSameReferenceAs(root);
    }

    [Test]
    public async Task Click_SelectsFrameUnderCursor()
    {
        var (chart, _, _, b) = TwoChildren();
        using var buffer = new CellBuffer(40, H);
        chart.Render(buffer, new Rect(0, 0, 40, H));

        // Depth 1 is at y=1 (no title). b occupies roughly x∈[24,40).
        chart.OnMouseEvent(new MouseEvent(30, 1, MouseButton.Left, MouseAction.Press));
        await Assert.That(chart.SelectedNode).IsSameReferenceAs(b);
    }

    [Test]
    public async Task SelectedFrame_IsHighlightedWhenFocused()
    {
        var (chart, _, a, _) = TwoChildren();
        chart.IsFocused = true;
        chart.SelectedNode = a;

        using var buffer = new CellBuffer(40, H);
        chart.Render(buffer, new Rect(0, 0, 40, H));

        var highlighted = false;
        for (var x = 0; x < 40; x++)
        {
            if (buffer.GetCell(x, 1).Background == new Color(38, 79, 120))
            {
                highlighted = true;
            }
        }

        await Assert.That(highlighted).IsTrue();
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
