using System.Collections;
using System.Collections.ObjectModel;
using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Input;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls.Charts;

/// <summary>
/// A trace waterfall: each <see cref="TraceSpan"/> is drawn as one row with an indented
/// name and a duration bar positioned on a shared time axis (offset by
/// <see cref="TraceSpan.StartMs"/>, length proportional to <see cref="TraceSpan.DurationMs"/>).
/// Nested spans supplied via <see cref="TraceSpan.Children"/> are flattened and indented by
/// depth. Modeled on Grafana's traces panel. Data can be supplied inline via
/// <see cref="Spans"/> or bound through <see cref="SpansSource"/>.
///
/// The chart is interactive: it is focusable, the Up/Down arrows (and Home/End) move the
/// row selection, and a left click selects the clicked row. The selected row is highlighted
/// and exposed through <see cref="SelectedIndex"/> and <see cref="SelectedSpan"/>, both of
/// which are two-way bindable.
/// </summary>
[ContentProperty("Spans")]
public sealed class TraceChart : ChartBase
{
    // Left-eighth blocks give sub-cell precision on a bar's right edge: char = 0x2590 - eighths.

    /// <summary>Absolute Y of the first data row and the number of rows drawn, from the last render — used to map clicks to rows.</summary>
    private int _rowTop;
    private int _rowCount;

    /// <summary>Guards the SelectedIndex/SelectedSpan two-way sync against re-entrancy.</summary>
    private bool _syncing;

    public TraceChart()
    {
        DefaultStyleKey = typeof(TraceChart);
        Focusable = true;
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

    public static readonly DependencyProperty SelectedIndexProperty =
        DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(TraceChart),
            new FrameworkPropertyMetadata(-1, affectsRender: true,
                propertyChangedCallback: OnSelectedIndexChanged) { BindsTwoWayByDefault = true });

    public static readonly DependencyProperty SelectedSpanProperty =
        DependencyProperty.Register(nameof(SelectedSpan), typeof(object), typeof(TraceChart),
            new FrameworkPropertyMetadata(null, affectsRender: true,
                propertyChangedCallback: OnSelectedSpanChanged) { BindsTwoWayByDefault = true });

    public static readonly DependencyProperty SelectedBackgroundProperty =
        DependencyProperty.Register(nameof(SelectedBackground), typeof(Color), typeof(TraceChart),
            new FrameworkPropertyMetadata(new Color(38, 79, 120), affectsRender: true));

    public static readonly DependencyProperty SelectedForegroundProperty =
        DependencyProperty.Register(nameof(SelectedForeground), typeof(Color), typeof(TraceChart),
            new FrameworkPropertyMetadata(new Color(255, 255, 255), affectsRender: true));

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

