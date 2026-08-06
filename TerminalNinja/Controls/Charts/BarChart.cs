using System.Collections;
using System.Collections.ObjectModel;
using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls.Charts;

/// <summary>
/// A categorical bar chart supporting one or more <see cref="ChartSeries"/> in either
/// grouped (side-by-side) or stacked layout, drawn vertically or horizontally. Bar
/// lengths use Unicode eighth-blocks for sub-cell precision. Data can be provided
/// inline via <see cref="Series"/> or bound through <see cref="SeriesSource"/>.
/// </summary>
[ContentProperty("Series")]
public sealed class BarChart : ChartBase
{
    // Vertical fill: U+2581 (▁, 1/8) .. U+2588 (█, 8/8). char = 0x2580 + eighths.
    // Horizontal fill: U+258F (▏, 1/8) .. U+2588 (█, 8/8). char = 0x2590 - eighths.

    public BarChart()
    {
        DefaultStyleKey = typeof(BarChart);
        _series.CollectionChanged += OnDataCollectionChanged;
    }

    private readonly ObservableCollection<ChartSeries> _series = [];

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty SeriesSourceProperty =
        DependencyProperty.Register(nameof(SeriesSource), typeof(IEnumerable), typeof(BarChart),
            new FrameworkPropertyMetadata(null, affectsRender: true,
                propertyChangedCallback: (d, e) => ((BarChart)d).RebindCollection(e.OldValue, e.NewValue)));

    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(BarChart),
            new FrameworkPropertyMetadata(Orientation.Vertical, affectsRender: true));

    public static readonly DependencyProperty BarModeProperty =
        DependencyProperty.Register(nameof(BarMode), typeof(BarMode), typeof(BarChart),
            new FrameworkPropertyMetadata(BarMode.Grouped, affectsRender: true));

    public static readonly DependencyProperty ShowValuesProperty =
        DependencyProperty.Register(nameof(ShowValues), typeof(bool), typeof(BarChart),
            new FrameworkPropertyMetadata(false, affectsRender: true));

    public static readonly DependencyProperty SelectedIndexProperty =
        DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(BarChart),
            new FrameworkPropertyMetadata(-1, affectsRender: true) { BindsTwoWayByDefault = true });

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(BarChart),
            new FrameworkPropertyMetadata(null, affectsRender: true) { BindsTwoWayByDefault = true });

    // ─── CLR Property Wrappers ───────────────────────────────────────

    /// <summary>The inline collection of series. Used when <see cref="SeriesSource"/> is null.</summary>
    public IList<ChartSeries> Series => _series;

    /// <summary>Optional bound series collection. Overrides <see cref="Series"/> when set.</summary>
    public IEnumerable? SeriesSource
    {
        get => (IEnumerable?)GetValue(SeriesSourceProperty);
        set => SetValue(SeriesSourceProperty, value);
    }

    /// <summary>Bar direction. Default is <see cref="Orientation.Vertical"/>.</summary>
    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty)!;
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>Whether multiple series are grouped side by side or stacked.</summary>
    public BarMode BarMode
    {
        get => (BarMode)GetValue(BarModeProperty)!;
        set => SetValue(BarModeProperty, value);
    }

    /// <summary>Whether to draw each bar's numeric value at its tip.</summary>
    public bool ShowValues
    {
        get => (bool)GetValue(ShowValuesProperty)!;
        set => SetValue(ShowValuesProperty, value);
    }

    /// <summary>Selected category index (-1 = none). Reserved for interaction; two-way bindable.</summary>
    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty)!;
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>Selected category value. Reserved for interaction; two-way bindable.</summary>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    private List<ChartSeries> EffectiveSeries =>
        SeriesSource != null ? [.. Enumerate<ChartSeries>(SeriesSource)] : [.. _series];

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

        var series = EffectiveSeries;
        var categoryCount = 0;
        foreach (var s in series)
        {
            categoryCount = Math.Max(categoryCount, s.Values.Count);
        }

        if (series.Count == 0 || categoryCount == 0)
        {
            DrawString(buffer, bounds.X + 1, bounds.Y, "(no data)", Foreground, Background, bounds.Width - 2);
            return;
        }

        // Reserve chrome rows/columns around the plot area.
        var top = bounds.Y;
        if (!string.IsNullOrEmpty(Title))
        {
            DrawString(buffer, bounds.X, top, Title, Foreground, Background, bounds.Width);
            top += 1;
        }

        var bottom = bounds.Bottom;
        if (ShowLegend && series.Count > 1)
        {
            DrawLegend(buffer, bounds.X, bottom - 1, bounds.Width, series);
            bottom -= 1;
        }

        // Compute the value axis extent (bars start at zero).
        var maxValue = ComputeMaxValue(series, categoryCount);
        var scale = AxisScale.Create(0, maxValue, maxTicks: 5);

        if (Orientation == Orientation.Vertical)
        {
            RenderVertical(buffer, new Rect(bounds.X, top, bounds.Width, bottom - top), series, categoryCount, scale);
        }
        else
        {
            RenderHorizontal(buffer, new Rect(bounds.X, top, bounds.Width, bottom - top), series, categoryCount, scale);
        }
    }

    private double ComputeMaxValue(List<ChartSeries> series, int categoryCount)
    {
        var max = 0.0;
        for (var c = 0; c < categoryCount; c++)
        {
            if (BarMode == BarMode.Stacked)
            {
                var sum = 0.0;
                foreach (var s in series)
                {
                    sum += ValueAt(s, c);
                }

                max = Math.Max(max, sum);
            }
            else
            {
                foreach (var s in series)
                {
                    max = Math.Max(max, ValueAt(s, c));
                }
            }
        }

        return max <= 0 ? 1 : max;
    }

    private static double ValueAt(ChartSeries s, int index) =>
        index < s.Values.Count ? s.Values[index].Value : 0.0;

    // ─── Vertical bars ───────────────────────────────────────────────

    private void RenderVertical(CellBuffer buffer, Rect area, List<ChartSeries> series, int categoryCount, AxisScale scale)
    {
        // Left gutter for y tick labels + axis line.
        var labelWidth = ShowAxes ? MaxTickLabelWidth(scale) : 0;
        var axisCol = area.X + labelWidth;                 // vertical axis line column
        var plotX = axisCol + (ShowAxes ? 1 : 0);
        var xLabelRow = area.Bottom - 1;                   // category labels
        var plotBottom = xLabelRow - (ShowAxes ? 1 : 0);   // one row above axis line for gap? keep axis on plotBottom
        var axisRow = plotBottom;                          // horizontal axis line row
        var plotTop = area.Y;
        var plotW = area.Right - plotX;
        var plotH = axisRow - plotTop;

        if (plotW <= 0 || plotH <= 0)
        {
            return;
        }

        // Grid + y tick labels.
        if (ShowGrid || ShowAxes)
        {
            foreach (var tick in scale.Ticks)
            {
                var norm = scale.Normalize(tick);
                var row = axisRow - (int)Math.Round(norm * plotH);
                if (row < plotTop || row > axisRow)
                {
                    continue;
                }

                if (ShowGrid && row < axisRow)
                {
                    for (var x = plotX; x < area.Right; x++)
                    {
                        if (buffer.IsInBounds(x, row))
                        {
                            buffer.SetChar(x, row, '·', GridColor, Background);
                        }
                    }
                }

                if (ShowAxes)
                {
                    var label = AxisScale.FormatTick(tick);
                    DrawString(buffer, area.X, row, label.PadLeft(labelWidth), Foreground, Background, labelWidth);
                }
            }
        }

        // Axis lines.
        if (ShowAxes)
        {
            for (var y = plotTop; y < axisRow; y++)
            {
                if (buffer.IsInBounds(axisCol, y))
                {
                    buffer.SetChar(axisCol, y, '│', AxisColor, Background);
                }
            }

            for (var x = axisCol; x < area.Right; x++)
            {
                if (buffer.IsInBounds(x, axisRow))
                {
                    buffer.SetChar(x, axisRow, x == axisCol ? '└' : '─', AxisColor, Background);
                }
            }
        }

        // Bars per category.
        var slotW = plotW / categoryCount;
        if (slotW < 1)
        {
            slotW = 1;
        }

        for (var c = 0; c < categoryCount; c++)
        {
            var slotX = plotX + c * slotW;
            var barsW = Math.Max(1, slotW - 1); // leave a 1-col gap between categories

            if (BarMode == BarMode.Stacked)
            {
                RenderStackedColumn(buffer, slotX, barsW, plotTop, axisRow, series, c, scale);
            }
            else
            {
                var perBar = Math.Max(1, barsW / series.Count);
                for (var s = 0; s < series.Count; s++)
                {
                    var barX = slotX + s * perBar;
                    if (barX >= area.Right)
                    {
                        break;
                    }

                    var value = ValueAt(series[s], c);
                    var eighths = (int)Math.Round(scale.Normalize(value) * plotH * 8);
                    var color = ColorForSeries(s, series[s].Color);
                    FillColumn(buffer, barX, Math.Min(perBar, area.Right - barX), plotTop, axisRow, eighths, color);
                }
            }

            // Category label centered under the slot.
            if (ShowAxes && c < FirstSeriesLabels(series).Count)
            {
                var label = FirstSeriesLabels(series)[c];
                if (!string.IsNullOrEmpty(label))
                {
                    var lx = slotX + Math.Max(0, (barsW - label.Length) / 2);
                    DrawString(buffer, lx, xLabelRow, label, Foreground, Background, slotW);
                }
            }
        }
    }

    private void FillColumn(CellBuffer buffer, int x, int width, int plotTop, int axisRow, int eighths, Color color)
    {
        eighths = Math.Max(0, eighths);
        var fullCells = eighths / 8;
        var rem = eighths % 8;

        for (var col = x; col < x + width; col++)
        {
            for (var cellsFromBottom = 0; cellsFromBottom < axisRow - plotTop; cellsFromBottom++)
            {
                var row = axisRow - 1 - cellsFromBottom;
                if (!buffer.IsInBounds(col, row))
                {
                    continue;
                }

                if (cellsFromBottom < fullCells)
                {
                    buffer.SetChar(col, row, '█', color, color);
                }
                else if (cellsFromBottom == fullCells && rem > 0)
                {
                    buffer.SetChar(col, row, (char)(0x2580 + rem), color, Background);
                    break;
                }
                else
                {
                    break;
                }
            }
        }
    }

    private void RenderStackedColumn(CellBuffer buffer, int x, int width, int plotTop, int axisRow, List<ChartSeries> series, int category, AxisScale scale)
    {
        var plotH = axisRow - plotTop;

        // Cumulative eighths for each segment boundary.
        var cum = new int[series.Count + 1];
        for (var s = 0; s < series.Count; s++)
        {
            var value = ValueAt(series[s], category);
            var eighths = (int)Math.Round(scale.Normalize(value) * plotH * 8);
            cum[s + 1] = cum[s] + Math.Max(0, eighths);
        }

        var total = cum[^1];

        for (var col = x; col < x + width; col++)
        {
            for (var cellsFromBottom = 0; cellsFromBottom < plotH; cellsFromBottom++)
            {
                var row = axisRow - 1 - cellsFromBottom;
                if (!buffer.IsInBounds(col, row))
                {
                    continue;
                }

                var cellBottomEighth = cellsFromBottom * 8;
                if (cellBottomEighth >= total)
                {
                    break;
                }

                // Which series covers the middle of this cell?
                var mid = cellBottomEighth + 4;
                var segIndex = SegmentAt(cum, Math.Min(mid, total - 1));
                var color = ColorForSeries(segIndex, series[segIndex].Color);

                if (cellBottomEighth + 8 <= total)
                {
                    buffer.SetChar(col, row, '█', color, color);
                }
                else
                {
                    var rem = total - cellBottomEighth;
                    buffer.SetChar(col, row, (char)(0x2580 + rem), color, Background);
                    break;
                }
            }
        }
    }

    private static int SegmentAt(int[] cumulative, int eighth)
    {
        for (var s = 0; s < cumulative.Length - 1; s++)
        {
            if (eighth < cumulative[s + 1])
            {
                return s;
            }
        }

        return cumulative.Length - 2;
    }

    // ─── Horizontal bars ─────────────────────────────────────────────

    private void RenderHorizontal(CellBuffer buffer, Rect area, List<ChartSeries> series, int categoryCount, AxisScale scale)
    {
        // Left gutter for category labels; bottom row omitted for brevity (no value axis labels).
        var labels = FirstSeriesLabels(series);
        var labelWidth = ShowAxes ? Math.Min(12, MaxLabelWidth(labels)) : 0;
        var plotX = area.X + labelWidth + (ShowAxes ? 1 : 0);
        var axisCol = area.X + labelWidth;
        var plotW = area.Right - plotX;
        var plotTop = area.Y;
        var plotBottom = area.Bottom;
        var plotH = plotBottom - plotTop;

        if (plotW <= 0 || plotH <= 0)
        {
            return;
        }

        if (ShowAxes)
        {
            for (var y = plotTop; y < plotBottom; y++)
            {
                if (buffer.IsInBounds(axisCol, y))
                {
                    buffer.SetChar(axisCol, y, '│', AxisColor, Background);
                }
            }
        }

        var slotH = plotH / categoryCount;
        if (slotH < 1)
        {
            slotH = 1;
        }

        for (var c = 0; c < categoryCount; c++)
        {
            var slotY = plotTop + c * slotH;
            var barsH = Math.Max(1, slotH); // rows available for this category's bars

            if (ShowAxes && c < labels.Count && !string.IsNullOrEmpty(labels[c]))
            {
                DrawString(buffer, area.X, slotY, labels[c], Foreground, Background, labelWidth);
            }

            if (BarMode == BarMode.Stacked)
            {
                RenderStackedRow(buffer, plotX, plotW, slotY, Math.Min(barsH, plotBottom - slotY), series, c, scale);
            }
            else
            {
                var perBar = Math.Max(1, barsH / series.Count);
                for (var s = 0; s < series.Count; s++)
                {
                    var barY = slotY + s * perBar;
                    if (barY >= plotBottom)
                    {
                        break;
                    }

                    var value = ValueAt(series[s], c);
                    var eighths = (int)Math.Round(scale.Normalize(value) * plotW * 8);
                    var color = ColorForSeries(s, series[s].Color);
                    FillRow(buffer, plotX, barY, Math.Min(perBar, plotBottom - barY), plotW, eighths, color);
                }
            }
        }
    }

    private void FillRow(CellBuffer buffer, int x, int y, int height, int plotW, int eighths, Color color)
    {
        eighths = Math.Max(0, eighths);
        var fullCells = eighths / 8;
        var rem = eighths % 8;

        for (var row = y; row < y + height; row++)
        {
            for (var i = 0; i < plotW; i++)
            {
                var col = x + i;
                if (!buffer.IsInBounds(col, row))
                {
                    continue;
                }

                if (i < fullCells)
                {
                    buffer.SetChar(col, row, '█', color, color);
                }
                else if (i == fullCells && rem > 0)
                {
                    buffer.SetChar(col, row, (char)(0x2590 - rem), color, Background);
                    break;
                }
                else
                {
                    break;
                }
            }
        }
    }

    private void RenderStackedRow(CellBuffer buffer, int x, int plotW, int y, int height, List<ChartSeries> series, int category, AxisScale scale)
    {
        var cum = new int[series.Count + 1];
        for (var s = 0; s < series.Count; s++)
        {
            var value = ValueAt(series[s], category);
            var eighths = (int)Math.Round(scale.Normalize(value) * plotW * 8);
            cum[s + 1] = cum[s] + Math.Max(0, eighths);
        }

        var total = cum[^1];

        for (var row = y; row < y + height; row++)
        {
            for (var i = 0; i < plotW; i++)
            {
                var col = x + i;
                if (!buffer.IsInBounds(col, row))
                {
                    continue;
                }

                var cellStart = i * 8;
                if (cellStart >= total)
                {
                    break;
                }

                var mid = Math.Min(cellStart + 4, total - 1);
                var segIndex = SegmentAt(cum, mid);
                var color = ColorForSeries(segIndex, series[segIndex].Color);

                if (cellStart + 8 <= total)
                {
                    buffer.SetChar(col, row, '█', color, color);
                }
                else
                {
                    var rem = total - cellStart;
                    buffer.SetChar(col, row, (char)(0x2590 - rem), color, Background);
                    break;
                }
            }
        }
    }

    // ─── Chrome helpers ──────────────────────────────────────────────

    private void DrawLegend(CellBuffer buffer, int x, int y, int width, List<ChartSeries> series)
    {
        var cx = x;
        for (var s = 0; s < series.Count && cx < x + width; s++)
        {
            var color = ColorForSeries(s, series[s].Color);
            if (buffer.IsInBounds(cx, y))
            {
                buffer.SetChar(cx, y, '█', color, Background);
            }

            cx += 1;
            var name = string.IsNullOrEmpty(series[s].Name) ? $"Series {s + 1}" : series[s].Name;
            cx += DrawString(buffer, cx + 1, y, name, LegendColor, Background, x + width - cx - 1) + 2;
        }
    }

    private static List<string> FirstSeriesLabels(List<ChartSeries> series)
    {
        var labels = new List<string>();
        if (series.Count > 0)
        {
            foreach (var p in series[0].Values)
            {
                labels.Add(p.Label);
            }
        }

        return labels;
    }

    private static int MaxTickLabelWidth(AxisScale scale)
    {
        var w = 1;
        foreach (var t in scale.Ticks)
        {
            w = Math.Max(w, AxisScale.FormatTick(t).Length);
        }

        return w;
    }

    private static int MaxLabelWidth(List<string> labels)
    {
        var w = 1;
        foreach (var l in labels)
        {
            w = Math.Max(w, l.Length);
        }

        return w;
    }
}
