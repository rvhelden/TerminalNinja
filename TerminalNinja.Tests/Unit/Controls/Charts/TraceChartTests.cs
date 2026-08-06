using TerminalNinja.Controls.Charts;

namespace TerminalNinja.Tests.Unit.Controls.Charts;

/// <summary>Tests for <see cref="TraceChart"/> rendering and XAML instantiation.</summary>
public class TraceChartTests
{
    private const int W = 60;
    private const int H = 12;

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
    public async Task Render_WithSpans_DrawsDurationBars()
    {
        using var buffer = new CellBuffer(W, H);
        var chart = new TraceChart();
        chart.Spans.Add(new TraceSpan { Name = "root", StartMs = 0, DurationMs = 100 });
        chart.Spans.Add(new TraceSpan { Name = "db", StartMs = 20, DurationMs = 40, Depth = 1 });

        chart.Render(buffer, new Rect(0, 0, W, H));

        await Assert.That(ContainsChar(buffer, '█')).IsTrue();
    }

    [Test]
    public async Task Render_LaterSpan_StartsFurtherRight()
    {
        using var buffer = new CellBuffer(W, H);
        var chart = new TraceChart { ShowAxes = false, Title = "" };
        chart.Spans.Add(new TraceSpan { Name = "early", StartMs = 0, DurationMs = 10 });
        chart.Spans.Add(new TraceSpan { Name = "late", StartMs = 90, DurationMs = 10 });

        chart.Render(buffer, new Rect(0, 0, W, H));

        int FirstBlockX(int row)
        {
            for (var x = 0; x < W; x++)
            {
                if (buffer.GetCell(x, row).Codepoint == '█')
                {
                    return x;
                }
            }

            return int.MaxValue;
        }

        // Row 0 = "early" span, row 1 = "late" span.
        await Assert.That(FirstBlockX(1)).IsGreaterThan(FirstBlockX(0));
    }

    [Test]
    public async Task Render_Empty_ShowsNoSpansMessage()
    {
        using var buffer = new CellBuffer(W, H);
        var chart = new TraceChart();

        chart.Render(buffer, new Rect(0, 0, W, H));

        await Assert.That(ContainsChar(buffer, '█')).IsFalse();
    }

    [Test]
    public async Task Xaml_WithNestedSpans_FlattensChildren()
    {
        const string xaml = """
            <TraceChart xmlns="http://schemas.terminalninja.dev/xaml">
                <TraceSpan Name="request" StartMs="0" DurationMs="120">
                    <TraceSpan Name="auth" StartMs="5" DurationMs="20" />
                    <TraceSpan Name="query" StartMs="30" DurationMs="70" />
                </TraceSpan>
            </TraceChart>
            """;

        var chart = TerminalXaml.Load<TraceChart>(xaml);

        await Assert.That(chart.Spans.Count).IsEqualTo(1);
        await Assert.That(chart.Spans[0].Children.Count).IsEqualTo(2);
        await Assert.That(chart.Spans[0].Children[1].Name).IsEqualTo("query");
    }
}
