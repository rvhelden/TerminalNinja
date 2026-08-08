using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
///
/// The view is zoomable and pannable through <see cref="Zoom"/>, <see cref="PanX"/> and
/// <see cref="PanY"/>: <c>+</c>/<c>-</c> (or the mouse wheel) zoom, <c>Shift</c> plus an
/// arrow key pans, and <c>0</c> resets to fit-all. At <c>Zoom = 1</c> with no pan the whole
/// graph is fitted to the control exactly as it always was.
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

    /// <summary>
    /// Lower bound on <see cref="Zoom"/>. Fit-all is already the whole graph, so there is nothing
    /// below it to reveal — zooming further out would only shrink the picture into a corner.
    /// </summary>
    private const double MinZoom = 1.0;

    /// <summary>Upper bound on <see cref="Zoom"/>.</summary>
    private const double MaxZoom = 10.0;

    /// <summary>Multiplicative step per zoom keystroke or wheel notch.</summary>
    private const double ZoomStep = 1.25;

    /// <summary>Pan step per keystroke, as a fraction of the visible viewport.</summary>
    private const double PanStep = 0.1;

    /// <summary>Cached normalized [0,1]² node positions and the data signature they were computed for.</summary>
    private double[] _layoutX = [];
    private double[] _layoutY = [];
    private int _layoutSignature;

    /// <summary>Node box rects from the last render, used to map clicks to nodes.</summary>
    private Rect[] _renderedBoxes = [];

    /// <summary>The node boxes as last drawn. Exposed so tests can assert on the packing directly.</summary>
    internal IReadOnlyList<Rect> RenderedBoxes => _renderedBoxes;

    /// <summary>
    /// The signature the cached simulation was computed for. Exposed so tests can pin that zoom
    /// and pan never invalidate it — they are projection-time concerns, not layout inputs.
    /// </summary>
    internal int LayoutSignature => _layoutSignature;

    /// <summary>Guards the SelectedIndex/SelectedNode two-way sync against re-entrancy.</summary>
    private bool _syncing;

    public NodeGraph()
    {
        DefaultStyleKey = typeof(NodeGraph);
        _nodes.CollectionChanged += OnDataCollectionChanged;
        _nodes.CollectionChanged += OnNodesSourceCollectionChanged;
        _edges.CollectionChanged += OnDataCollectionChanged;
    }

    private readonly ObservableCollection<GraphNode> _nodes = [];
    private readonly ObservableCollection<GraphEdge> _edges = [];

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty GraphNodesSourceProperty =
        DependencyProperty.Register(nameof(GraphNodesSource), typeof(IEnumerable), typeof(NodeGraph),
            new FrameworkPropertyMetadata(null, affectsRender: true,
                propertyChangedCallback: (d, e) => ((NodeGraph)d).RebindNodesSource(e.OldValue, e.NewValue)));

    public static readonly DependencyProperty GraphEdgesSourceProperty =
        DependencyProperty.Register(nameof(GraphEdgesSource), typeof(IEnumerable), typeof(NodeGraph),
            new FrameworkPropertyMetadata(null, affectsRender: true,
                propertyChangedCallback: (d, e) => ((NodeGraph)d).RebindCollection(e.OldValue, e.NewValue)));

    public static readonly DependencyProperty LayoutIterationsProperty =
        DependencyProperty.Register(nameof(LayoutIterations), typeof(int), typeof(NodeGraph),
            new FrameworkPropertyMetadata(60, affectsRender: true, propertyChangedCallback: null,
                coerceValueCallback: (_, value) => Math.Clamp((int)value!, 1, MaxIterations)));

    public static readonly DependencyProperty ShowEdgeArrowsProperty =
        DependencyProperty.Register(nameof(ShowEdgeArrows), typeof(bool), typeof(NodeGraph),
            new FrameworkPropertyMetadata(true, affectsRender: true));

    public static readonly DependencyProperty ZoomProperty =
        DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(NodeGraph),
            new FrameworkPropertyMetadata(1.0, affectsRender: true,
                propertyChangedCallback: (d, _) => ((NodeGraph)d).ClampPanToZoom(),
                coerceValueCallback: (_, value) => Math.Clamp((double)value!, MinZoom, MaxZoom))
            { BindsTwoWayByDefault = true });

    public static readonly DependencyProperty PanXProperty =
        DependencyProperty.Register(nameof(PanX), typeof(double), typeof(NodeGraph),
            new FrameworkPropertyMetadata(0.0, affectsRender: true, propertyChangedCallback: null,
                coerceValueCallback: (d, value) => ((NodeGraph)d).ClampPan((double)value!))
            { BindsTwoWayByDefault = true });

    public static readonly DependencyProperty PanYProperty =
        DependencyProperty.Register(nameof(PanY), typeof(double), typeof(NodeGraph),
            new FrameworkPropertyMetadata(0.0, affectsRender: true, propertyChangedCallback: null,
                coerceValueCallback: (d, value) => ((NodeGraph)d).ClampPan((double)value!))
            { BindsTwoWayByDefault = true });

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

    /// <summary>
    /// Whether each edge gets a direction marker at its target end. Default true.
    /// </summary>
    /// <remarks>
    /// <see cref="GraphEdge"/> is directional, but a braille line is not: without the marker the
    /// only place the direction shows is a detail pane the graph does not own.
    /// </remarks>
    public bool ShowEdgeArrows
    {
        get => (bool)GetValue(ShowEdgeArrowsProperty)!;
        set => SetValue(ShowEdgeArrowsProperty, value);
    }

    /// <summary>
    /// Magnification of the fitted layout. 1.0 (the default) fits the whole graph to the control,
    /// exactly as this chart has always drawn it; 2.0 shows the middle quarter of it. Clamped to
    /// [1, 10]. Two-way bindable so a consumer's own zoom control stays in step with the keys.
    /// </summary>
    /// <remarks>
    /// A terminal cannot draw a bigger glyph, so zoom does not enlarge the boxes — it spreads the
    /// node <em>positions</em> apart and shows fewer of them. That is exactly what a crowded graph
    /// needs: at fit-all a large estate is a wall of touching boxes that the packer has to squeeze
    /// into rows, and the same graph at 3× has room to draw the nodes where the layout actually
    /// put them.
    ///
    /// Zoom and pan are applied when the cached layout is projected onto the plot. They are
    /// deliberately absent from the layout signature, so changing either re-projects the picture
    /// without re-running the force simulation and without reshuffling it.
    /// </remarks>
    public double Zoom
    {
        get => (double)GetValue(ZoomProperty)!;
        set => SetValue(ZoomProperty, value);
    }

    /// <summary>
    /// Horizontal pan, as an offset of the viewport centre from the graph centre in units of the
    /// whole graph's width. 0 (the default) centres the graph. Two-way bindable.
    /// </summary>
    /// <remarks>
    /// Clamped to ±(0.5 − 0.5/<see cref="Zoom"/>), which is the offset at which the viewport edge
    /// reaches the edge of the graph — so the viewport can never leave the graph and the user
    /// cannot pan the picture off-screen and be left staring at nothing. At <see cref="Zoom"/> 1
    /// that range collapses to zero: the whole graph is already visible, so there is nowhere to
    /// pan to, and fit-all therefore stays pixel-identical no matter what a caller assigns here.
    /// </remarks>
    public double PanX
    {
        get => (double)GetValue(PanXProperty)!;
        set => SetValue(PanXProperty, value);
    }

    /// <summary>Vertical pan. See <see cref="PanX"/> for the units and the clamp.</summary>
    public double PanY
    {
        get => (double)GetValue(PanYProperty)!;
        set => SetValue(PanYProperty, value);
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

    /// <summary>
    /// Subscribes to <see cref="GraphNodesSource"/> like the base class does, and additionally
    /// brings the selection back in line with whatever the new collection holds.
    /// </summary>
    private void RebindNodesSource(object? oldValue, object? newValue)
    {
        if (oldValue is INotifyCollectionChanged oldObservable)
        {
            oldObservable.CollectionChanged -= OnNodesSourceCollectionChanged;
        }

        if (newValue is INotifyCollectionChanged newObservable)
        {
            newObservable.CollectionChanged += OnNodesSourceCollectionChanged;
        }

        RebindCollection(oldValue, newValue);
        CoerceSelectionToNodes();
    }

    private void OnNodesSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        CoerceSelectionToNodes();

    /// <summary>
    /// Re-points <see cref="SelectedIndex"/> and <see cref="SelectedNode"/> at the current node
    /// list after that list has been replaced or mutated.
    /// </summary>
    /// <remarks>
    /// Without this the index survived a wholesale rebuild and kept addressing an ordinal in a
    /// list that no longer exists, so <see cref="SelectedNode"/> pointed at a node the graph was
    /// not drawing and every consumer had to reassign it by hand after each refresh. The order of
    /// preference is: the same node instance if the new list still contains it (a rebuild that
    /// reuses its objects keeps the user where they were), otherwise the same ordinal, otherwise
    /// nothing.
    ///
    /// Both writes go through <see cref="DependencyObject.SetValueInternal"/> for the same reason
    /// the two-way sync does — <see cref="DependencyObject.SetValue"/> would drop a two-way
    /// <c>{Binding SelectedNode}</c> on the first rebuild.
    /// </remarks>
    private void CoerceSelectionToNodes()
    {
        if (_syncing)
        {
            return;
        }

        var nodes = SelectableNodes;
        var selected = SelectedNode;

        var index = -1;
        for (var i = 0; i < nodes.Count; i++)
        {
            if (ReferenceEquals(nodes[i], selected))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            var current = SelectedIndex;
            index = current >= 0 && current < nodes.Count ? current : -1;
        }

        var node = index >= 0 ? nodes[index] : null;
        if (index == SelectedIndex && ReferenceEquals(node, selected))
        {
            return;
        }

        _syncing = true;
        SetValueInternal(SelectedIndexProperty, index);
        SetValueInternal(SelectedNodeProperty, node);
        _syncing = false;
    }

    // ─── Zoom and pan ────────────────────────────────────────────────

    /// <summary>True when the view is the plain fit-all one this chart drew before zoom existed.</summary>
    /// <remarks>
    /// The identity check is exact rather than epsilon-based on purpose: it is what lets the
    /// projection reuse the layout arrays untouched instead of running them through a remap whose
    /// round trip is only <em>almost</em> the identity in floating point. A single ULP there would
    /// move a box by a cell on some graphs and shift every existing screen.
    /// </remarks>
    private bool IsIdentityView => Zoom == 1.0 && PanX == 0.0 && PanY == 0.0;

    /// <summary>The furthest the viewport centre may sit from the graph centre at the current zoom.</summary>
    private double MaxPan => Math.Max(0.0, 0.5 - 0.5 / Zoom);

    private double ClampPan(double value)
    {
        var max = MaxPan;
        return double.IsNaN(value) ? 0.0 : Math.Clamp(value, -max, max);
    }

    /// <summary>Re-clamps the pan after a zoom change, since the allowed range is a function of zoom.</summary>
    private void ClampPanToZoom()
    {
        // SetCurrentValue, not SetValue: a two-way {Binding PanX} must survive the user zooming out.
        SetCurrentValue(PanXProperty, ClampPan(PanX));
        SetCurrentValue(PanYProperty, ClampPan(PanY));
    }

    /// <summary>Returns the view to fit-all: the whole graph, centred, at <see cref="Zoom"/> 1.</summary>
    public void ResetView()
    {
        SetCurrentValue(ZoomProperty, 1.0);
        SetCurrentValue(PanXProperty, 0.0);
        SetCurrentValue(PanYProperty, 0.0);
    }

    /// <summary>
    /// Multiplies the zoom, keeping the viewport centre where it is.
    /// </summary>
    /// <remarks>
    /// The anchor is the viewport centre rather than the selected node because the pan is stored
    /// <em>as</em> that centre, so zooming is a pure change of scale that moves nothing — the thing
    /// the user is looking at stays under their eyes. Anchoring on the selection would make zoom
    /// jump the view whenever the selection moved, and the selection is frequently -1 (nothing
    /// selected), which leaves no anchor at all.
    /// </remarks>
    private void ZoomBy(double factor) => SetCurrentValue(ZoomProperty, Zoom * factor);

    /// <summary>Moves the viewport by a tenth of its own width/height, so a pan step feels the same at any zoom.</summary>
    private void PanBy(int dx, int dy)
    {
        var step = PanStep / Zoom;
        if (dx != 0)
        {
            SetCurrentValue(PanXProperty, PanX + dx * step);
        }

        if (dy != 0)
        {
            SetCurrentValue(PanYProperty, PanY + dy * step);
        }
    }

    // ─── Input ───────────────────────────────────────────────────────

    /// <inheritdoc />
    /// <remarks>
    /// The view keys are chosen to coexist with the selection keys that were already here: the
    /// bare arrows, Home and End keep moving the node selection, panning takes the same arrows
    /// <em>with Shift</em>, <c>+</c>/<c>=</c> and <c>-</c>/<c>_</c> zoom, and <c>0</c> resets to
    /// fit-all. Nothing that used to be handled changes meaning.
    /// </remarks>
    public override bool OnKeyEvent(KeyEvent e)
    {
        switch (e.KeyChar)
        {
            case '+' or '=':
                ZoomBy(ZoomStep);
                return true;
            case '-' or '_':
                ZoomBy(1.0 / ZoomStep);
                return true;
            case '0':
                ResetView();
                return true;
        }

        if (e.Shift)
        {
            switch (e.Key)
            {
                case ConsoleKey.LeftArrow:
                    PanBy(-1, 0);
                    return true;
                case ConsoleKey.RightArrow:
                    PanBy(1, 0);
                    return true;
                case ConsoleKey.UpArrow:
                    PanBy(0, -1);
                    return true;
                case ConsoleKey.DownArrow:
                    PanBy(0, 1);
                    return true;
            }
        }

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
        // The wheel zooms about the viewport centre, the same anchor the keys use. Anchoring on
        // the pointer would need the plot rect, which only exists inside a render.
        switch (e.Action)
        {
            case MouseAction.ScrollUp:
                ZoomBy(ZoomStep);
                return;
            case MouseAction.ScrollDown:
                ZoomBy(1.0 / ZoomStep);
                return;
        }

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

        // Zoom and pan are a projection-time remap of the cached layout — like the box packing
        // below, and for the same reason: they must never touch the signature or the simulation.
        var (viewX, viewY) = ProjectView(nodes.Count);

        // Project normalized positions to box rects. At fit-all every box lands inside the plot;
        // zoomed in, the ones outside the viewport are meant to fall outside it.
        var boxes = new Rect[nodes.Count];
        for (var i = 0; i < nodes.Count; i++)
        {
            var label = Label(nodes[i]);
            var boxW = Math.Min(label.Length + 2, plot.Width);
            var boxH = Math.Min(3, plot.Height);
            var boxX = plot.X + (int)Math.Round(viewX[i] * (plot.Width - boxW));
            var boxY = plot.Y + (int)Math.Round(viewY[i] * (plot.Height - boxH));
            boxes[i] = new Rect(boxX, boxY, boxW, boxH);
        }

        // Only the boxes that are on screen get packed; dragging an off-viewport node back into
        // the plot would undo the zoom the user just asked for.
        SeparateBoxes(boxes, plot, viewX, viewY, VisibleIndexes(boxes, plot));
        _renderedBoxes = boxes;

        DrawEdges(buffer, plot, boxes, edges, edgeColors);
        var arrows = DrawEdgeArrows(buffer, plot, boxes, edges, edgeColors);
        DrawEdgeLabels(buffer, plot, boxes, edges, edgeColors, edgeLabels, arrows);

        var selectedBg = EffectiveSelectionBackground;
        for (var i = 0; i < nodes.Count; i++)
        {
            if (!boxes[i].Overlaps(plot))
            {
                continue;
            }

            var selected = i == SelectedIndex;
            DrawNodeBox(buffer, boxes[i], plot, Label(nodes[i]),
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
    /// Remaps the cached [0,1]² layout into view coordinates for the current zoom and pan, where
    /// [0,1] is still "the plot" — so everything downstream (packing, edges, labels, arrowheads,
    /// hit-testing) keeps working on ordinary projected rects and automatically operates in the
    /// zoomed space.
    /// </summary>
    /// <remarks>
    /// A node at layout position <c>u</c> maps to <c>(u − centre) · zoom + 0.5</c>, where the
    /// centre is <c>0.5 + pan</c>. At fit-all the cached arrays are handed back <em>by reference</em>
    /// rather than run through the algebra, so the result is bit-for-bit the projection this chart
    /// has always produced.
    /// </remarks>
    private (double[] X, double[] Y) ProjectView(int count)
    {
        if (IsIdentityView)
        {
            return (_layoutX, _layoutY);
        }

        var zoom = Zoom;
        var centerX = 0.5 + ClampPan(PanX);
        var centerY = 0.5 + ClampPan(PanY);
        var x = new double[count];
        var y = new double[count];
        for (var i = 0; i < count; i++)
        {
            x[i] = ((_layoutX[i] - centerX) * zoom) + 0.5;
            y[i] = ((_layoutY[i] - centerY) * zoom) + 0.5;
        }

        return (x, y);
    }

    /// <summary>Indexes of the boxes that are at least partly on screen, in node order.</summary>
    private static int[] VisibleIndexes(Rect[] boxes, Rect plot)
    {
        var visible = new List<int>(boxes.Length);
        for (var i = 0; i < boxes.Length; i++)
        {
            if (boxes[i].Overlaps(plot))
            {
                visible.Add(i);
            }
        }

        return [.. visible];
    }

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
        List<string> edgeLabels,
        List<Rect>? occupied)
    {
        var placed = occupied;

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

    /// <summary>
    /// Draws a direction marker for every edge in the cell nearest its target box, in the edge's
    /// own colour, and returns the cells it took so the labels can steer around them.
    /// </summary>
    /// <remarks>
    /// The marker is a single full-cell triangle (◀ ▶ ▲ ▼) rather than anything assembled out of
    /// braille: the line is drawn on a <see cref="BrailleCanvas"/> whose cells hold 2×4 dots, so a
    /// head built from dots is four times finer than the eye can resolve at this size and just
    /// thickens the last cell. A whole cell replaced by a triangle reads as an arrow. None of the
    /// four is wide (<c>WidthTable.IsWide</c>), so the marker cannot push a neighbouring cell out
    /// of column.
    ///
    /// Placement walks back along the segment from the target's centre and takes the first cell
    /// clear of every box, so the head sits against the box it points at. Following the edge-label
    /// convention, a marker with nowhere to go — boxes flush against each other, or a target
    /// pushed off the plot — is dropped whole rather than drawn somewhere misleading.
    /// </remarks>
    private List<Rect>? DrawEdgeArrows(
        CellBuffer buffer,
        Rect plot,
        Rect[] boxes,
        List<(int From, int To)> edges,
        List<Color> edgeColors)
    {
        if (!ShowEdgeArrows || edges.Count == 0)
        {
            return null;
        }

        List<Rect>? placed = null;

        for (var e = 0; e < edges.Count; e++)
        {
            var (from, to) = edges[e];
            var source = boxes[from];
            var target = boxes[to];

            // A marker points at a box; if that box is off the viewport there is nothing to point
            // at, and walking the segment back from a target hundreds of cells away is wasted work.
            if (!target.Overlaps(plot))
            {
                continue;
            }

            var sx = source.X + source.Width / 2.0;
            var sy = source.Y + source.Height / 2.0;
            var tx = target.X + target.Width / 2.0;
            var ty = target.Y + target.Height / 2.0;

            var dx = tx - sx;
            var dy = ty - sy;
            var length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 1e-9)
            {
                continue;
            }

            var cell = default(Rect);
            var found = false;
            for (var travelled = 0.0; travelled <= length; travelled += 0.25)
            {
                var candidate = new Rect(
                    (int)Math.Round(tx - dx / length * travelled),
                    (int)Math.Round(ty - dy / length * travelled),
                    1, 1);

                if (Array.Exists(boxes, box => box.Overlaps(candidate)))
                {
                    continue;
                }

                cell = candidate;
                found = true;
                break;
            }

            if (!found || cell.X < plot.X || cell.X >= plot.Right || cell.Y < plot.Y || cell.Y >= plot.Bottom)
            {
                continue;
            }

            buffer.SetChar(cell.X, cell.Y, ArrowGlyph(cell, target), edgeColors[e], Background);
            placed ??= [];
            placed.Add(cell);
        }

        return placed;
    }

    /// <summary>
    /// Picks the triangle that points from <paramref name="cell"/> into <paramref name="target"/>,
    /// by whichever side of the box the marker ended up furthest outside.
    /// </summary>
    /// <remarks>
    /// Taking the glyph from the edge's own direction instead looks right only for edges that run
    /// along an axis: an edge arriving from the lower right of its target is more vertical than
    /// horizontal as often as not, and the marker then sits under the box pointing sideways past
    /// it. The side the marker landed on is what a reader actually sees. Only the four axis
    /// directions are used — a diagonal glyph at this resolution is a corner block, which reads as
    /// a fragment of a box rather than as an arrow.
    /// </remarks>
    private static char ArrowGlyph(Rect cell, Rect target)
    {
        var left = target.X - cell.X;             // marker is left of the box
        var right = cell.X - (target.Right - 1);  // marker is right of the box
        var above = target.Y - cell.Y;            // marker is above the box
        var below = cell.Y - (target.Bottom - 1); // marker is below the box

        // Horizontal wins ties: the boxes are far wider than they are tall, so a marker level
        // with one is overwhelmingly more likely to be beside it than above or below it.
        var best = Math.Max(Math.Max(left, right), Math.Max(above, below));
        if (left == best)
        {
            return '▶';
        }

        if (right == best)
        {
            return '◀';
        }

        return above == best ? '▼' : '▲';
    }

    /// <summary>
    /// Nudges the projected boxes apart until none of them overlaps, leaving them exactly where
    /// the force layout put them when they already fit.
    /// </summary>
    /// <remarks>
    /// The force layout spaces node <em>centres</em> in a unit square and knows nothing of the
    /// box drawn around each one, so a dozen nodes with real-world labels in an 80-column plot
    /// produce a pile of half-overwritten boxes. It cannot be fixed inside the layout: box widths
    /// come from the labels, and the layout signature deliberately excludes labels so that
    /// relabelling or recolouring never moves the picture. So the fix belongs here, at projection
    /// time, and it runs only when there is something to fix — a graph that already fits renders
    /// exactly as it did before.
    ///
    /// The repair packs boxes into rows. Every box is three rows tall, so the plot holds a whole
    /// number of them; each node keeps the row band its layout y put it in, and within a band the
    /// boxes keep their layout x order and are pushed apart just enough to leave a column between
    /// them. A band with no room left hands the node to the nearest band that has some, so the
    /// result preserves the layout's up/down and left/right structure while guaranteeing clear
    /// boxes — for as long as the plot has room at all. Past that (hundreds of nodes in a pane
    /// this size) boxes are stacked in their preferred band and will still overlap; nothing
    /// legible exists at that density and dropping nodes would break selection by index.
    ///
    /// Deterministic throughout: every decision is a function of the layout, the labels and the
    /// plot size, with node index as the only tie-break.
    /// </remarks>
    private static void SeparateBoxes(Rect[] boxes, Rect plot, double[] layoutX, double[] layoutY, int[] subset)
    {
        var n = subset.Length;
        if (n < 2 || !AnyOverlap(boxes, subset))
        {
            return;
        }

        var boxH = boxes[subset[0]].Height;
        if (boxH <= 0)
        {
            return;
        }

        var bandCount = Math.Max(1, plot.Height / boxH);
        var bandStride = bandCount > 1 ? (plot.Height - boxH) / (bandCount - 1) : 0;

        // Preferred band from the layout's own y, then a stable sweep order: band first, then
        // left to right, then node index.
        var order = (int[])subset.Clone();
        var band = new int[boxes.Length];
        foreach (var i in subset)
        {
            band[i] = Math.Clamp((int)Math.Round(layoutY[i] * (bandCount - 1)), 0, bandCount - 1);
        }

        Array.Sort(order, (a, b) =>
        {
            var byBand = band[a].CompareTo(band[b]);
            if (byBand != 0)
            {
                return byBand;
            }

            var byX = layoutX[a].CompareTo(layoutX[b]);
            return byX != 0 ? byX : a.CompareTo(b);
        });

        // Fill bands, spilling to the nearest band with room. A band's cost is the widths it
        // already carries plus one separating column per box after the first.
        var used = new int[bandCount];
        var counts = new int[bandCount];
        var members = new List<int>[bandCount];
        for (var b = 0; b < bandCount; b++)
        {
            members[b] = [];
        }

        foreach (var i in order)
        {
            var width = boxes[i].Width;
            var chosen = -1;
            for (var offset = 0; offset < bandCount && chosen < 0; offset++)
            {
                // Below first, then above, so the choice is a function of the data alone.
                for (var sign = 0; sign < 2; sign++)
                {
                    if (offset == 0 && sign == 1)
                    {
                        break; // the preferred band is one band, not two
                    }

                    var candidate = band[i] + (sign == 0 ? offset : -offset);
                    if (candidate < 0 || candidate >= bandCount)
                    {
                        continue;
                    }

                    var cost = used[candidate] + width + (counts[candidate] > 0 ? 1 : 0);
                    if (cost <= plot.Width)
                    {
                        chosen = candidate;
                        break;
                    }
                }
            }

            if (chosen < 0)
            {
                chosen = band[i]; // genuinely out of room; stack it where it wanted to be
            }

            used[chosen] += boxes[i].Width + (counts[chosen] > 0 ? 1 : 0);
            counts[chosen]++;
            members[chosen].Add(i);
        }

        for (var b = 0; b < bandCount; b++)
        {
            var y = plot.Y + b * bandStride;
            var row = members[b];

            // Left to right: keep each box at its projected x unless the previous one is in the
            // way, then walk back from the right edge so the last box still fits.
            var cursor = plot.X;
            foreach (var i in row)
            {
                var x = Math.Max(boxes[i].X, cursor);
                boxes[i] = new Rect(x, y, boxes[i].Width, boxes[i].Height);
                cursor = x + boxes[i].Width + 1;
            }

            var limit = plot.Right;
            for (var k = row.Count - 1; k >= 0; k--)
            {
                var i = row[k];
                var x = Math.Max(plot.X, Math.Min(boxes[i].X, limit - boxes[i].Width));
                boxes[i] = new Rect(x, y, boxes[i].Width, boxes[i].Height);
                limit = x - 1;
            }
        }
    }

    private static bool AnyOverlap(Rect[] boxes, int[] subset)
    {
        for (var i = 0; i < subset.Length; i++)
        {
            for (var j = i + 1; j < subset.Length; j++)
            {
                if (boxes[subset[i]].Overlaps(boxes[subset[j]]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int PixelX(Rect box, Rect plot) => (box.X - plot.X + box.Width / 2) * 2 + 1;

    private static int PixelY(Rect box, Rect plot) => (box.Y - plot.Y + box.Height / 2) * 4 + 2;

    /// <summary>
    /// Draws one node box, clipped to <paramref name="clip"/> (the plot).
    /// </summary>
    /// <remarks>
    /// Clipping only ever bites when the view is zoomed or panned — at fit-all every box is inside
    /// the plot by construction, so every guard here passes and the output is unchanged. It matters
    /// once zoom pushes a box half out of the viewport: the plot is not the whole buffer (the title
    /// row and the truncation notice sit outside it), so an unclipped box would scribble over them.
    /// </remarks>
    private static void DrawNodeBox(CellBuffer buffer, Rect box, Rect clip, string label, Color border, Color labelFg, Color bg)
    {
        if (box.Width < 2 || box.Height < 2)
        {
            return;
        }

        // Interior first: overpaints any edge lines passing under the box.
        buffer.FillRect(box.Intersect(clip).Intersect(new Rect(0, 0, buffer.Width, buffer.Height)), new Cell(' ', labelFg, bg));

        var right = box.Right - 1;
        var bottom = box.Bottom - 1;
        for (var xx = box.X + 1; xx < right; xx++)
        {
            SetClipped(buffer, clip, xx, box.Y, '─', border, bg);
            SetClipped(buffer, clip, xx, bottom, '─', border, bg);
        }

        for (var yy = box.Y + 1; yy < bottom; yy++)
        {
            SetClipped(buffer, clip, box.X, yy, '│', border, bg);
            SetClipped(buffer, clip, right, yy, '│', border, bg);
        }

        SetClipped(buffer, clip, box.X, box.Y, '┌', border, bg);
        SetClipped(buffer, clip, right, box.Y, '┐', border, bg);
        SetClipped(buffer, clip, box.X, bottom, '└', border, bg);
        SetClipped(buffer, clip, right, bottom, '┘', border, bg);

        if (box.Height < 3)
        {
            return;
        }

        var labelX = box.X + 1;
        var labelY = box.Y + 1;
        if (labelY < clip.Y || labelY >= clip.Bottom)
        {
            return;
        }

        // Clip the label horizontally by skipping the characters that fall left of the plot and
        // shortening the run that falls right of it.
        var skip = Math.Max(0, clip.X - labelX);
        var width = Math.Min(box.Width - 2, clip.Right - labelX) - skip;
        if (width <= 0 || skip >= label.Length)
        {
            return;
        }

        DrawString(buffer, labelX + skip, labelY, skip == 0 ? label : label[skip..], labelFg, bg, width);
    }

    private static void SetClipped(CellBuffer buffer, Rect clip, int x, int y, char c, Color fg, Color bg)
    {
        if (clip.Contains(x, y))
        {
            buffer.SetChar(x, y, c, fg, bg);
        }
    }
}
