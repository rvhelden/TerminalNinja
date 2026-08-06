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

    private static bool ContainsText(CellBuffer buffer, string text)
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
                    return true;
                }
            }
        }

        return false;
    }

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
