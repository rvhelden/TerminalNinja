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
    public async Task Tag_CarriesTheApplicationsOwnObject()
    {
        // Id belongs to the graph — GraphEdge endpoints match on it — so it is not somewhere an
        // application should have to hide a domain key just to find its way back from a selection.
        var payload = new { Resource = "app-x" };

        var graph = new NodeGraph();
        graph.GraphNodes.Add(new GraphNode { Id = "a", Name = "a", Tag = payload });

        graph.OnKeyEvent(Key(ConsoleKey.DownArrow));

        await Assert.That((graph.SelectedNode as GraphNode)?.Tag).IsSameReferenceAs(payload);
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

    // ─── Edge labels ─────────────────────────────────────────────────

    /// <summary>Two short-labeled nodes, so the midpoint between them is clear of both boxes.</summary>
    private static NodeGraph LabeledPair(string label)
    {
        var graph = new NodeGraph();
        graph.GraphNodes.Add(new GraphNode { Id = "a", Name = "a" });
        graph.GraphNodes.Add(new GraphNode { Id = "b", Name = "b" });
        graph.GraphEdges.Add(new GraphEdge { From = "a", To = "b", Label = label });
        return graph;
    }

    [Test]
    public async Task Render_WithEdgeLabel_DrawsIt()
    {
        using var buffer = new CellBuffer(W, H);

        LabeledPair("42%").Render(buffer, new Rect(0, 0, W, H));

        await Assert.That(ContainsText(buffer, "42%")).IsTrue();
    }

    [Test]
    public async Task Render_WithoutEdgeLabel_DrawsNoCaption()
    {
        using var buffer = new CellBuffer(W, H);

        LabeledPair("").Render(buffer, new Rect(0, 0, W, H));

        await Assert.That(ContainsText(buffer, "x")).IsFalse();
    }

    [Test]
    public async Task Render_EdgeLabel_UsesTheEdgeColor()
    {
        using var buffer = new CellBuffer(W, H);
        var graph = new NodeGraph();
        graph.GraphNodes.Add(new GraphNode { Id = "a", Name = "a" });
        graph.GraphNodes.Add(new GraphNode { Id = "b", Name = "b" });
        graph.GraphEdges.Add(new GraphEdge { From = "a", To = "b", Label = "9%", Color = new Color(255, 0, 0) });

        graph.Render(buffer, new Rect(0, 0, W, H));

        var (x, y) = FindText(buffer, "9%");
        await Assert.That(x).IsGreaterThanOrEqualTo(0);
        await Assert.That(buffer.GetCell(x, y).Foreground).IsEqualTo(new Color(255, 0, 0));
    }

    [Test]
    public async Task Render_EdgeLabelWiderThanThePlot_IsSkipped()
    {
        using var buffer = new CellBuffer(W, H);

        // Must not throw, and must not leave a fragment behind.
        LabeledPair(new string('z', W + 10)).Render(buffer, new Rect(0, 0, W, H));

        await Assert.That(ContainsText(buffer, "zz")).IsFalse();
    }

    [Test]
    public async Task Render_EdgeLabel_GivesWayToTheNodeBoxes()
    {
        // A cramped plot puts the midpoint inside a box. Boxes are drawn after labels, so an
        // unskipped label would be half-eaten — it must be dropped whole instead, leaving no
        // fragment of itself anywhere on the buffer.
        using var buffer = new CellBuffer(14, 8);
        var graph = new NodeGraph();
        graph.GraphNodes.Add(new GraphNode { Id = "a", Name = "aaaaaaaa" });
        graph.GraphNodes.Add(new GraphNode { Id = "b", Name = "bbbbbbbb" });
        graph.GraphEdges.Add(new GraphEdge { From = "a", To = "b", Label = "99%" });

        graph.Render(buffer, new Rect(0, 0, 14, 8));

        await Assert.That(ContainsText(buffer, "99%")).IsFalse();
        await Assert.That(ContainsChar(buffer, '%')).IsFalse();
    }

    [Test]
    public async Task Render_LabeledEdges_StaysDeterministic()
    {
        using var first = new CellBuffer(W, H);
        using var second = new CellBuffer(W, H);

        LabeledPair("7%").Render(first, new Rect(0, 0, W, H));
        LabeledPair("7%").Render(second, new Rect(0, 0, W, H));

        for (var y = 0; y < H; y++)
        {
            for (var x = 0; x < W; x++)
            {
                await Assert.That(second.GetCell(x, y)).IsEqualTo(first.GetCell(x, y));
            }
        }
    }

    [Test]
    public async Task Render_CrossingEdges_LetTheLaterOneWinTheSharedCells()
    {
        // A graph is a hub: every edge crosses near the center. If the blit order did not follow
        // the data, a caller's one highlighted edge could be painted over by the rest and left
        // with a cell or two — which is exactly what unspecified Dictionary order used to do.
        using var buffer = new CellBuffer(W, H);
        var graph = new NodeGraph();
        foreach (var id in new[] { "hub", "a", "b", "c", "d" })
        {
            graph.GraphNodes.Add(new GraphNode { Id = id, Name = id });
        }

        var grey = new Color(120, 120, 120);
        var red = new Color(220, 100, 100);

        graph.GraphEdges.Add(new GraphEdge { From = "hub", To = "a", Color = grey });
        graph.GraphEdges.Add(new GraphEdge { From = "hub", To = "b", Color = grey });
        graph.GraphEdges.Add(new GraphEdge { From = "hub", To = "c", Color = grey });
        graph.GraphEdges.Add(new GraphEdge { From = "hub", To = "d", Color = red });

        graph.Render(buffer, new Rect(0, 0, W, H));

        var redCells = 0;
        for (var y = 0; y < H; y++)
        {
            for (var x = 0; x < W; x++)
            {
                var cell = buffer.GetCell(x, y);
                if (cell.Codepoint is > 0x2800 and <= 0x28FF && cell.Foreground == red)
                {
                    redCells++;
                }
            }
        }

        // The last edge is drawn over the others, so its line survives as a line.
        await Assert.That(redCells).IsGreaterThan(1);
    }

    [Test]
    public async Task Xaml_EdgeLabel_Parses()
    {
        const string xaml = """
            <NodeGraph xmlns="http://schemas.terminalninja.dev/xaml">
                <GraphNode Id="web" Name="Web" />
                <GraphNode Id="db" Name="Db" />
                <NodeGraph.GraphEdges>
                    <GraphEdge From="web" To="db" Label="1.2%" />
                </NodeGraph.GraphEdges>
            </NodeGraph>
            """;

        var graph = TerminalXaml.Load<NodeGraph>(xaml);

        await Assert.That(graph.GraphEdges[0].Label).IsEqualTo("1.2%");
    }

    // ─── Edge direction markers ──────────────────────────────────────

    private const string Arrows = "▶◀▲▼"; // ▶ ◀ ▲ ▼

    /// <summary>Every cell holding one of the four direction glyphs, in reading order.</summary>
    private static List<(int X, int Y, Color Fg, char Glyph)> FindArrows(CellBuffer buffer)
    {
        var found = new List<(int, int, Color, char)>();
        for (var y = 0; y < buffer.Height; y++)
        {
            for (var x = 0; x < buffer.Width; x++)
            {
                var cell = buffer.GetCell(x, y);
                if (cell.Codepoint <= char.MaxValue && Arrows.Contains((char)cell.Codepoint))
                {
                    found.Add((x, y, cell.Foreground, (char)cell.Codepoint));
                }
            }
        }

        return found;
    }

    private static NodeGraph DirectedPair(string from, string to, Color color = default)
    {
        var graph = new NodeGraph();
        graph.GraphNodes.Add(new GraphNode { Id = "a", Name = "aaa" });
        graph.GraphNodes.Add(new GraphNode { Id = "b", Name = "bbb" });
        graph.GraphEdges.Add(new GraphEdge { From = from, To = to, Color = color });
        return graph;
    }

    [Test]
    public async Task Render_DirectedEdge_DrawsADirectionMarker()
    {
        using var buffer = new CellBuffer(W, H);

        DirectedPair("a", "b").Render(buffer, new Rect(0, 0, W, H));

        await Assert.That(FindArrows(buffer)).IsNotEmpty();
    }

    [Test]
    public async Task Render_ReversedEdge_MovesTheMarkerToTheOtherEnd()
    {
        // Same nodes in the same order, so the layout signature — and with it every box — is
        // identical. Only the direction differs, so anything that moves is the direction marker.
        using var forward = new CellBuffer(W, H);
        using var backward = new CellBuffer(W, H);

        DirectedPair("a", "b").Render(forward, new Rect(0, 0, W, H));
        DirectedPair("b", "a").Render(backward, new Rect(0, 0, W, H));

        var one = FindArrows(forward);
        var other = FindArrows(backward);

        await Assert.That(one).IsNotEmpty();
        await Assert.That(other).IsNotEmpty();
        await Assert.That((one[0].X, one[0].Y)).IsNotEqualTo((other[0].X, other[0].Y));
    }

    [Test]
    public async Task Render_DirectionMarker_PointsAtTheTargetBox()
    {
        using var buffer = new CellBuffer(W, H);
        var graph = DirectedPair("a", "b");

        graph.Render(buffer, new Rect(0, 0, W, H));

        var target = graph.RenderedBoxes[1];
        var arrows = FindArrows(buffer);
        await Assert.That(arrows).IsNotEmpty();

        var (x, y, _, glyph) = arrows[0];

        // The marker sits clear of every box — a marker inside one would be painted over by it —
        // and the glyph names the side of the target it arrived on.
        foreach (var box in graph.RenderedBoxes)
        {
            await Assert.That(box.Contains(x, y)).IsFalse();
        }

        var expected = glyph switch
        {
            '▶' => x < target.X,
            '◀' => x >= target.Right,
            '▼' => y < target.Y,
            _ => y >= target.Bottom,
        };
        await Assert.That(expected).IsTrue();
    }

    [Test]
    public async Task Render_DirectionMarker_UsesTheEdgeColor()
    {
        using var buffer = new CellBuffer(W, H);
        var red = new Color(255, 0, 0);

        DirectedPair("a", "b", red).Render(buffer, new Rect(0, 0, W, H));

        var arrows = FindArrows(buffer);
        await Assert.That(arrows).IsNotEmpty();
        await Assert.That(arrows[0].Fg).IsEqualTo(red);
    }

    [Test]
    public async Task Render_ShowEdgeArrowsFalse_DrawsNoMarker()
    {
        using var buffer = new CellBuffer(W, H);
        var graph = DirectedPair("a", "b");
        graph.ShowEdgeArrows = false;

        graph.Render(buffer, new Rect(0, 0, W, H));

        await Assert.That(FindArrows(buffer)).IsEmpty();
    }

    [Test]
    public async Task Render_DirectionMarkersNeverEscapeThePlot()
    {
        // The title row and the truncation row are outside the plot; a marker landing on either
        // is dropped, exactly as an edge label would be.
        using var buffer = new CellBuffer(40, 12);
        var graph = new NodeGraph { Title = "Topology", LayoutIterations = 1 };
        for (var i = 0; i < 520; i++)
        {
            graph.GraphNodes.Add(new GraphNode { Id = $"n{i}", Name = $"n{i}" });
            if (i > 0)
            {
                graph.GraphEdges.Add(new GraphEdge { From = "n0", To = $"n{i}" });
            }
        }

        graph.Render(buffer, new Rect(0, 0, 40, 12));

        foreach (var (_, y, _, _) in FindArrows(buffer))
        {
            await Assert.That(y).IsGreaterThanOrEqualTo(1);  // below the title
            await Assert.That(y).IsLessThan(11);             // above the "… N more" notice
        }
    }

    [Test]
    public async Task Render_DirectionMarkers_StayDeterministic()
    {
        using var first = new CellBuffer(W, H);
        using var second = new CellBuffer(W, H);

        DirectedPair("a", "b").Render(first, new Rect(0, 0, W, H));
        DirectedPair("a", "b").Render(second, new Rect(0, 0, W, H));

        await Assert.That(FindArrows(second)).IsEquivalentTo(FindArrows(first));
    }

    [Test]
    public async Task Xaml_ShowEdgeArrows_Parses()
    {
        const string xaml = """
            <NodeGraph xmlns="http://schemas.terminalninja.dev/xaml" ShowEdgeArrows="False">
                <GraphNode Id="a" Name="a" />
            </NodeGraph>
            """;

        var graph = TerminalXaml.Load<NodeGraph>(xaml);

        await Assert.That(graph.ShowEdgeArrows).IsFalse();
    }

    // ─── Selection across a source replacement ───────────────────────

    private static List<GraphNode> Nodes(params string[] ids)
    {
        var list = new List<GraphNode>();
        foreach (var id in ids)
        {
            list.Add(new GraphNode { Id = id, Name = id });
        }

        return list;
    }

    [Test]
    public async Task ReplacingNodesSource_WithAShorterList_ClampsTheSelection()
    {
        var graph = new NodeGraph { GraphNodesSource = Nodes("a", "b", "c") };
        graph.SelectedIndex = 2;

        graph.GraphNodesSource = Nodes("x");

        await Assert.That(graph.SelectedIndex).IsEqualTo(-1);
        await Assert.That(graph.SelectedNode).IsNull();
    }

    [Test]
    public async Task ReplacingNodesSource_LeavesNoStaleSelectedNode()
    {
        // The bug: SelectedIndex survived the swap and kept addressing the old list, so
        // SelectedNode still pointed at a node the graph was no longer drawing.
        var first = Nodes("a", "b", "c");
        var graph = new NodeGraph { GraphNodesSource = first };
        graph.SelectedIndex = 1;
        await Assert.That(graph.SelectedNode).IsSameReferenceAs(first[1]);

        var second = Nodes("d", "e", "f");
        graph.GraphNodesSource = second;

        await Assert.That(graph.SelectedNode).IsSameReferenceAs(second[1]);
        await Assert.That(graph.SelectedIndex).IsEqualTo(1);
    }

    [Test]
    public async Task ReplacingNodesSource_KeepsTheSelectedNodeWhenItSurvivesTheRebuild()
    {
        var kept = new GraphNode { Id = "b", Name = "b" };
        var first = new List<GraphNode> { new() { Id = "a", Name = "a" }, kept };
        var graph = new NodeGraph { GraphNodesSource = first };
        graph.SelectedIndex = 1;

        // A refresh that reuses its node objects but reorders them must keep the user on the
        // node they picked, not on the ordinal it happened to have.
        graph.GraphNodesSource = new List<GraphNode> { kept, new() { Id = "c", Name = "c" }, new() { Id = "a", Name = "a" } };

        await Assert.That(graph.SelectedNode).IsSameReferenceAs(kept);
        await Assert.That(graph.SelectedIndex).IsEqualTo(0);
    }

    [Test]
    public async Task ReplacingNodesSource_WithAnEmptyList_ClearsTheSelection()
    {
        var graph = new NodeGraph { GraphNodesSource = Nodes("a", "b") };
        graph.SelectedIndex = 0;

        graph.GraphNodesSource = new List<GraphNode>();

        await Assert.That(graph.SelectedIndex).IsEqualTo(-1);
        await Assert.That(graph.SelectedNode).IsNull();
    }

    [Test]
    public async Task RemovingTheSelectedNodeFromABoundCollection_ClampsTheSelection()
    {
        var source = new System.Collections.ObjectModel.ObservableCollection<GraphNode>(Nodes("a", "b", "c"));
        var graph = new NodeGraph { GraphNodesSource = source };
        graph.SelectedIndex = 2;

        source.RemoveAt(2);

        await Assert.That(graph.SelectedIndex).IsEqualTo(-1);
        await Assert.That(graph.SelectedNode).IsNull();
    }

    internal sealed class GraphViewModel : TerminalNinja.Xaml.Mvvm.ViewModelBase
    {
        private object? _selected;
        private System.Collections.Generic.List<GraphNode> _nodes = [];

        public object? Selected
        {
            get => _selected;
            set => SetProperty(ref _selected, value);
        }

        public System.Collections.Generic.List<GraphNode> Nodes
        {
            get => _nodes;
            set => SetProperty(ref _nodes, value);
        }
    }

    [Test]
    public async Task ReplacingNodesSource_KeepsATwoWaySelectedNodeBinding()
    {
        var vm = new GraphViewModel { Nodes = Nodes("a", "b", "c") };
        const string xaml = """
            <NodeGraph xmlns="http://schemas.terminalninja.dev/xaml"
                       GraphNodesSource="{Binding Nodes}"
                       SelectedNode="{Binding Selected}" />
            """;
        var graph = TerminalXaml.Load<NodeGraph>(xaml, vm);

        graph.OnKeyEvent(Key(ConsoleKey.DownArrow));
        await Assert.That(vm.Selected).IsSameReferenceAs(vm.Nodes[0]);

        // The reset writes through SetValueInternal; SetValue would drop the binding here and the
        // view model would never hear about the selection again.
        var rebuilt = Nodes("d", "e");
        vm.Nodes = rebuilt;
        await Assert.That(vm.Selected).IsSameReferenceAs(rebuilt[0]);

        graph.OnKeyEvent(Key(ConsoleKey.DownArrow));
        await Assert.That(vm.Selected).IsSameReferenceAs(rebuilt[1]);
    }

    // ─── Box collision avoidance ─────────────────────────────────────

    private static NodeGraph Estate(int count)
    {
        var graph = new NodeGraph();
        for (var i = 0; i < count; i++)
        {
            var id = $"app-debble-service{i:00}";
            graph.GraphNodes.Add(new GraphNode { Id = id, Name = id });
            if (i > 0)
            {
                graph.GraphEdges.Add(new GraphEdge { From = "app-debble-service00", To = id });
            }
        }

        return graph;
    }

    private static (int A, int B) FirstOverlap(IReadOnlyList<Rect> boxes)
    {
        for (var i = 0; i < boxes.Count; i++)
        {
            for (var j = i + 1; j < boxes.Count; j++)
            {
                if (boxes[i].Overlaps(boxes[j]))
                {
                    return (i, j);
                }
            }
        }

        return (-1, -1);
    }

    [Test]
    public async Task Render_ADozenLongLabels_DrawsNoOverlappingBoxes()
    {
        // The force layout spaces centres, not boxes, so twelve real-world labels in an
        // 80-column plot used to land in a pile of half-overwritten boxes.
        using var buffer = new CellBuffer(80, 24);
        var graph = Estate(12);

        graph.Render(buffer, new Rect(0, 0, 80, 24));

        await Assert.That(FirstOverlap(graph.RenderedBoxes)).IsEqualTo((-1, -1));
    }

    [Test]
    public async Task Render_ADozenLongLabels_KeepsEveryLabelReadable()
    {
        using var buffer = new CellBuffer(80, 24);
        var graph = Estate(12);

        graph.Render(buffer, new Rect(0, 0, 80, 24));

        for (var i = 0; i < 12; i++)
        {
            await Assert.That(ContainsText(buffer, $"app-debble-service{i:00}")).IsTrue();
        }
    }

    [Test]
    public async Task Render_EveryBox_StaysInsideThePlot()
    {
        using var buffer = new CellBuffer(80, 24);
        var graph = Estate(12);
        graph.Title = "Topology";

        graph.Render(buffer, new Rect(0, 0, 80, 24));

        foreach (var box in graph.RenderedBoxes)
        {
            await Assert.That(box.X).IsGreaterThanOrEqualTo(0);
            await Assert.That(box.Y).IsGreaterThanOrEqualTo(1); // the title row is not the plot
            await Assert.That(box.Right).IsLessThanOrEqualTo(80);
            await Assert.That(box.Bottom).IsLessThanOrEqualTo(24);
        }
    }

    [Test]
    public async Task Render_BoxesThatAlreadyFit_AreNotMoved()
    {
        // The repair must be a no-op on a graph that never had a problem: a picture that shifts
        // when nothing was wrong is a regression in its own right.
        using var buffer = new CellBuffer(W, H);
        var graph = ThreeNodeGraph();

        graph.Render(buffer, new Rect(0, 0, W, H));
        var boxes = graph.RenderedBoxes.ToArray();

        await Assert.That(FirstOverlap(boxes)).IsEqualTo((-1, -1));

        // Three short labels in a 60×24 plot are laid out on a wide circle; the y coordinates
        // are the raw projection, not a row band.
        var distinctRows = boxes.Select(b => b.Y).Distinct().Count();
        await Assert.That(distinctRows).IsGreaterThan(1);
    }

    [Test]
    public async Task Render_BoxPacking_StaysDeterministic()
    {
        using var first = new CellBuffer(80, 24);
        using var second = new CellBuffer(80, 24);

        var a = Estate(12);
        var b = Estate(12);
        a.Render(first, new Rect(0, 0, 80, 24));
        b.Render(second, new Rect(0, 0, 80, 24));

        await Assert.That(b.RenderedBoxes).IsEquivalentTo(a.RenderedBoxes);
        for (var y = 0; y < 24; y++)
        {
            for (var x = 0; x < 80; x++)
            {
                await Assert.That(second.GetCell(x, y)).IsEqualTo(first.GetCell(x, y));
            }
        }
    }

    [Test]
    public async Task Render_RecoloringANode_DoesNotMoveTheBoxes()
    {
        // The layout signature hashes ids and edge endpoints only. The packing must not smuggle
        // color back in through the projection step.
        using var plain = new CellBuffer(80, 24);
        using var colored = new CellBuffer(80, 24);

        var a = Estate(12);
        var b = Estate(12);
        b.GraphNodes[3].Color = new Color(220, 100, 100);

        a.Render(plain, new Rect(0, 0, 80, 24));
        b.Render(colored, new Rect(0, 0, 80, 24));

        await Assert.That(b.RenderedBoxes).IsEquivalentTo(a.RenderedBoxes);
    }

    [Test]
    public async Task Render_ClickAfterPacking_StillHitsTheRightNode()
    {
        // The packing rewrites the boxes the mouse hit-test uses; if it ran after they were
        // captured, every click in a crowded graph would select the wrong node.
        using var buffer = new CellBuffer(80, 24);
        var graph = Estate(12);
        graph.Render(buffer, new Rect(0, 0, 80, 24));

        var (x, y) = FindText(buffer, "app-debble-service07");
        await Assert.That(x).IsGreaterThanOrEqualTo(0);

        graph.OnMouseEvent(new MouseEvent(x, y, MouseButton.Left, MouseAction.Press));

        await Assert.That(graph.SelectedNode).IsSameReferenceAs(graph.GraphNodes[7]);
    }
}
