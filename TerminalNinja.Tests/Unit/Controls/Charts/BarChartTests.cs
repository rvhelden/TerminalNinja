using TerminalNinja.Controls.Charts;

namespace TerminalNinja.Tests.Unit.Controls.Charts;

/// <summary>Tests for <see cref="BarChart"/> rendering and XAML instantiation.</summary>
public class BarChartTests
{
    private const int W = 40;
    private const int H = 15;

    private static BarChart SingleSeries(params double[] values)
    {
        var series = new ChartSeries { Name = "A" };
        foreach (var v in values)
        {
            series.Values.Add(new ChartDataPoint { Label = "c", Value = v });
        }

        var chart = new BarChart();
        chart.Series.Add(series);
        return chart;
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
    public async Task Render_WithData_DrawsFullBlocks()
    {
        using var buffer = new CellBuffer(W, H);
        var chart = SingleSeries(10, 20, 30);

        chart.Render(buffer, new Rect(0, 0, W, H));

        await Assert.That(ContainsChar(buffer, '█')).IsTrue();
    }

    [Test]
    public async Task Render_EmptyChart_ShowsNoDataMessage()
    {
        using var buffer = new CellBuffer(W, H);
        var chart = new BarChart();

        chart.Render(buffer, new Rect(0, 0, W, H));

        // The literal 'n' of "(no data)" should appear somewhere.
        await Assert.That(ContainsChar(buffer, 'n')).IsTrue();
        await Assert.That(ContainsChar(buffer, '█')).IsFalse();
    }

    [Test]
    public async Task Render_TallerBar_UsesMoreRowsThanShorterBar()
    {
        using var buffer = new CellBuffer(W, H);
        var chart = SingleSeries(1, 100);
        chart.ShowAxes = false;
        chart.ShowLegend = false;

        chart.Render(buffer, new Rect(0, 0, W, H));

        // Count block rows in the left half (first category) vs right half (second).
        int BlockRows(int xFrom, int xTo)
        {
            var rows = 0;
            for (var y = 0; y < H; y++)
            {
                for (var x = xFrom; x < xTo; x++)
                {
                    if (buffer.GetCell(x, y).Codepoint == '█')
                    {
                        rows++;
                        break;
                    }
                }
            }

            return rows;
        }

        await Assert.That(BlockRows(W / 2, W)).IsGreaterThan(BlockRows(0, W / 2));
    }

    [Test]
    public async Task Render_Stacked_DrawsMultipleSeriesColors()
    {
        using var buffer = new CellBuffer(W, H);
        var chart = new BarChart { BarMode = BarMode.Stacked, ShowAxes = false, ShowLegend = false };
        var s1 = new ChartSeries { Color = new Color(200, 0, 0) };
        var s2 = new ChartSeries { Color = new Color(0, 0, 200) };
        s1.Values.Add(new ChartDataPoint { Value = 30 });
        s2.Values.Add(new ChartDataPoint { Value = 30 });
        chart.Series.Add(s1);
        chart.Series.Add(s2);

        chart.Render(buffer, new Rect(0, 0, W, H));

        var sawRed = false;
        var sawBlue = false;
        for (var y = 0; y < H; y++)
        {
            for (var x = 0; x < W; x++)
            {
                var fg = buffer.GetCell(x, y).Foreground;
                if (fg == new Color(200, 0, 0)) sawRed = true;
                if (fg == new Color(0, 0, 200)) sawBlue = true;
            }
        }

        await Assert.That(sawRed).IsTrue();
        await Assert.That(sawBlue).IsTrue();
    }

    private static BarChart LabeledSeries()
    {
        var series = new ChartSeries { Name = "s" };
        foreach (var (label, value) in new[] { ("Q1", 42.0), ("Q2", 55.0), ("Q3", 30.0), ("Q4", 70.0) })
        {
            series.Values.Add(new ChartDataPoint { Label = label, Value = value });
        }

        var chart = new BarChart();
        chart.Series.Add(series);
        return chart;
    }

    private static KeyEvent Key(ConsoleKey key) => new(key, '\0', false, false, false);

    [Test]
    public async Task IsFocusable()
    {
        await Assert.That(new BarChart().Focusable).IsTrue();
    }

    [Test]
    public async Task RightArrow_MovesCategory_AndSyncsSelectedItemLabel()
    {
        var chart = LabeledSeries();

        chart.OnKeyEvent(Key(ConsoleKey.RightArrow));
        await Assert.That(chart.SelectedIndex).IsEqualTo(0);
        await Assert.That(chart.SelectedItem).IsEqualTo("Q1");

        chart.OnKeyEvent(Key(ConsoleKey.RightArrow));
        await Assert.That(chart.SelectedIndex).IsEqualTo(1);
        await Assert.That(chart.SelectedItem).IsEqualTo("Q2");

        chart.OnKeyEvent(Key(ConsoleKey.End));
        await Assert.That(chart.SelectedIndex).IsEqualTo(3);
        await Assert.That(chart.SelectedItem).IsEqualTo("Q4");
    }

    [Test]
    public async Task Click_SelectsCategory()
    {
        var chart = LabeledSeries();
        chart.ShowAxes = false; // plotX = 0 so slots are predictable
        chart.ShowLegend = false;

        using var buffer = new CellBuffer(40, H);
        chart.Render(buffer, new Rect(0, 0, 40, H)); // 4 categories → slotW = 10

        chart.OnMouseEvent(new MouseEvent(25, 5, MouseButton.Left, MouseAction.Press)); // 25/10 = 2
        await Assert.That(chart.SelectedIndex).IsEqualTo(2);
    }

    [Test]
    public async Task SelectedCategory_IsHighlightedWhenFocused()
    {
        var chart = LabeledSeries();
        chart.IsFocused = true;
        chart.SelectedIndex = 1;

        using var buffer = new CellBuffer(40, H);
        chart.Render(buffer, new Rect(0, 0, 40, H));

        var highlighted = false;
        for (var y = 0; y < H; y++)
        {
            for (var x = 0; x < 40; x++)
            {
                if (buffer.GetCell(x, y).Background == new Color(38, 79, 120))
                {
                    highlighted = true;
                }
            }
        }

        await Assert.That(highlighted).IsTrue();
    }

    [Test]
    public async Task Xaml_WithInlineSeries_PopulatesData()
    {
        const string xaml = """
            <BarChart xmlns="http://schemas.terminalninja.dev/xaml">
                <ChartSeries Name="Revenue">
                    <ChartDataPoint Label="Q1" Value="42" />
                    <ChartDataPoint Label="Q2" Value="55" />
                </ChartSeries>
            </BarChart>
            """;

        var chart = TerminalXaml.Load<BarChart>(xaml);

        await Assert.That(chart.Series.Count).IsEqualTo(1);
        await Assert.That(chart.Series[0].Name).IsEqualTo("Revenue");
        await Assert.That(chart.Series[0].Values.Count).IsEqualTo(2);
        await Assert.That(chart.Series[0].Values[1].Value).IsEqualTo(55.0);
    }
}
