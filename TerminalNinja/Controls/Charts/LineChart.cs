using System.Collections;
using System.Collections.ObjectModel;
using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Input;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls.Charts;

/// <summary>
/// A line chart that plots one or more <see cref="ChartSeries"/> as continuous lines
/// using a high-resolution braille canvas (2×4 dots per cell). Points in each series
/// are spread evenly along the x axis by index; the y axis is auto-scaled unless
/// <see cref="YMin"/>/<see cref="YMax"/> are set. Data can be supplied inline via
/// <see cref="Series"/> or bound through <see cref="SeriesSource"/>.
/// </summary>
[ContentProperty("Series")]
public sealed class LineChart : ChartBase
{
    public LineChart()
    {
        DefaultStyleKey = typeof(LineChart);
        _series.CollectionChanged += OnDataCollectionChanged;
    }

    private readonly ObservableCollection<ChartSeries> _series = [];

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty SeriesSourceProperty =
        DependencyProperty.Register(nameof(SeriesSource), typeof(IEnumerable), typeof(LineChart),
            new FrameworkPropertyMetadata(null, affectsRender: true,
                propertyChangedCallback: (d, e) => ((LineChart)d).RebindCollection(e.OldValue, e.NewValue)));

    public static readonly DependencyProperty ShowMarkersProperty =
        DependencyProperty.Register(nameof(ShowMarkers), typeof(bool), typeof(LineChart),
            new FrameworkPropertyMetadata(false, affectsRender: true));

    public static readonly DependencyProperty YMinProperty =
        DependencyProperty.Register(nameof(YMin), typeof(double), typeof(LineChart),
            new FrameworkPropertyMetadata(double.NaN, affectsRender: true));

    public static readonly DependencyProperty YMaxProperty =
        DependencyProperty.Register(nameof(YMax), typeof(double), typeof(LineChart),
            new FrameworkPropertyMetadata(double.NaN, affectsRender: true));

    public static readonly DependencyProperty ShowXLabelsProperty =
        DependencyProperty.Register(nameof(ShowXLabels), typeof(bool), typeof(LineChart),
            new FrameworkPropertyMetadata(true, affectsRender: true));

