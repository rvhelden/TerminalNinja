using System.Collections;
using System.Collections.ObjectModel;
using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls.Charts;

/// <summary>
/// A trace waterfall: each <see cref="TraceSpan"/> is drawn as one row with an indented
/// name and a duration bar positioned on a shared time axis (offset by
/// <see cref="TraceSpan.StartMs"/>, length proportional to <see cref="TraceSpan.DurationMs"/>).
/// Nested spans supplied via <see cref="TraceSpan.Children"/> are flattened and indented by
/// depth. Modeled on Grafana's traces panel. Data can be supplied inline via
/// <see cref="Spans"/> or bound through <see cref="SpansSource"/>.
/// </summary>
[ContentProperty("Spans")]
public sealed class TraceChart : ChartBase
{
    // Left-eighth blocks give sub-cell precision on a bar's right edge: char = 0x2590 - eighths.

    public TraceChart()
    {
        DefaultStyleKey = typeof(TraceChart);
        _spans.CollectionChanged += OnDataCollectionChanged;
    }

    private readonly ObservableCollection<TraceSpan> _spans = [];

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty SpansSourceProperty =
        DependencyProperty.Register(nameof(SpansSource), typeof(IEnumerable), typeof(TraceChart),
            new FrameworkPropertyMetadata(null, affectsRender: true,
                propertyChangedCallback: (d, e) => ((TraceChart)d).RebindCollection(e.OldValue, e.NewValue)));

    public static readonly DependencyProperty LabelWidthProperty =
        DependencyProperty.Register(nameof(LabelWidth), typeof(int), typeof(TraceChart),
            new FrameworkPropertyMetadata(24, affectsRender: true));

    public static readonly DependencyProperty SelectedSpanProperty =
        DependencyProperty.Register(nameof(SelectedSpan), typeof(object), typeof(TraceChart),
            new FrameworkPropertyMetadata(null, affectsRender: true) { BindsTwoWayByDefault = true });

    // ─── CLR Property Wrappers ───────────────────────────────────────

    /// <summary>The inline collection of spans. Used when <see cref="SpansSource"/> is null.</summary>
    public IList<TraceSpan> Spans => _spans;

    /// <summary>Optional bound spans collection. Overrides <see cref="Spans"/> when set.</summary>
    public IEnumerable? SpansSource
    {
        get => (IEnumerable?)GetValue(SpansSourceProperty);
        set => SetValue(SpansSourceProperty, value);
    }

    /// <summary>Width (in cells) of the left gutter holding span names. Default 24.</summary>
    public int LabelWidth
    {
        get => (int)GetValue(LabelWidthProperty)!;
        set => SetValue(LabelWidthProperty, value);
    }

    /// <summary>Selected span. Reserved for interaction; two-way bindable.</summary>
    public object? SelectedSpan
    {
        get => GetValue(SelectedSpanProperty);
        set => SetValue(SelectedSpanProperty, value);
    }

    private List<TraceSpan> EffectiveSpans =>
        SpansSource != null ? [.. Enumerate<TraceSpan>(SpansSource)] : [.. _spans];

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

        var rows = new List<(TraceSpan Span, int Depth)>();
        Flatten(EffectiveSpans, 0, rows);

        if (rows.Count == 0)
        {
            DrawString(buffer, bounds.X + 1, bounds.Y, "(no spans)", Foreground, Background, bounds.Width - 2);
            return;
        }

        var top = bounds.Y;
        if (!string.IsNullOrEmpty(Title))
        {
            DrawString(buffer, bounds.X, top, Title, Foreground, Background, bounds.Width);
            top += 1;
        }

        // Time range across all spans.
        var tMin = double.PositiveInfinity;
        var tMax = double.NegativeInfinity;
        foreach (var (span, _) in rows)
        {
            tMin = Math.Min(tMin, span.StartMs);
            tMax = Math.Max(tMax, span.StartMs + Math.Max(0, span.DurationMs));
        }

        if (!double.IsFinite(tMin))
        {
            tMin = 0;
        }

        tMin = Math.Min(0, tMin);
        if (!double.IsFinite(tMax) || tMax <= tMin)
        {
            tMax = tMin + 1;
        }

        var gutter = Math.Clamp(LabelWidth, 4, Math.Max(4, bounds.Width - 4));
        var timeX = bounds.X + gutter + 1;
        var timeW = bounds.Right - timeX;
        if (timeW <= 0)
        {
            return;
        }

        // Time axis header.
        var rowTop = top;
        if (ShowAxes)
        {
            DrawString(buffer, bounds.X, rowTop, "span", Foreground, Background, gutter);
            DrawString(buffer, timeX, rowTop, "0", AxisColor, Background, timeW);
            var maxLabel = AxisScale.FormatTick(tMax) + "ms";
            DrawString(buffer, bounds.Right - maxLabel.Length, rowTop, maxLabel, AxisColor, Background, maxLabel.Length);
            rowTop += 1;
        }

        var span2 = tMax - tMin;
        var maxRows = bounds.Bottom - rowTop;
        for (var r = 0; r < rows.Count && r < maxRows; r++)
        {
            var (span, depth) = rows[r];
            var y = rowTop + r;

            // Label (indented by depth).
            var indent = new string(' ', Math.Min(depth * 2, Math.Max(0, gutter - 1)));
            DrawString(buffer, bounds.X, y, indent + span.Name, Foreground, Background, gutter);

            // Vertical gutter separator.
            if (ShowAxes && buffer.IsInBounds(bounds.X + gutter, y))
            {
                buffer.SetChar(bounds.X + gutter, y, '│', AxisColor, Background);
            }

            // Duration bar.
            var startNorm = (span.StartMs - tMin) / span2;
            var endNorm = (span.StartMs + Math.Max(0, span.DurationMs) - tMin) / span2;
            var color = ColorForSeries(r, span.Color);
            DrawBar(buffer, timeX, y, timeW, startNorm, endNorm, color);
        }
    }

    private void DrawBar(CellBuffer buffer, int timeX, int y, int timeW, double startNorm, double endNorm, Color color)
    {
        startNorm = Math.Clamp(startNorm, 0, 1);
        endNorm = Math.Clamp(endNorm, 0, 1);

        var startCol = (int)Math.Floor(startNorm * timeW);
        var endEighths = (int)Math.Round(endNorm * timeW * 8);
        var startEighths = startCol * 8;
        var lengthEighths = Math.Max(1, endEighths - startEighths); // always show at least a sliver
        var fullCells = lengthEighths / 8;
        var rem = lengthEighths % 8;

        for (var i = 0; i < fullCells; i++)
        {
            var col = timeX + startCol + i;
            if (buffer.IsInBounds(col, y))
            {
                buffer.SetChar(col, y, '█', color, color);
            }
        }

        if (rem > 0)
        {
            var col = timeX + startCol + fullCells;
            if (buffer.IsInBounds(col, y))
            {
                buffer.SetChar(col, y, (char)(0x2590 - rem), color, Background);
            }
        }
    }

    /// <summary>
    /// Flattens the span tree into rows. When a span has children, their depth is derived
    /// from nesting; otherwise the span's own <see cref="TraceSpan.Depth"/> is honored.
    /// </summary>
    private static void Flatten(List<TraceSpan> spans, int depth, List<(TraceSpan, int)> rows)
    {
        foreach (var span in spans)
        {
            var effectiveDepth = span.Children.Count > 0 || depth > 0 ? depth : span.Depth;
            rows.Add((span, effectiveDepth));
            if (span.Children.Count > 0)
            {
                Flatten(span.Children, depth + 1, rows);
            }
        }
    }
}
