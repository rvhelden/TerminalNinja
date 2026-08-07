using System.Collections;
using System.Collections.ObjectModel;
using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Input;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls.Charts;

/// <summary>
/// A node graph for topologies and networks: each <see cref="GraphNode"/> is drawn as a
/// small labeled box and each <see cref="GraphEdge"/> as a braille line connecting the
/// boxes it references by <see cref="GraphNode.Id"/>, optionally captioned at its midpoint
/// with <see cref="GraphEdge.Label"/>. Node positions are computed
/// automatically with a deterministic force-directed (Fruchterman–Reingold) layout —
/// no coordinates are supplied by the caller. Data can be supplied inline via
/// <see cref="GraphNodes"/> / <see cref="GraphEdges"/> or bound through
/// <see cref="GraphNodesSource"/> / <see cref="GraphEdgesSource"/>.
///
/// The layout is seeded from node order (not randomness), so the same data always
/// renders the same picture. Only the first 500 nodes are laid out; a "… N more"
/// notice marks any remainder. <see cref="ChartBase.ShowAxes"/>,
/// <see cref="ChartBase.ShowGrid"/> and <see cref="ChartBase.ShowLegend"/> have no
/// effect on this chart.
///
/// The chart is interactive: it is focusable, the arrow keys (and Home/End) move the
/// node selection in node order, and a left click selects the clicked box. The
/// selected node is highlighted and exposed through <see cref="SelectedIndex"/> and
/// <see cref="SelectedNode"/>, both of which are two-way bindable.
/// </summary>
[ContentProperty("GraphNodes")]
public sealed class NodeGraph : ChartBase
{
    /// <summary>Upper bound on nodes the O(n²) force layout runs over; the rest are reported via the truncation notice.</summary>
    private const int MaxLayoutNodes = 500;

    /// <summary>Hard ceiling for <see cref="LayoutIterations"/>.</summary>
    private const int MaxIterations = 200;

    /// <summary>Iteration displacement below which the layout is considered converged.</summary>
    private const double ConvergenceEpsilon = 1e-4;

    /// <summary>Cached normalized [0,1]² node positions and the data signature they were computed for.</summary>
    private double[] _layoutX = [];
    private double[] _layoutY = [];
    private int _layoutSignature;

    /// <summary>Node box rects from the last render, used to map clicks to nodes.</summary>
    private Rect[] _renderedBoxes = [];

    /// <summary>Guards the SelectedIndex/SelectedNode two-way sync against re-entrancy.</summary>
    private bool _syncing;

    public NodeGraph()
    {
        DefaultStyleKey = typeof(NodeGraph);
        _nodes.CollectionChanged += OnDataCollectionChanged;
        _edges.CollectionChanged += OnDataCollectionChanged;
    }

    private readonly ObservableCollection<GraphNode> _nodes = [];
    private readonly ObservableCollection<GraphEdge> _edges = [];

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty GraphNodesSourceProperty =
        DependencyProperty.Register(nameof(GraphNodesSource), typeof(IEnumerable), typeof(NodeGraph),
            new FrameworkPropertyMetadata(null, affectsRender: true,
                propertyChangedCallback: (d, e) => ((NodeGraph)d).RebindCollection(e.OldValue, e.NewValue)));

    public static readonly DependencyProperty GraphEdgesSourceProperty =
        DependencyProperty.Register(nameof(GraphEdgesSource), typeof(IEnumerable), typeof(NodeGraph),
            new FrameworkPropertyMetadata(null, affectsRender: true,
                propertyChangedCallback: (d, e) => ((NodeGraph)d).RebindCollection(e.OldValue, e.NewValue)));

    public static readonly DependencyProperty LayoutIterationsProperty =
        DependencyProperty.Register(nameof(LayoutIterations), typeof(int), typeof(NodeGraph),
            new FrameworkPropertyMetadata(60, affectsRender: true, propertyChangedCallback: null,
                coerceValueCallback: (_, value) => Math.Clamp((int)value!, 1, MaxIterations)));