    public static readonly DependencyProperty SelectedIndexProperty =
        DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(LineChart),
            new FrameworkPropertyMetadata(-1, affectsRender: true) { BindsTwoWayByDefault = true });

    // ─── CLR Property Wrappers ───────────────────────────────────────

    /// <summary>The inline collection of series. Used when <see cref="SeriesSource"/> is null.</summary>
    public IList<ChartSeries> Series => _series;

    /// <summary>Optional bound series collection. Overrides <see cref="Series"/> when set.</summary>
    public IEnumerable? SeriesSource
    {
        get => (IEnumerable?)GetValue(SeriesSourceProperty);
        set => SetValue(SeriesSourceProperty, value);
    }

    /// <summary>Whether to draw a marker at each data point.</summary>
    public bool ShowMarkers
    {
        get => (bool)GetValue(ShowMarkersProperty)!;
        set => SetValue(ShowMarkersProperty, value);
    }

    /// <summary>Explicit y-axis minimum. NaN (default) auto-scales from the data.</summary>
    public double YMin
    {
        get => (double)GetValue(YMinProperty)!;
        set => SetValue(YMinProperty, value);
    }

    /// <summary>Explicit y-axis maximum. NaN (default) auto-scales from the data.</summary>
    public double YMax
    {
        get => (double)GetValue(YMaxProperty)!;
        set => SetValue(YMaxProperty, value);
    }

    /// <summary>
    /// Whether to draw x-axis labels along the bottom, taken from each
    /// <see cref="ChartDataPoint.Label"/> (e.g. dates). A label row is only reserved when at
    /// least one point carries a label; labels are thinned to fit without overlapping.
    /// Default is true.
    /// </summary>
    public bool ShowXLabels
    {
        get => (bool)GetValue(ShowXLabelsProperty)!;
        set => SetValue(ShowXLabelsProperty, value);
    }

    /// <summary>
    /// Index of the selected data point along the x axis (-1 = none). A vertical crosshair
    /// marks the selection. Two-way bindable; move it with Left/Right or click.
    /// </summary>
    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty)!;
        set => SetValue(SelectedIndexProperty, value);
    }

    private List<ChartSeries> EffectiveSeries =>
        SeriesSource != null ? [.. Enumerate<ChartSeries>(SeriesSource)] : [.. _series];

    private int PointCount()
    {
        var count = 0;
        foreach (var s in EffectiveSeries)
        {
            count = Math.Max(count, s.Values.Count);
        }

        return count;
    }

    // Plot geometry captured from the last render, for click hit-testing / crosshair.
    private int _plotX;
    private int _plotW;
    private int _pointCount;

    // ─── Input ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public override void OnKeyEvent(KeyEvent e)
    {
        var count = PointCount();
        if (count <= 0)
        {
            return;
        }

        var current = SelectedIndex;
        switch (e.Key)
        {
            case ConsoleKey.LeftArrow:
                SelectedIndex = current < 0 ? count - 1 : Math.Max(0, current - 1);
                break;
            case ConsoleKey.RightArrow:
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
        if (e is not { Action: MouseAction.Press, Button: MouseButton.Left } || _plotW <= 1 || _pointCount <= 0)
        {
            return;
        }

        // Snap the click to the nearest point along the x axis.
        var fx = (double)(e.X - _plotX) / (_plotW - 1);
        var index = (int)Math.Round(Math.Clamp(fx, 0, 1) * (_pointCount - 1));
        if (index >= 0 && index < _pointCount)
        {
            SelectedIndex = index;
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

        var series = EffectiveSeries;
        var maxPoints = 0;
        foreach (var s in series)
        {
            maxPoints = Math.Max(maxPoints, s.Values.Count);
        }

        if (series.Count == 0 || maxPoints == 0)
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

        var bottom = bounds.Bottom;
        if (ShowLegend && series.Count > 1)
        {
            DrawLegend(buffer, bounds.X, bottom - 1, bounds.Width, series);
            bottom -= 1;
        }

        // Determine y scale.
        var (dataMin, dataMax) = DataRange(series);
        var min = double.IsFinite(YMin) ? YMin : dataMin;
        var max = double.IsFinite(YMax) ? YMax : dataMax;
        var scale = AxisScale.Create(min, max, maxTicks: 5);

        // Reserve a bottom row for x-axis labels only when the points carry labels.
        var xLabels = XAxisLabels(series);
        var showX = ShowXLabels && xLabels.Exists(l => !string.IsNullOrEmpty(l));

        var labelWidth = ShowAxes ? MaxTickLabelWidth(scale) : 0;
        var axisCol = bounds.X + labelWidth;
        var plotX = axisCol + (ShowAxes ? 1 : 0);
        var xLabelRow = showX ? bottom - 1 : -1;
        var axisRow = showX ? bottom - 2 : bottom - 1;
        var plotTop = top;
        var plotW = bounds.Right - plotX;
        var plotH = axisRow - plotTop;

        if (plotW <= 0 || plotH <= 0)
        {
            return;
        }

        DrawGridAndAxes(buffer, scale, axisCol, plotX, plotTop, axisRow, bounds.Right, bounds.X, labelWidth);

        // Capture geometry for click hit-testing / crosshair, then draw the crosshair band
        // behind the lines so the braille (blitted with a transparent background) shows through.
        _plotX = plotX;
        _plotW = plotW;
        _pointCount = maxPoints;

        var crosshairX = -1;
        if (SelectedIndex >= 0 && SelectedIndex < maxPoints)
        {
            var fx = maxPoints > 1 ? (double)SelectedIndex / (maxPoints - 1) : 0.0;
            crosshairX = plotX + (int)Math.Round(fx * (plotW - 1));
            var selBg = EffectiveSelectionBackground;
            for (var y = plotTop; y < axisRow; y++)
            {
                if (buffer.IsInBounds(crosshairX, y))
                {
                    buffer.SetChar(crosshairX, y, ' ', SelectedForeground, selBg);
                }
            }
        }

        // Draw each series into its own braille canvas so lines get 2×4 resolution.
        var pxW = plotW * 2;
        var pxH = plotH * 4;
        for (var s = 0; s < series.Count; s++)
        {
            var points = series[s].Values;
            if (points.Count == 0)
            {
                continue;
            }

            var canvas = new BrailleCanvas(plotW, plotH);
            var prevX = 0;
            var prevY = 0;
            var have = false;

            for (var i = 0; i < points.Count; i++)
            {
                var fx = points.Count > 1 ? (double)i / (points.Count - 1) : 0.0;
                var px = (int)Math.Round(fx * (pxW - 1));
                var norm = scale.Normalize(points[i].Value);
                var py = (int)Math.Round((1.0 - norm) * (pxH - 1));

                if (have)
                {
                    canvas.Line(prevX, prevY, px, py);
                }

                if (ShowMarkers)
                {
                    canvas.Plot(px, py);
                    canvas.Plot(Math.Min(px + 1, pxW - 1), py);
                    canvas.Plot(px, Math.Min(py + 1, pxH - 1));
                }

                prevX = px;
                prevY = py;
                have = true;
            }

            canvas.Blit(buffer, plotX, plotTop, ColorForSeries(s, series[s].Color));
        }

        if (showX)
        {
            DrawXLabels(buffer, xLabels, plotX, plotW, xLabelRow, axisRow);

            // Emphasize the selected point's x label.
            if (crosshairX >= 0 && SelectedIndex < xLabels.Count && !string.IsNullOrEmpty(xLabels[SelectedIndex]))
            {
                var lbl = xLabels[SelectedIndex];
                var start = Math.Clamp(crosshairX - lbl.Length / 2, plotX, plotX + Math.Max(0, plotW - lbl.Length));
                DrawString(buffer, start, xLabelRow, lbl, SelectedForeground, EffectiveSelectionBackground, plotW);
            }
        }
    }

    /// <summary>Labels for the x axis, taken from the series that has the most points.</summary>
    private static List<string> XAxisLabels(List<ChartSeries> series)
    {
        var labels = new List<string>();
        var best = -1;
        foreach (var s in series)
        {
            if (s.Values.Count > best)
            {
                best = s.Values.Count;
                labels = s.Values.Select(p => p.Label ?? "").ToList();
            }
        }

        return labels;
    }

    /// <summary>
    /// Draws x-axis labels centered under their data points, thinning them so they never
    /// overlap, and marks each drawn label's column with a tick on the axis line.
    /// </summary>
    private void DrawXLabels(CellBuffer buffer, List<string> labels, int plotX, int plotW, int xLabelRow, int axisRow)
    {
        var count = labels.Count;
        if (count == 0 || plotW <= 0)
        {
            return;
        }

        var maxLen = 1;
        foreach (var l in labels)
        {
            maxLen = Math.Max(maxLen, l.Length);
        }

        // How many labels fit side by side, keeping a gap between them.
        var maxLabels = Math.Max(2, plotW / (maxLen + 2));

        var indices = new List<int>();
        if (count <= maxLabels)
        {
            for (var i = 0; i < count; i++)
            {
                indices.Add(i);
            }
        }
        else
        {
            // Evenly spaced, always including the first and last point.
            for (var k = 0; k < maxLabels; k++)
            {
                var idx = (int)Math.Round((double)k / (maxLabels - 1) * (count - 1));
                if (indices.Count == 0 || indices[^1] != idx)
                {
                    indices.Add(idx);
                }
            }
        }

        foreach (var idx in indices)
        {
            var label = labels[idx];
            if (string.IsNullOrEmpty(label))
            {
                continue;
            }

            var fx = count > 1 ? (double)idx / (count - 1) : 0.0;
            var cellX = plotX + (int)Math.Round(fx * (plotW - 1));

            if (ShowAxes && buffer.IsInBounds(cellX, axisRow))
            {
                buffer.SetChar(cellX, axisRow, '┬', AxisColor, Background);
            }

            var start = Math.Clamp(cellX - label.Length / 2, plotX, plotX + Math.Max(0, plotW - label.Length));
            DrawString(buffer, start, xLabelRow, label, Foreground, Background, plotW);
        }
    }

    private void DrawGridAndAxes(CellBuffer buffer, AxisScale scale, int axisCol, int plotX, int plotTop, int axisRow, int right, int labelX, int labelWidth)
    {
        if (ShowGrid || ShowAxes)
        {
            foreach (var tick in scale.Ticks)
            {
                var norm = scale.Normalize(tick);
                var row = axisRow - (int)Math.Round(norm * (axisRow - plotTop));
                if (row < plotTop || row > axisRow)
                {
                    continue;
                }

                if (ShowGrid && row < axisRow)
                {
                    for (var x = plotX; x < right; x++)
                    {
                        if (buffer.IsInBounds(x, row))
                        {
                            buffer.SetChar(x, row, '·', GridColor, Background);
                        }
                    }
                }

                if (ShowAxes)
                {
                    DrawString(buffer, labelX, row, AxisScale.FormatTick(tick).PadLeft(labelWidth), Foreground, Background, labelWidth);
                }
            }
        }

        if (ShowAxes)
        {
            for (var y = plotTop; y < axisRow; y++)
            {
                if (buffer.IsInBounds(axisCol, y))
                {
                    buffer.SetChar(axisCol, y, '│', AxisColor, Background);
                }
            }

            for (var x = axisCol; x < right; x++)
            {
                if (buffer.IsInBounds(x, axisRow))
                {
                    buffer.SetChar(x, axisRow, x == axisCol ? '└' : '─', AxisColor, Background);
                }
            }
        }
    }

    private static (double Min, double Max) DataRange(List<ChartSeries> series)
    {
        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;
        foreach (var s in series)
        {
            foreach (var p in s.Values)
            {
                min = Math.Min(min, p.Value);
                max = Math.Max(max, p.Value);
            }
        }

        if (!double.IsFinite(min) || !double.IsFinite(max))
        {
            return (0, 1);
        }

        return (min, max);
    }

    private void DrawLegend(CellBuffer buffer, int x, int y, int width, List<ChartSeries> series)
    {
        var cx = x;
        for (var s = 0; s < series.Count && cx < x + width; s++)
        {
            var color = ColorForSeries(s, series[s].Color);
            if (buffer.IsInBounds(cx, y))
            {
                buffer.SetChar(cx, y, '─', color, Background);
            }

            cx += 1;
            var name = string.IsNullOrEmpty(series[s].Name) ? $"Series {s + 1}" : series[s].Name;
            cx += DrawString(buffer, cx + 1, y, name, LegendColor, Background, x + width - cx - 1) + 2;
        }
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
}
