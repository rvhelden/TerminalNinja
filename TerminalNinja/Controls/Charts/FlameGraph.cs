using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Input;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls.Charts;

/// <summary>
/// A flame graph (icicle layout, root at top): each <see cref="FlameNode"/> is drawn as
/// a horizontal bar whose width is proportional to its value relative to the root, with
/// children laid out proportionally in the row beneath their parent. Widths use eighth-
/// blocks for sub-cell precision. Set the tree via <see cref="Root"/> (the content
/// property), which may also be bound.
///
/// The graph is interactive: it is focusable, the arrow keys walk the tree (Up = parent,
/// Down = first child, Left/Right = siblings), and a left click selects the frame under the
/// cursor. The selected frame is highlighted and exposed via <see cref="SelectedNode"/>
/// (two-way bindable).
/// </summary>
[ContentProperty("Root")]
public sealed class FlameGraph : ChartBase
{
    /// <summary>A laid-out frame from the last render, used for navigation and hit-testing.</summary>
    private readonly record struct FrameEntry(FlameNode Node, int Depth, int X, int W, FlameNode? Parent);

    private readonly List<FrameEntry> _frames = [];
    private int _flameTop;

    public FlameGraph()
    {
        DefaultStyleKey = typeof(FlameGraph);
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty RootProperty =
        DependencyProperty.Register(nameof(Root), typeof(FlameNode), typeof(FlameGraph),
            new FrameworkPropertyMetadata(null, affectsRender: true));

    public static readonly DependencyProperty SelectedNodeProperty =
        DependencyProperty.Register(nameof(SelectedNode), typeof(object), typeof(FlameGraph),
            new FrameworkPropertyMetadata(null, affectsRender: true) { BindsTwoWayByDefault = true });

    // ─── CLR Property Wrappers ───────────────────────────────────────

    /// <summary>The root frame of the graph.</summary>
    public FlameNode? Root
    {
        get => (FlameNode?)GetValue(RootProperty);
        set => SetValue(RootProperty, value);
    }

    /// <summary>The selected frame. Navigate with the arrow keys or click. Two-way bindable.</summary>
    public object? SelectedNode
    {
        get => GetValue(SelectedNodeProperty);
        set => SetValue(SelectedNodeProperty, value);
    }

    // ─── Input ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public override bool OnKeyEvent(KeyEvent e)
    {
        if (_frames.Count == 0)
        {
            return false;
        }

        if (SelectedNode is not FlameNode sel || FindEntry(sel) is not { } entry)
        {
            SetCurrentValue(SelectedNodeProperty, Root);
            return true;
        }

        switch (e.Key)
        {
            case ConsoleKey.UpArrow:
                if (entry.Parent != null)
                {
                    SetCurrentValue(SelectedNodeProperty, entry.Parent);
                }

                return true;
            case ConsoleKey.DownArrow:
                var child = FirstChildOf(entry.Node);
                if (child != null)
                {
                    SetCurrentValue(SelectedNodeProperty, child);
                }

                return true;
            case ConsoleKey.LeftArrow:
                var prev = SiblingAtDepth(entry, -1);
                if (prev != null)
                {
                    SetCurrentValue(SelectedNodeProperty, prev);
                }

                return true;
            case ConsoleKey.RightArrow:
                var next = SiblingAtDepth(entry, +1);
                if (next != null)
                {
                    SetCurrentValue(SelectedNodeProperty, next);
                }

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

        var depth = e.Y - _flameTop;
        foreach (var f in _frames)
        {
            if (f.Depth == depth && e.X >= f.X && e.X < f.X + f.W)
            {
                SetCurrentValue(SelectedNodeProperty, f.Node);
                return;
            }
        }
    }

    private FrameEntry? FindEntry(FlameNode node)
    {
        foreach (var f in _frames)
        {
            if (ReferenceEquals(f.Node, node))
            {
                return f;
            }
        }

        return null;
    }

    private FlameNode? FirstChildOf(FlameNode node)
    {
        FrameEntry? best = null;
        foreach (var f in _frames)
        {
            if (ReferenceEquals(f.Parent, node) && (best == null || f.X < best.Value.X))
            {
                best = f;
            }
        }

        return best?.Node;
    }

    private FlameNode? SiblingAtDepth(FrameEntry entry, int direction)
    {
        // Frames on the same row, ordered left-to-right.
        var sameDepth = _frames.Where(f => f.Depth == entry.Depth).OrderBy(f => f.X).ToList();
        var idx = sameDepth.FindIndex(f => ReferenceEquals(f.Node, entry.Node));
        var target = idx + direction;
        return target >= 0 && target < sameDepth.Count ? sameDepth[target].Node : null;
    }

    // ─── Rendering ───────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnRender(CellBuffer buffer, Rect parentBounds)
    {
        _frames.Clear();

        var bounds = CalculateBounds(parentBounds).Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        FillBackground(buffer, bounds);

        var root = Root;
        if (root == null)
        {
            DrawString(buffer, bounds.X + 1, bounds.Y, "(no data)", Foreground, Background, bounds.Width - 2);
            return;
        }

        var top = bounds.Y;
        if (!string.IsNullOrEmpty(Title))
        {
            DrawString(buffer, bounds.X, top, Title, Foreground, Background, bounds.Width);
            top += 1;
        }

        var rowCount = bounds.Bottom - top;
        if (rowCount <= 0)
        {
            return;
        }

        _flameTop = top;
        RenderNode(buffer, root, depth: 0, xStart: bounds.X, widthCells: bounds.Width, top: top, maxRows: rowCount, parent: null);
    }

    private void RenderNode(CellBuffer buffer, FlameNode node, int depth, double xStart, double widthCells, int top, int maxRows, FlameNode? parent)
    {
        if (depth >= maxRows || widthCells < 1.0 / 8)
        {
            return;
        }

        var row = top + depth;
        var xCell = (int)Math.Round(xStart);
        var selected = ReferenceEquals(node, SelectedNode);
        var color = selected ? EffectiveSelectionBackground : ColorForNode(node, depth);
        var wCell = DrawFrame(buffer, xStart, widthCells, row, node.Name, color, selected);
        _frames.Add(new FrameEntry(node, depth, xCell, Math.Max(1, wCell), parent));

        // Lay out children proportionally within this node's width.
        var parentValue = EffectiveValue(node);
        if (parentValue <= 0)
        {
            return;
        }

        var childX = xStart;
        foreach (var child in node.Children)
        {
            var childWidth = EffectiveValue(child) / parentValue * widthCells;
            RenderNode(buffer, child, depth + 1, childX, childWidth, top, maxRows, node);
            childX += childWidth;
        }
    }

    private int DrawFrame(CellBuffer buffer, double xStart, double widthCells, int row, string name, Color color, bool selected)
    {
        var startEighths = (int)Math.Round(xStart * 8);
        var endEighths = (int)Math.Round((xStart + widthCells) * 8);
        var lengthEighths = Math.Max(1, endEighths - startEighths);
        var startCol = startEighths / 8;
        var fullCells = lengthEighths / 8;
        var rem = lengthEighths % 8;

        for (var i = 0; i < fullCells; i++)
        {
            var col = startCol + i;
            if (buffer.IsInBounds(col, row))
            {
                buffer.SetChar(col, row, '█', color, color);
            }
        }

        if (rem > 0)
        {
            var col = startCol + fullCells;
            if (buffer.IsInBounds(col, row))
            {
                buffer.SetChar(col, row, (char)(0x2590 - rem), color, Background);
            }
        }

        // Overlay the label on top of the frame, if it fits.
        if (fullCells >= 2 && !string.IsNullOrEmpty(name))
        {
            var textColor = selected
                ? SelectedForeground
                : Luminance(color) > 0.55 ? new Color(0, 0, 0) : new Color(255, 255, 255);
            DrawLabelOverBar(buffer, startCol + 1, row, name, textColor, color, fullCells - 1);
        }

        return fullCells + (rem > 0 ? 1 : 0);
    }

    private static void DrawLabelOverBar(CellBuffer buffer, int x, int y, string text, Color fg, Color bg, int maxWidth)
    {
        if (maxWidth <= 0)
        {
            return;
        }

        var truncate = text.Length > maxWidth;
        var count = truncate ? Math.Max(0, maxWidth - 1) : Math.Min(text.Length, maxWidth);
        for (var i = 0; i < count; i++)
        {
            if (buffer.IsInBounds(x + i, y))
            {
                buffer.SetChar(x + i, y, text[i], fg, bg);
            }
        }

        if (truncate && buffer.IsInBounds(x + count, y))
        {
            buffer.SetChar(x + count, y, Ellipsis, fg, bg);
        }
    }

    private Color ColorForNode(FlameNode node, int depth)
    {
        if (!node.Color.IsTransparent)
        {
            return node.Color;
        }

        // Vary color by depth and a stable hash of the name for visual separation.
        var index = depth + StableHash(node.Name);
        return ColorForSeries(index);
    }

    /// <summary>The node's weight, derived from children when its own value is zero.</summary>
    private static double EffectiveValue(FlameNode node)
    {
        if (node.Value > 0)
        {
            return node.Value;
        }

        var sum = 0.0;
        foreach (var child in node.Children)
        {
            sum += EffectiveValue(child);
        }

        return sum;
    }

    /// <summary>Deterministic FNV-1a hash so frame colors are stable within a render.</summary>
    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = (int)2166136261;
            foreach (var c in value)
            {
                hash = (hash ^ c) * 16777619;
            }

            return Math.Abs(hash);
        }
    }

    private static double Luminance(Color c) =>
        (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
}
