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
/// The chart is interactive: it is focusable, the Up/Down arrows (plus PageUp/PageDown and
/// Home/End) move the row selection, and a left click selects the clicked row. The selected row
/// is highlighted and exposed through <see cref="SelectedIndex"/> and <see cref="SelectedSpan"/>,
/// both of which are two-way bindable.
///
/// A trace taller than the control scrolls to follow the selection, and the gutter header
/// reports the visible range, so every span stays reachable however long the trace is.
/// </summary>
[ContentProperty("Spans")]
public sealed class TraceChart : ChartBase
{
    // Left-eighth blocks give sub-cell precision on a bar's right edge: char = 0x2590 - eighths.

    /// <summary>Absolute Y of the first data row, the number of rows drawn, and the per-span vertical stride, from the last render — used to map clicks to rows.</summary>
    private int _rowTop;
    private int _rowCount;
    private int _rowStride = 1;

    /// <summary>Index of the topmost drawn span; the chart scrolls to keep the selection in view.</summary>
    /// <remarks>
    /// Without this the chart drew only the spans that fit and dropped the rest with no sign that
    /// it had: an 85-span transaction showed its first thirty, and because the key handler clamped
    /// to the drawn count, End could not reach the others either. They were not merely off screen,
    /// they were unreachable.
    /// </remarks>
    private int _firstVisible;

    /// <summary>Guards the SelectedIndex/SelectedSpan two-way sync against re-entrancy.</summary>
    private bool _syncing;

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

    public static readonly DependencyProperty RowSpacingProperty =
        DependencyProperty.Register(nameof(RowSpacing), typeof(int), typeof(TraceChart),
            new FrameworkPropertyMetadata(1, affectsRender: true));

