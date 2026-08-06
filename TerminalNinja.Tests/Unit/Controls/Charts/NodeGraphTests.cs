using TerminalNinja.Controls.Charts;

namespace TerminalNinja.Tests.Unit.Controls.Charts;

/// <summary>Tests for <see cref="NodeGraph"/> layout, rendering, and XAML instantiation.</summary>
public class NodeGraphTests
{
    private const int W = 60;
    private const int H = 24;

    private static NodeGraph ThreeNodeGraph()
    {
        var graph = new NodeGraph();
        graph.GraphNodes.Add(new GraphNode { Id = "web", Name = "web" });
        graph.GraphNodes.Add(new GraphNode { Id = "api", Name = "api" });
        graph.GraphNodes.Add(new GraphNode { Id = "db", Name = "db" });
        graph.GraphEdges.Add(new GraphEdge { From = "web", To = "api" });
        graph.GraphEdges.Add(new GraphEdge { From = "api", To = "db" });
        return graph;
    }

    private static bool ContainsChar(CellBuffer buffer, uint codepoint)
    {
        for (var y = 0; y < buffer.Height; y++)
        {
            for (var x = 0; x < buffer.Width; x++)
            {
                if (buffer.GetCell(x, y).Codepoint == codepoint)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ContainsText(CellBuffer buffer, string text) => FindText(buffer, text).X >= 0;

    private static bool ContainsBraille(CellBuffer buffer)
    {
        for (var y = 0; y < buffer.Height; y++)
        {
            for (var x = 0; x < buffer.Width; x++)
            {
                var cp = buffer.GetCell(x, y).Codepoint;
                if (cp is > 0x2800 and <= 0x28FF)
                {
                    return true;
                }
            }
        }

        return false;
    }

    [Test]
    public async Task Render_WithNodes_DrawsNodeBoxes()
    {
        using var buffer = new CellBuffer(W, H);
        var graph = ThreeNodeGraph();

        graph.Render(buffer, new Rect(0, 0, W, H));

        await Assert.That(ContainsChar(buffer, '┌')).IsTrue();
        await Assert.That(ContainsChar(buffer, '┘')).IsTrue();
    }

    [Test]
    public async Task Render_WithNodes_DrawsLabels()
    {
        using var buffer = new CellBuffer(W, H);
        var graph = ThreeNodeGraph();

        graph.Render(buffer, new Rect(0, 0, W, H));

        await Assert.That(ContainsText(buffer, "web")).IsTrue();
        await Assert.That(ContainsText(buffer, "api")).IsTrue();
        await Assert.That(ContainsText(buffer, "db")).IsTrue();
    }

    [Test]
    public async Task Render_WithEdges_DrawsBrailleLines()
    {
        using var buffer = new CellBuffer(W, H);
        var graph = ThreeNodeGraph();

        graph.Render(buffer, new Rect(0, 0, W, H));

        await Assert.That(ContainsBraille(buffer)).IsTrue();
    }

    [Test]
    public async Task Render_Twice_ProducesIdenticalOutput()
    {
        using var first = new CellBuffer(W, H);
        using var second = new CellBuffer(W, H);
        var graph = ThreeNodeGraph();

        graph.Render(first, new Rect(0, 0, W, H));
        graph.Render(second, new Rect(0, 0, W, H));

        for (var y = 0; y < H; y++)
        {
            for (var x = 0; x < W; x++)
            {
                await Assert.That(second.GetCell(x, y)).IsEqualTo(first.GetCell(x, y));
            }
        }
    }

    [Test]
    public async Task Render_NoNodes_DrawsEmptyState()
    {
        using var buffer = new CellBuffer(W, H);
        var graph = new NodeGraph();

        graph.Render(buffer, new Rect(0, 0, W, H));

        await Assert.That(ContainsText(buffer, "(no nodes)")).IsTrue();
    }

    [Test]
    public async Task Render_MoreNodesThanCap_DrawsTruncationNotice()
    {
        using var buffer = new CellBuffer(W, H);
        var graph = new NodeGraph { LayoutIterations = 1 };
        for (var i = 0; i < 502; i++)
        {
            graph.GraphNodes.Add(new GraphNode { Id = $"n{i}", Name = $"n{i}" });
        }

        graph.Render(buffer, new Rect(0, 0, W, H));

        await Assert.That(ContainsText(buffer, "2 more")).IsTrue();
    }

    [Test]
    public async Task LayoutIterations_OutOfRange_IsCoerced()
    {
        var graph = new NodeGraph();

        graph.LayoutIterations = 0;
        await Assert.That(graph.LayoutIterations).IsEqualTo(1);

        graph.LayoutIterations = 5000;
        await Assert.That(graph.LayoutIterations).IsEqualTo(200);
    }

    private static KeyEvent Key(ConsoleKey key) => new(key, '\0', false, false, false);

    /// <summary>Finds the top-left buffer coordinate of <paramref name="text"/>, or (-1,-1).</summary>
    private static (int X, int Y) FindText(CellBuffer buffer, string text)
    {
        for (var y = 0; y < buffer.Height; y++)
        {
            for (var x = 0; x + text.Length <= buffer.Width; x++)
            {
                var match = true;
                for (var i = 0; i < text.Length; i++)
                {
                    if (buffer.GetCell(x + i, y).Codepoint != text[i])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return (x, y);
                }
            }
        }

        return (-1, -1);
    }

    [Test]
    public async Task DownArrow_MovesSelectionAndSyncsSelectedNode()
    {
        var graph = ThreeNodeGraph();

        graph.OnKeyEvent(Key(ConsoleKey.DownArrow));
        await Assert.That(graph.SelectedIndex).IsEqualTo(0);
        await Assert.That(graph.SelectedNode).IsSameReferenceAs(graph.GraphNodes[0]);

        graph.OnKeyEvent(Key(ConsoleKey.DownArrow));
        await Assert.That(graph.SelectedIndex).IsEqualTo(1);
        await Assert.That(graph.SelectedNode).IsSameReferenceAs(graph.GraphNodes[1]);
    }

    [Test]
    public async Task ArrowKeys_ClampAtEnds()
    {
        var graph = ThreeNodeGraph();

        graph.OnKeyEvent(Key(ConsoleKey.End));
        await Assert.That(graph.SelectedIndex).IsEqualTo(2);

        graph.OnKeyEvent(Key(ConsoleKey.RightArrow)); // clamps at last
        await Assert.That(graph.SelectedIndex).IsEqualTo(2);

        graph.OnKeyEvent(Key(ConsoleKey.Home));
        await Assert.That(graph.SelectedIndex).IsEqualTo(0);

        graph.OnKeyEvent(Key(ConsoleKey.UpArrow)); // clamps at first
        await Assert.That(graph.SelectedIndex).IsEqualTo(0);
    }

    [Test]
    public async Task SettingSelectedNode_SyncsSelectedIndex()
    {
        var graph = ThreeNodeGraph();

        graph.SelectedNode = graph.GraphNodes[2];

        await Assert.That(graph.SelectedIndex).IsEqualTo(2);
    }

    [Test]
    public async Task Click_OnNodeBox_SelectsThatNode()
    {
        using var buffer = new CellBuffer(W, H);
        var graph = ThreeNodeGraph();
        graph.Render(buffer, new Rect(0, 0, W, H));

        var (x, y) = FindText(buffer, "api");
        await Assert.That(x).IsGreaterThanOrEqualTo(0);

        graph.OnMouseEvent(new MouseEvent(x, y, MouseButton.Left, MouseAction.Press));

        await Assert.That(graph.SelectedNode).IsSameReferenceAs(graph.GraphNodes[1]);
        await Assert.That(graph.SelectedIndex).IsEqualTo(1);
    }

    [Test]
    public async Task Click_OutsideAllBoxes_KeepsSelection()
    {
        using var buffer = new CellBuffer(W, H);
        var graph = ThreeNodeGraph();
        graph.Title = "Topology"; // row 0 is the title, never a node box
        graph.SelectedIndex = 1;
        graph.Render(buffer, new Rect(0, 0, W, H));

        graph.OnMouseEvent(new MouseEvent(W - 1, 0, MouseButton.Left, MouseAction.Press));

        await Assert.That(graph.SelectedIndex).IsEqualTo(1);
    }

    [Test]
    public async Task Render_SelectedNode_UsesSelectionBackground()
    {
        using var buffer = new CellBuffer(W, H);
        var graph = ThreeNodeGraph();
        graph.IsFocused = true;
        graph.SelectedIndex = 0;

        graph.Render(buffer, new Rect(0, 0, W, H));

        var highlighted = false;
        for (var y = 0; y < H; y++)
        {
            for (var x = 0; x < W; x++)
            {
                if (buffer.GetCell(x, y).Background == new Color(38, 79, 120))
                {
                    highlighted = true;
                }
            }
        }

        await Assert.That(highlighted).IsTrue();
    }

    internal sealed class SelectionViewModel : TerminalNinja.Xaml.Mvvm.ViewModelBase
    {
        private object? _selected;

        public object? Selected
        {
            get => _selected;
            set => SetProperty(ref _selected, value);
        }
    }

    [Test]
    public async Task TwoWayBinding_SelectedNode_SurvivesRepeatedSelectionMoves()
    {
        var vm = new SelectionViewModel();
        const string xaml = """
            <NodeGraph xmlns="http://schemas.terminalninja.dev/xaml"
                       SelectedNode="{Binding Selected}">
                <GraphNode Id="a" Name="a" />
                <GraphNode Id="b" Name="b" />
                <GraphNode Id="c" Name="c" />
            </NodeGraph>
            """;
        var graph = TerminalXaml.Load<NodeGraph>(xaml, vm);

        // Each move must keep writing through the binding; SetValue instead of
        // SetValueInternal in the sync callbacks would break it after the first move.
        graph.OnKeyEvent(Key(ConsoleKey.DownArrow));
        await Assert.That(vm.Selected).IsSameReferenceAs(graph.GraphNodes[0]);

        graph.OnKeyEvent(Key(ConsoleKey.DownArrow));
        await Assert.That(vm.Selected).IsSameReferenceAs(graph.GraphNodes[1]);

        graph.OnKeyEvent(Key(ConsoleKey.DownArrow));
        await Assert.That(vm.Selected).IsSameReferenceAs(graph.GraphNodes[2]);
    }

    [Test]
    public async Task Xaml_WithNodesAndEdges_ParsesBoth()
    {
        const string xaml = """
            <NodeGraph xmlns="http://schemas.terminalninja.dev/xaml" Title="Topology">
                <GraphNode Id="web" Name="Web" />
                <GraphNode Id="db" Name="Db" />
                <NodeGraph.GraphEdges>
                    <GraphEdge From="web" To="db" />
                </NodeGraph.GraphEdges>
            </NodeGraph>
            """;

        var graph = TerminalXaml.Load<NodeGraph>(xaml);

        await Assert.That(graph.GraphNodes.Count).IsEqualTo(2);
        await Assert.That(graph.GraphEdges.Count).IsEqualTo(1);
        await Assert.That(graph.GraphEdges[0].To).IsEqualTo("db");
    }
}
