using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls.Charts;

/// <summary>
/// A flame graph (icicle layout, root at top): each <see cref="FlameNode"/> is drawn as
/// a horizontal bar whose width is proportional to its value relative to the root, with
/// children laid out proportionally in the row beneath their parent. Widths use eighth-
/// blocks for sub-cell precision. Set the tree via <see cref="Root"/> (the content
/// property), which may also be bound.
/// </summary>
[ContentProperty("Root")]
public sealed class FlameGraph : ChartBase
{
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

    /// <summary>Selected frame. Reserved for interaction; two-way bindable.</summary>
    public object? SelectedNode
    {
        get => GetValue(SelectedNodeProperty);
        set => SetValue(SelectedNodeProperty, value);
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

        RenderNode(buffer, root, depth: 0, xStart: bounds.X, widthCells: bounds.Width, top: top, maxRows: rowCount);
    }

    private void RenderNode(CellBuffer buffer, FlameNode node, int depth, double xStart, double widthCells, int top, int maxRows)
    {
        if (depth >= maxRows || widthCells < 1.0 / 8)
        {
            return;
        }

        var row = top + depth;
        var color = ColorForNode(node, depth);
        DrawFrame(buffer, (int)Math.Round(xStart), xStart, widthCells, row, node.Name, color);

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
            RenderNode(buffer, child, depth + 1, childX, childWidth, top, maxRows);
            childX += childWidth;
        }
    }

    private void DrawFrame(CellBuffer buffer, int xCell, double xStart, double widthCells, int row, string name, Color color)
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
            var textColor = Luminance(color) > 0.55 ? new Color(0, 0, 0) : new Color(255, 255, 255);
            DrawLabelOverBar(buffer, startCol + 1, row, name, textColor, color, fullCells - 1);
        }
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