    public static readonly DependencyProperty SelectedIndexProperty =
        DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(TraceChart),
            new FrameworkPropertyMetadata(-1, affectsRender: true,
                propertyChangedCallback: OnSelectedIndexChanged) { BindsTwoWayByDefault = true });

    public static readonly DependencyProperty SelectedSpanProperty =
        DependencyProperty.Register(nameof(SelectedSpan), typeof(object), typeof(TraceChart),
            new FrameworkPropertyMetadata(null, affectsRender: true,
                propertyChangedCallback: OnSelectedSpanChanged) { BindsTwoWayByDefault = true });

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

    /// <summary>Number of blank rows inserted between consecutive spans. Default 1.</summary>
    public int RowSpacing
    {
        get => (int)GetValue(RowSpacingProperty)!;
        set => SetValue(RowSpacingProperty, value);
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
        // SetValueInternal, not the public setter: the setter goes through SetValue, which clears
        // any binding on SelectedSpan, so a two-way {Binding SelectedSpan} would be destroyed the
        // first time the user moved the selection. This keeps the expression and still raises the
        // change so the binding writes back.
        chart.SetValueInternal(SelectedSpanProperty, index >= 0 && index < rows.Count ? rows[index].Span : null);
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
        chart.SetValueInternal(SelectedIndexProperty, index);
        chart._syncing = false;
    }

    // ─── Input ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public override bool OnKeyEvent(KeyEvent e)
    {
        // Every span, not just the drawn ones. Clamping to what fit on screen is what made the
        // spans past the first page unreachable.
        var count = BuildRows().Count;
        if (count <= 0)
        {
            return false;
        }

        var page = Math.Max(1, _rowCount);
        var current = SelectedIndex;
        switch (e.Key)
        {
            case ConsoleKey.UpArrow:
                SelectedIndex = current < 0 ? count - 1 : Math.Max(0, current - 1);
                return true;
            case ConsoleKey.DownArrow:
                SelectedIndex = current < 0 ? 0 : Math.Min(count - 1, current + 1);
                return true;
            case ConsoleKey.PageUp:
                SelectedIndex = Math.Max(0, (current < 0 ? 0 : current) - page);
                return true;
            case ConsoleKey.PageDown:
                SelectedIndex = Math.Min(count - 1, (current < 0 ? 0 : current) + page);
                return true;
            case ConsoleKey.Home:
                SelectedIndex = 0;
                return true;
            case ConsoleKey.End:
                SelectedIndex = count - 1;
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

        // Map the click's Y to a data row using the layout captured during the last render.
        // Clicking a gap row selects the span above it.
        var rel = e.Y - _rowTop;
        if (rel < 0)
        {
            return;
        }

        var row = rel / _rowStride;
        if (row < _rowCount)
        {
            // Offset by the scroll position: row 0 on screen is not span 0 once scrolled.
            SelectedIndex = _firstVisible + row;
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
        var headerY = ShowAxes ? rowTop : -1;
        if (ShowAxes)
        {
            DrawString(buffer, bounds.X, rowTop, "span", Foreground, Background, gutter);
            DrawString(buffer, timeX, rowTop, "0", AxisColor, Background, timeW);
            var maxLabel = AxisScale.FormatDurationMs(tMax);
            DrawString(buffer, bounds.Right - maxLabel.Length, rowTop, maxLabel, AxisColor, Background, maxLabel.Length);
            rowTop += 1;
        }

        // Capture layout for click hit-testing / key clamping. Each span occupies one row plus
        // RowSpacing blank rows beneath it.
        var spacing = Math.Max(0, RowSpacing);
        var stride = 1 + spacing;
        var avail = bounds.Bottom - rowTop;
        var maxFit = avail <= 0 ? 0 : (avail + spacing) / stride;
        _rowTop = rowTop;
        _rowStride = stride;
        _rowCount = Math.Min(rows.Count, maxFit);

        // Scroll so the selection is on screen, then clamp so the last page is not half empty.
        var selected1 = SelectedIndex;
        if (maxFit > 0 && selected1 >= 0)
        {
            if (selected1 < _firstVisible)
            {
                _firstVisible = selected1;
            }
            else if (selected1 >= _firstVisible + maxFit)
            {
                _firstVisible = selected1 - maxFit + 1;
            }
        }

        _firstVisible = Math.Clamp(_firstVisible, 0, Math.Max(0, rows.Count - maxFit));

        // Say so when the chart is a window onto something longer. Without it a scrolled trace
        // looks exactly like a complete one that happens to start in the middle.
        if (headerY >= 0 && _rowCount < rows.Count)
        {
            var position = $"span {_firstVisible + 1}-{_firstVisible + _rowCount}/{rows.Count}";
            DrawString(buffer, bounds.X, headerY, position, Foreground, Background, gutter);
        }

        // Selection highlight is brighter when the chart holds focus.
        var selectedBg = EffectiveSelectionBackground;

        // Continuous gutter separator so it stays unbroken across the spacing gaps.
        if (ShowAxes && _rowCount > 0)
        {
            var lastY = rowTop + (_rowCount - 1) * stride;
            for (var y = rowTop; y <= lastY; y++)
            {
                if (buffer.IsInBounds(bounds.X + gutter, y))
                {
                    buffer.SetChar(bounds.X + gutter, y, '│', AxisColor, Background);
                }
            }
        }

        var span2 = tMax - tMin;
        for (var r = 0; r < _rowCount; r++)
        {
            // The absolute index is what selection, colour and hit-testing are all keyed on;
            // using the row offset would recolour every span as the chart scrolled.
            var index = _firstVisible + r;
            var (span, depth) = rows[index];
            var y = rowTop + r * stride;
            var selected = index == SelectedIndex;

            var spanColor = ColorForSeries(index, span.Color);
            var rowBg = selected ? selectedBg : Background;
            // The label takes the span's own colour so it matches its bar; a selected row uses the
            // selection foreground for contrast against the highlight.
            var labelFg = selected ? SelectedForeground : spanColor;

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
            DrawBar(buffer, timeX, y, timeW, startNorm, endNorm, spanColor, rowBg);
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
