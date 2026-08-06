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