    /// <summary>Index of the selected row in the flattened span list (-1 = none). Two-way bindable.</summary>
    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty)!;
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>The selected span, kept in sync with <see cref="SelectedIndex"/>. Two-way bindable.</summary>
    public object? SelectedSpan
    {
        get => GetValue(SelectedSpanProperty);
        set => SetValue(SelectedSpanProperty, value);
    }

    /// <summary>Background color of the selected row. Default is a muted blue.</summary>
    public Color SelectedBackground
    {
        get => (Color)GetValue(SelectedBackgroundProperty)!;
        set => SetValue(SelectedBackgroundProperty, value);
    }

    /// <summary>Text color of the selected row's label. Default is white.</summary>
    public Color SelectedForeground
    {
        get => (Color)GetValue(SelectedForegroundProperty)!;
        set => SetValue(SelectedForegroundProperty, value);
    }

    private List<TraceSpan> EffectiveSpans =>
        SpansSource != null ? [.. Enumerate<TraceSpan>(SpansSource)] : [.. _spans];

    /// <summary>Flattens the effective spans into display rows (span + depth).</summary>
    private List<(TraceSpan Span, int Depth)> BuildRows()
    {
        var rows = new List<(TraceSpan, int)>();
        Flatten(EffectiveSpans, 0, rows);
        return rows;
    }

    // ─── Selection sync ──────────────────────────────────────────────

    private static void OnSelectedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (TraceChart)d;
        if (chart._syncing)
        {
            return;
        }

        var rows = chart.BuildRows();
        var index = (int)e.NewValue!;
        chart._syncing = true;
        chart.SelectedSpan = index >= 0 && index < rows.Count ? rows[index].Span : null;
        chart._syncing = false;
    }

    private static void OnSelectedSpanChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (TraceChart)d;
        if (chart._syncing)
        {
            return;
        }

        var rows = chart.BuildRows();
        var index = -1;
        for (var i = 0; i < rows.Count; i++)
        {
            if (ReferenceEquals(rows[i].Span, e.NewValue))
            {
                index = i;
                break;
            }
        }

        chart._syncing = true;
        chart.SelectedIndex = index;
        chart._syncing = false;
    }

    // ─── Input ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public override void OnKeyEvent(KeyEvent e)
    {
        var count = Math.Min(BuildRows().Count, _rowCount > 0 ? _rowCount : int.MaxValue);
        if (count <= 0)
        {
            return;
        }

        var current = SelectedIndex;
        switch (e.Key)
        {
            case ConsoleKey.UpArrow:
                SelectedIndex = current < 0 ? count - 1 : Math.Max(0, current - 1);
                break;
            case ConsoleKey.DownArrow:
                SelectedIndex = current < 0 ? 0 : Math.Min(count - 1, current + 1);
                break;
            case ConsoleKey.Home:
                SelectedIndex = 0;
                break;
            case ConsoleKey.End:
                SelectedIndex = count - 1;
                break;
        }
    }

    /// <inheritdoc />
    public override void OnMouseEvent(MouseEvent e)
    {
        if (e is not { Action: MouseAction.Press, Button: MouseButton.Left })
        {
            return;
        }

        // Map the click's Y to a data row using the layout captured during the last render.
        var row = e.Y - _rowTop;
        if (row >= 0 && row < _rowCount)
        {
            SelectedIndex = row;
        }
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

        var rows = BuildRows();

        if (rows.Count == 0)
        {
            _rowCount = 0;
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

        // Capture layout for click hit-testing / key clamping.
        _rowTop = rowTop;
        _rowCount = Math.Min(rows.Count, Math.Max(0, bounds.Bottom - rowTop));

        // Selection highlight is brighter when the chart holds focus.
        var selectedBg = IsFocused ? SelectedBackground : Dim(SelectedBackground);

        var span2 = tMax - tMin;
        for (var r = 0; r < _rowCount; r++)
        {
            var (span, depth) = rows[r];
            var y = rowTop + r;
            var selected = r == SelectedIndex;

            var rowBg = selected ? selectedBg : Background;
            var labelFg = selected ? SelectedForeground : Foreground;

            // Fill the whole row behind the content when selected.
            if (selected)
            {
                buffer.FillRect(new Rect(bounds.X, y, bounds.Width, 1), new Cell(' ', labelFg, selectedBg));
            }

            // Label (indented by depth).
            var indent = new string(' ', Math.Min(depth * 2, Math.Max(0, gutter - 1)));
            DrawString(buffer, bounds.X, y, indent + span.Name, labelFg, rowBg, gutter);

            // Vertical gutter separator.
            if (ShowAxes && buffer.IsInBounds(bounds.X + gutter, y))
            {
                buffer.SetChar(bounds.X + gutter, y, '│', AxisColor, rowBg);
            }

            // Duration bar.
            var startNorm = (span.StartMs - tMin) / span2;
            var endNorm = (span.StartMs + Math.Max(0, span.DurationMs) - tMin) / span2;
            var color = ColorForSeries(r, span.Color);
            DrawBar(buffer, timeX, y, timeW, startNorm, endNorm, color, rowBg);
        }
    }

    private void DrawBar(CellBuffer buffer, int timeX, int y, int timeW, double startNorm, double endNorm, Color color, Color rowBg)
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
                buffer.SetChar(col, y, (char)(0x2590 - rem), color, rowBg);
            }
        }
    }

    private static Color Dim(Color c) => new((byte)(c.R * 0.55), (byte)(c.G * 0.55), (byte)(c.B * 0.55));

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