    public static readonly DependencyProperty SelectedIndexProperty =
        DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(NodeGraph),
            new FrameworkPropertyMetadata(-1, affectsRender: true,
                propertyChangedCallback: OnSelectedIndexChanged) { BindsTwoWayByDefault = true });

    public static readonly DependencyProperty SelectedNodeProperty =
        DependencyProperty.Register(nameof(SelectedNode), typeof(object), typeof(NodeGraph),
            new FrameworkPropertyMetadata(null, affectsRender: true,
                propertyChangedCallback: OnSelectedNodeChanged) { BindsTwoWayByDefault = true });

    // ─── CLR Property Wrappers ───────────────────────────────────────

    /// <summary>The inline collection of nodes. Used when <see cref="GraphNodesSource"/> is null.</summary>
    public IList<GraphNode> GraphNodes => _nodes;

    /// <summary>The inline collection of edges. Used when <see cref="GraphEdgesSource"/> is null.</summary>
    public IList<GraphEdge> GraphEdges => _edges;

    /// <summary>Optional bound nodes collection. Overrides <see cref="GraphNodes"/> when set.</summary>
    public IEnumerable? GraphNodesSource
    {
        get => (IEnumerable?)GetValue(GraphNodesSourceProperty);
        set => SetValue(GraphNodesSourceProperty, value);
    }

    /// <summary>Optional bound edges collection. Overrides <see cref="GraphEdges"/> when set.</summary>
    public IEnumerable? GraphEdgesSource
    {
        get => (IEnumerable?)GetValue(GraphEdgesSourceProperty);
        set => SetValue(GraphEdgesSourceProperty, value);
    }

    /// <summary>
    /// Number of force-directed iterations to run (the layout stops earlier once it
    /// converges). Clamped to [1, 200]. Default 60.
    /// </summary>
    public int LayoutIterations
    {
        get => (int)GetValue(LayoutIterationsProperty)!;
        set => SetValue(LayoutIterationsProperty, value);
    }

    /// <summary>Index of the selected node in the effective node list (-1 = none). Two-way bindable.</summary>
    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty)!;
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>The selected node, kept in sync with <see cref="SelectedIndex"/>. Two-way bindable.</summary>
    public object? SelectedNode
    {
        get => GetValue(SelectedNodeProperty);
        set => SetValue(SelectedNodeProperty, value);
    }

    private List<GraphNode> EffectiveNodes =>
        GraphNodesSource != null ? [.. Enumerate<GraphNode>(GraphNodesSource)] : [.. _nodes];

    private List<GraphEdge> EffectiveEdges =>
        GraphEdgesSource != null ? [.. Enumerate<GraphEdge>(GraphEdgesSource)] : [.. _edges];

    /// <summary>The nodes that are actually laid out and selectable (capped at <see cref="MaxLayoutNodes"/>).</summary>
    private List<GraphNode> SelectableNodes
    {
        get
        {
            var nodes = EffectiveNodes;
            return nodes.Count > MaxLayoutNodes ? nodes.GetRange(0, MaxLayoutNodes) : nodes;
        }
    }

    // ─── Selection sync ──────────────────────────────────────────────

    private static void OnSelectedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var graph = (NodeGraph)d;
        if (graph._syncing)
        {
            return;
        }

        var nodes = graph.SelectableNodes;
        var index = (int)e.NewValue!;
        graph._syncing = true;
        // SetValueInternal, not the public setter: the setter goes through SetValue, which clears
        // any binding on SelectedNode, so a two-way {Binding SelectedNode} would be destroyed the
        // first time the user moved the selection. This keeps the expression and still raises the
        // change so the binding writes back.
        graph.SetValueInternal(SelectedNodeProperty, index >= 0 && index < nodes.Count ? nodes[index] : null);
        graph._syncing = false;
    }

    private static void OnSelectedNodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var graph = (NodeGraph)d;
        if (graph._syncing)
        {
            return;
        }

        var nodes = graph.SelectableNodes;
        var index = -1;
        for (var i = 0; i < nodes.Count; i++)
        {
            if (ReferenceEquals(nodes[i], e.NewValue))
            {
                index = i;
                break;
            }
        }

        graph._syncing = true;
        graph.SetValueInternal(SelectedIndexProperty, index);
        graph._syncing = false;
    }

    // ─── Input ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public override bool OnKeyEvent(KeyEvent e)
    {
        var count = SelectableNodes.Count;
        if (count <= 0)
        {
            return false;
        }

        var current = SelectedIndex;
        switch (e.Key)
        {
            case ConsoleKey.UpArrow:
            case ConsoleKey.LeftArrow:
                SetCurrentValue(SelectedIndexProperty, current < 0 ? count - 1 : Math.Max(0, current - 1));
                return true;
            case ConsoleKey.DownArrow:
            case ConsoleKey.RightArrow:
                SetCurrentValue(SelectedIndexProperty, current < 0 ? 0 : Math.Min(count - 1, current + 1));
                return true;
            case ConsoleKey.Home:
                SetCurrentValue(SelectedIndexProperty, 0);
                return true;
            case ConsoleKey.End:
                SetCurrentValue(SelectedIndexProperty, count - 1);
                return true;
            default:
                return false;
        }
    }

    /// <inheritdoc />
    public override void OnMouseEvent(MouseEvent e)
    {
        if (e is not { Action: MouseAction.Press, Button: MouseButton.Left })
        {
            return;
        }

        // Hit-test against the node boxes captured during the last render.
        for (var i = 0; i < _renderedBoxes.Length; i++)
        {
            var box = _renderedBoxes[i];
            if (e.X >= box.X && e.X < box.Right && e.Y >= box.Y && e.Y < box.Bottom)
            {
                SetCurrentValue(SelectedIndexProperty, i);
                return;
            }
        }
    }

    // ─── Force-directed layout ───────────────────────────────────────

    /// <summary>
    /// Hash of everything the layout depends on: node identity/order, edge endpoints,
    /// and the iteration budget. Label and color changes deliberately don't invalidate.
    /// </summary>
    private int ComputeLayoutSignature(List<GraphNode> nodes, List<(int From, int To)> edges)
    {
        var hash = new HashCode();
        hash.Add(LayoutIterations);
        foreach (var node in nodes)
        {
            hash.Add(node.Id);
        }

        foreach (var (from, to) in edges)
        {
            hash.Add(from);
            hash.Add(to);
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// Computes normalized [0,1]² positions for <paramref name="nodes"/> with a
    /// Fruchterman–Reingold simulation, reusing the cached result when the data is
    /// unchanged. Positions are seeded on a circle by node index so the result is
    /// deterministic.
    /// </summary>
    private void EnsureLayout(List<GraphNode> nodes, List<(int From, int To)> edges)
    {
        var signature = ComputeLayoutSignature(nodes, edges);
        if (signature == _layoutSignature && _layoutX.Length == nodes.Count)
        {
            return;
        }

        var n = nodes.Count;
        var x = new double[n];
        var y = new double[n];

        // Circular seed: unique, deterministic starting positions.
        for (var i = 0; i < n; i++)
        {
            var angle = 2 * Math.PI * i / n;
            x[i] = 0.5 + 0.4 * Math.Cos(angle);
            y[i] = 0.5 + 0.4 * Math.Sin(angle);
        }

        if (n > 1)
        {
            var k = 1.0 / Math.Sqrt(n); // ideal spring length for a unit-square area
            var dispX = new double[n];
            var dispY = new double[n];
            var iterations = LayoutIterations;

            for (var iter = 0; iter < iterations; iter++)
            {
                Array.Clear(dispX);
                Array.Clear(dispY);

                // Repulsion between every node pair.
                for (var i = 0; i < n; i++)
                {
                    for (var j = i + 1; j < n; j++)
                    {
                        var dx = x[i] - x[j];
                        var dy = y[i] - y[j];
                        var distSq = dx * dx + dy * dy;
                        // Coincident nodes get a deterministic index-based nudge instead of a random one.
                        if (distSq < 1e-12)
                        {
                            dx = 1e-3 * (i - j);
                            dy = 1e-3;
                            distSq = dx * dx + dy * dy;
                        }

                        var dist = Math.Sqrt(distSq);
                        var force = k * k / dist;
                        var fx = dx / dist * force;
                        var fy = dy / dist * force;
                        dispX[i] += fx;
                        dispY[i] += fy;
                        dispX[j] -= fx;
                        dispY[j] -= fy;
                    }
                }

                // Attraction along edges.
                foreach (var (from, to) in edges)
                {
                    var dx = x[from] - x[to];
                    var dy = y[from] - y[to];
                    var dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist < 1e-9)
                    {
                        continue;
                    }

                    var force = dist * dist / k;
                    var fx = dx / dist * force;
                    var fy = dy / dist * force;
                    dispX[from] -= fx;
                    dispY[from] -= fy;
                    dispX[to] += fx;
                    dispY[to] += fy;
                }

                // Linearly cooling temperature caps per-iteration movement.
                var temperature = 0.1 * (1.0 - (double)iter / iterations);
                var totalMoved = 0.0;
                for (var i = 0; i < n; i++)
                {
                    var dist = Math.Sqrt(dispX[i] * dispX[i] + dispY[i] * dispY[i]);
                    if (dist < 1e-12)
                    {
                        continue;
                    }

                    var move = Math.Min(dist, temperature);
                    x[i] = Math.Clamp(x[i] + dispX[i] / dist * move, 0.0, 1.0);
                    y[i] = Math.Clamp(y[i] + dispY[i] / dist * move, 0.0, 1.0);
                    totalMoved += move;
                }

                if (totalMoved < ConvergenceEpsilon)
                {
                    break;
                }
            }
        }
        else if (n == 1)
        {
            x[0] = 0.5;
            y[0] = 0.5;
        }

        _layoutX = x;
        _layoutY = y;
        _layoutSignature = signature;
    }

    // ─── Rendering ───────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnRender(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds).Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        FillBackground(buffer, bounds);

        var allNodes = EffectiveNodes;
        if (allNodes.Count == 0)
        {
            _renderedBoxes = [];
            DrawString(buffer, bounds.X + 1, bounds.Y, "(no nodes)", Foreground, Background, bounds.Width - 2);
            return;
        }

        var top = bounds.Y;
        if (!string.IsNullOrEmpty(Title))
        {
            DrawString(buffer, bounds.X, top, Title, Foreground, Background, bounds.Width);
            top += 1;
        }

        var truncatedCount = Math.Max(0, allNodes.Count - MaxLayoutNodes);
        var nodes = truncatedCount > 0 ? allNodes.GetRange(0, MaxLayoutNodes) : allNodes;

        // Reserve the bottom row for the truncation notice so it never overlaps a box.
        var plotBottom = truncatedCount > 0 ? bounds.Bottom - 1 : bounds.Bottom;
        var plot = new Rect(bounds.X, top, bounds.Width, plotBottom - top);
        if (plot.Width <= 0 || plot.Height <= 0)
        {
            return;
        }

        // Resolve edge endpoints to node indexes; edges referencing unknown ids are dropped.
        var indexById = new Dictionary<string, int>();
        for (var i = 0; i < nodes.Count; i++)
        {
            indexById.TryAdd(nodes[i].Id, i);
        }

        var allEdges = EffectiveEdges;
        var edges = new List<(int From, int To)>(allEdges.Count);
        var edgeColors = new List<Color>(allEdges.Count);
        var edgeLabels = new List<string>(allEdges.Count);
        foreach (var edge in allEdges)
        {
            if (indexById.TryGetValue(edge.From, out var from) && indexById.TryGetValue(edge.To, out var to) && from != to)
            {
                edges.Add((from, to));
                edgeColors.Add(edge.Color.IsTransparent ? AxisColor : edge.Color);
                edgeLabels.Add(edge.Label);
            }
        }

        EnsureLayout(nodes, edges);

        // Project normalized positions to box rects fully inside the plot area.
        var boxes = new Rect[nodes.Count];
        for (var i = 0; i < nodes.Count; i++)
        {
            var label = Label(nodes[i]);
            var boxW = Math.Min(label.Length + 2, plot.Width);
            var boxH = Math.Min(3, plot.Height);
            var boxX = plot.X + (int)Math.Round(_layoutX[i] * (plot.Width - boxW));
            var boxY = plot.Y + (int)Math.Round(_layoutY[i] * (plot.Height - boxH));
            boxes[i] = new Rect(boxX, boxY, boxW, boxH);
        }

        _renderedBoxes = boxes;

        DrawEdges(buffer, plot, boxes, edges, edgeColors);
        DrawEdgeLabels(buffer, plot, boxes, edges, edgeColors, edgeLabels);

        var selectedBg = EffectiveSelectionBackground;
        for (var i = 0; i < nodes.Count; i++)
        {
            var selected = i == SelectedIndex;
            DrawNodeBox(buffer, boxes[i], Label(nodes[i]),
                border: ColorForSeries(i, nodes[i].Color),
                labelFg: selected ? SelectedForeground : Foreground,
                bg: selected ? selectedBg : Background);
        }

        if (truncatedCount > 0)
        {
            var notice = $"{Ellipsis} {truncatedCount} more";
            DrawString(buffer, bounds.Right - notice.Length, bounds.Bottom - 1, notice, LegendColor, Background, notice.Length);
        }
    }

    private static string Label(GraphNode node) => node.Name.Length > 0 ? node.Name : node.Id;

    /// <summary>
    /// Draws every edge as a braille line between box centers. Lines are grouped by
    /// color because a <see cref="BrailleCanvas"/> blits in a single color.
    /// </summary>
    /// <remarks>
    /// Where two edges cross, the one later in the collection wins the shared cells, and the
    /// groups are blitted in order of the last edge that joined each — so ordering the data
    /// puts an edge on top. This is a contract, not an accident: it used to fall out of
    /// <see cref="Dictionary{TKey,TValue}"/> enumeration order, which is unspecified, and it
    /// mattered. A graph is usually a hub, so every edge crosses near the center; a caller that
    /// listed its one failing edge first had it painted over by the healthy majority and left
    /// with a single visible cell.
    /// </remarks>
    private static void DrawEdges(CellBuffer buffer, Rect plot, Rect[] boxes, List<(int From, int To)> edges, List<Color> edgeColors)
    {
        if (edges.Count == 0)
        {
            return;
        }

        var order = new List<Color>();
        var canvases = new Dictionary<Color, BrailleCanvas>();

        for (var e = 0; e < edges.Count; e++)
        {
            var color = edgeColors[e];
            if (!canvases.TryGetValue(color, out var canvas))
            {
                canvases[color] = canvas = new BrailleCanvas(plot.Width, plot.Height);
            }

            // Re-rank the group to this edge's position, so "last edge wins" holds for colors
            // whose first edge came early but whose last one came late.
            order.Remove(color);
            order.Add(color);

            var (from, to) = edges[e];
            canvas.Line(
                PixelX(boxes[from], plot), PixelY(boxes[from], plot),
                PixelX(boxes[to], plot), PixelY(boxes[to], plot));
        }

        foreach (var color in order)
        {
            canvases[color].Blit(buffer, plot.X, plot.Y, color);
        }
    }

    /// <summary>
    /// Draws each labeled edge's <see cref="GraphEdge.Label"/> at the midpoint between the two
    /// boxes it joins, in the edge's own color.
    /// </summary>
    /// <remarks>
    /// A label is skipped rather than clipped when it would not fit: over a node box (the boxes
    /// are drawn afterwards and would eat half of it, leaving a fragment that reads as corruption),
    /// over a label already placed (two short edges in a dense graph land on the same cells), or
    /// outside the plot. Losing a number is recoverable; a half-drawn one is not readable at all.
    /// </remarks>
    private void DrawEdgeLabels(
        CellBuffer buffer,
        Rect plot,
        Rect[] boxes,
        List<(int From, int To)> edges,
        List<Color> edgeColors,
        List<string> edgeLabels)
    {
        List<Rect>? placed = null;

        for (var e = 0; e < edges.Count; e++)
        {
            var label = edgeLabels[e];
            if (string.IsNullOrEmpty(label) || label.Length > plot.Width)
            {
                continue;
            }

            var (from, to) = edges[e];
            var midX = (boxes[from].X + boxes[from].Width / 2 + boxes[to].X + boxes[to].Width / 2) / 2;
            var midY = (boxes[from].Y + boxes[from].Height / 2 + boxes[to].Y + boxes[to].Height / 2) / 2;

            var rect = new Rect(midX - label.Length / 2, midY, label.Length, 1);
            if (rect.X < plot.X || rect.Right > plot.Right || rect.Y < plot.Y || rect.Y >= plot.Bottom)
            {
                continue;
            }

            if (Array.Exists(boxes, box => box.Overlaps(rect)))
            {
                continue;
            }

            placed ??= [];
            if (placed.Exists(other => other.Overlaps(rect)))
            {
                continue;
            }

            placed.Add(rect);
            DrawString(buffer, rect.X, rect.Y, label, edgeColors[e], Background, label.Length);
        }
    }

    private static int PixelX(Rect box, Rect plot) => (box.X - plot.X + box.Width / 2) * 2 + 1;

    private static int PixelY(Rect box, Rect plot) => (box.Y - plot.Y + box.Height / 2) * 4 + 2;

    private static void DrawNodeBox(CellBuffer buffer, Rect box, string label, Color border, Color labelFg, Color bg)
    {
        if (box.Width < 2 || box.Height < 2)
        {
            return;
        }

        // Interior first: overpaints any edge lines passing under the box.
        buffer.FillRect(box.Intersect(new Rect(0, 0, buffer.Width, buffer.Height)), new Cell(' ', labelFg, bg));

        var right = box.Right - 1;
        var bottom = box.Bottom - 1;
        for (var xx = box.X + 1; xx < right; xx++)
        {
            buffer.SetChar(xx, box.Y, '─', border, bg);
            buffer.SetChar(xx, bottom, '─', border, bg);
        }

        for (var yy = box.Y + 1; yy < bottom; yy++)
        {
            buffer.SetChar(box.X, yy, '│', border, bg);
            buffer.SetChar(right, yy, '│', border, bg);
        }

        buffer.SetChar(box.X, box.Y, '┌', border, bg);
        buffer.SetChar(right, box.Y, '┐', border, bg);
        buffer.SetChar(box.X, bottom, '└', border, bg);
        buffer.SetChar(right, bottom, '┘', border, bg);

        if (box.Height >= 3)
        {
            DrawString(buffer, box.X + 1, box.Y + 1, label, labelFg, bg, box.Width - 2);
        }
    }
}
