using TerminalNinja.Controls.Charts;

namespace TerminalNinja.Tests.Unit.Controls.Charts;

/// <summary>Tests for <see cref="LineChart"/> rendering and XAML instantiation.</summary>
public class LineChartTests
{
    private const int W = 40;
    private const int H = 15;

    private static bool ContainsBraille(CellBuffer buffer)
    {
        for (var y = 0; y < buffer.Height; y++)
        {
            for (var x = 0; x < buffer.Width; x++)
            {
                var cp = buffer.GetCell(x, y).Codepoint;
                if (cp is >= 0x2800 and <= 0x28FF)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static LineChart LineOf(params double[] values)
    {
        var series = new ChartSeries { Name = "A" };
        foreach (var v in values)
        {
            series.Values.Add(new ChartDataPoint { Value = v });
        }

        var chart = new LineChart();
        chart.Series.Add(series);
        return chart;
    }

    [Test]
    public async Task Render_WithData_DrawsBrailleLine()
    {
        using var buffer = new CellBuffer(W, H);
        var chart = LineOf(1, 5, 2, 8, 3, 9, 4);

        chart.Render(buffer, new Rect(0, 0, W, H));

        await Assert.That(ContainsBraille(buffer)).IsTrue();
    }

    [Test]
    public async Task Render_EmptyChart_ShowsNoDataMessage()
    {
        using var buffer = new CellBuffer(W, H);
        var chart = new LineChart();

        chart.Render(buffer, new Rect(0, 0, W, H));

        await Assert.That(ContainsBraille(buffer)).IsFalse();
    }

    [Test]
    public async Task Render_LineColor_MatchesSeriesColor()
    {
        using var buffer = new CellBuffer(W, H);
        var chart = LineOf(1, 4, 2, 6, 3);
        chart.Series[0].Color = new Color(10, 220, 30);
        chart.ShowAxes = false;

        chart.Render(buffer, new Rect(0, 0, W, H));

        var sawColor = false;
        for (var y = 0; y < H; y++)
        {
            for (var x = 0; x < W; x++)
            {
                var cell = buffer.GetCell(x, y);
                if (cell.Codepoint is >= 0x2800 and <= 0x28FF && cell.Foreground == new Color(10, 220, 30))
                {
                    sawColor = true;
                }
            }
        }

        await Assert.That(sawColor).IsTrue();
    }

    private static bool ContainsChar(CellBuffer buffer, char c)
    {
        for (var y = 0; y < buffer.Height; y++)
        {
            for (var x = 0; x < buffer.Width; x++)
            {
                if (buffer.GetCell(x, y).Codepoint == c)
                {
                    return true;
                }
            }
        }

        return false;
    }

    [Test]
    public async Task Render_WithPointLabels_DrawsXAxisLabelsAndTicks()
    {
        using var buffer = new CellBuffer(W, H);
        var series = new ChartSeries { Name = "s" };
        foreach (var (label, value) in new[] { ("Mon", 12.0), ("Tue", 18.0), ("Wed", 9.0), ("Thu", 22.0) })
        {
            series.Values.Add(new ChartDataPoint { Label = label, Value = value });
        }

        var chart = new LineChart();
        chart.Series.Add(series);
        chart.Render(buffer, new Rect(0, 0, W, H));

        await Assert.That(ContainsChar(buffer, '┬')).IsTrue();       // axis tick under a labeled point
        await Assert.That(ContainsChar(buffer, 'M')).IsTrue();       // "Mon"
    }

    [Test]
    public async Task Render_ShowXLabelsFalse_OmitsLabels()
    {
        using var buffer = new CellBuffer(W, H);
        var series = new ChartSeries { Name = "s" };
        series.Values.Add(new ChartDataPoint { Label = "Mon", Value = 12 });
        series.Values.Add(new ChartDataPoint { Label = "Tue", Value = 18 });

        var chart = new LineChart { ShowXLabels = false };
        chart.Series.Add(series);
        chart.Render(buffer, new Rect(0, 0, W, H));

        await Assert.That(ContainsChar(buffer, '┬')).IsFalse();
    }

    private static KeyEvent Key(ConsoleKey key) => new(key, '\0', false, false, false);

    [Test]
    public async Task IsFocusable()
    {
        await Assert.That(new LineChart().Focusable).IsTrue();
    }

    [Test]
    public async Task RightArrow_MovesPointSelection()
    {
        var chart = LineOf(1, 5, 2, 8, 3);

        chart.OnKeyEvent(Key(ConsoleKey.RightArrow));
        await Assert.That(chart.SelectedIndex).IsEqualTo(0);
        chart.OnKeyEvent(Key(ConsoleKey.RightArrow));
        await Assert.That(chart.SelectedIndex).IsEqualTo(1);
        chart.OnKeyEvent(Key(ConsoleKey.End));
        await Assert.That(chart.SelectedIndex).IsEqualTo(4);
    }

    [Test]
    public async Task Click_SelectsNearestPoint()
    {
        var chart = LineOf(1, 5, 2, 8, 3); // 5 points
        chart.ShowAxes = false;            // plotX = 0, plotW = 40

        using var buffer = new CellBuffer(40, H);
        chart.Render(buffer, new Rect(0, 0, 40, H));

        chart.OnMouseEvent(new MouseEvent(39, 5, MouseButton.Left, MouseAction.Press)); // far right → last
        await Assert.That(chart.SelectedIndex).IsEqualTo(4);

        chart.OnMouseEvent(new MouseEvent(0, 5, MouseButton.Left, MouseAction.Press)); // far left → first
        await Assert.That(chart.SelectedIndex).IsEqualTo(0);
    }

    [Test]
    public async Task SelectedPoint_DrawsCrosshairWhenFocused()
    {
        var chart = LineOf(1, 5, 2, 8, 3);
        chart.IsFocused = true;
        chart.SelectedIndex = 2;

        using var buffer = new CellBuffer(40, H);
        chart.Render(buffer, new Rect(0, 0, 40, H));

        var crosshair = false;
        for (var y = 0; y < H; y++)
        {
            for (var x = 0; x < 40; x++)
            {
                if (buffer.GetCell(x, y).Background == new Color(38, 79, 120))
                {
                    crosshair = true;
                }
            }
        }

        await Assert.That(crosshair).IsTrue();
    }

    [Test]
    public async Task Xaml_WithInlineSeries_PopulatesData()
    {
        const string xaml = """
            <LineChart xmlns="http://schemas.terminalninja.dev/xaml" ShowMarkers="True">
                <ChartSeries Name="Latency">
                    <ChartDataPoint Value="10" />
                    <ChartDataPoint Value="30" />
                    <ChartDataPoint Value="20" />
                </ChartSeries>
            </LineChart>
            """;

        var chart = TerminalXaml.Load<LineChart>(xaml);

        await Assert.That(chart.ShowMarkers).IsTrue();
        await Assert.That(chart.Series.Count).IsEqualTo(1);
        await Assert.That(chart.Series[0].Values.Count).IsEqualTo(3);
    }
}
