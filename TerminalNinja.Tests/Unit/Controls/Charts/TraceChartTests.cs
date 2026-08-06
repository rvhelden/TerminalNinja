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

    private static TraceChart ThreeSpans()
    {
        var chart = new TraceChart();
        chart.Spans.Add(new TraceSpan { Name = "a", StartMs = 0, DurationMs = 10 });
        chart.Spans.Add(new TraceSpan { Name = "b", StartMs = 10, DurationMs = 20 });
        chart.Spans.Add(new TraceSpan { Name = "c", StartMs = 30, DurationMs = 15 });
        return chart;
    }

    private static KeyEvent Key(ConsoleKey key) => new(key, '\0', false, false, false);

    [Test]
    public async Task IsFocusable()
    {
        await Assert.That(new TraceChart().Focusable).IsTrue();
    }

    [Test]
    public async Task DownArrow_MovesSelectionAndSyncsSelectedSpan()
    {
        var chart = ThreeSpans();
        using var buffer = new CellBuffer(W, H);
        chart.Render(buffer, new Rect(0, 0, W, H)); // captures row layout

        chart.OnKeyEvent(Key(ConsoleKey.DownArrow));
        await Assert.That(chart.SelectedIndex).IsEqualTo(0);
        await Assert.That(chart.SelectedSpan).IsSameReferenceAs(chart.Spans[0]);

        chart.OnKeyEvent(Key(ConsoleKey.DownArrow));
        await Assert.That(chart.SelectedIndex).IsEqualTo(1);
        await Assert.That(chart.SelectedSpan).IsSameReferenceAs(chart.Spans[1]);
    }

    [Test]
    public async Task UpArrow_StopsAtFirstRow_EndGoesToLast()
    {
        var chart = ThreeSpans();
        using var buffer = new CellBuffer(W, H);
        chart.Render(buffer, new Rect(0, 0, W, H));

        chart.OnKeyEvent(Key(ConsoleKey.End));
        await Assert.That(chart.SelectedIndex).IsEqualTo(2);

        chart.OnKeyEvent(Key(ConsoleKey.UpArrow));
        chart.OnKeyEvent(Key(ConsoleKey.UpArrow));
        chart.OnKeyEvent(Key(ConsoleKey.UpArrow)); // clamps at 0
        await Assert.That(chart.SelectedIndex).IsEqualTo(0);
    }

    [Test]
    public async Task SettingSelectedSpan_SyncsSelectedIndex()
    {
        var chart = ThreeSpans();

        chart.SelectedSpan = chart.Spans[2];
        await Assert.That(chart.SelectedIndex).IsEqualTo(2);
    }

    [Test]
    public async Task LeftClick_SelectsClickedRow()
    {
        var chart = ThreeSpans();
        using var buffer = new CellBuffer(W, H);
        chart.Render(buffer, new Rect(0, 0, W, H));

        // Header row occupies y=0; data rows start at y=1. Click the 3rd data row (index 2).
        chart.OnMouseEvent(new MouseEvent(3, 3, MouseButton.Left, MouseAction.Press));
        await Assert.That(chart.SelectedIndex).IsEqualTo(2);
        await Assert.That(chart.SelectedSpan).IsSameReferenceAs(chart.Spans[2]);
    }

    [Test]
    public async Task SelectedRow_IsHighlighted()
    {
        var chart = ThreeSpans();
        chart.IsFocused = true;
        chart.SelectedIndex = 1;

        using var buffer = new CellBuffer(W, H);
        chart.Render(buffer, new Rect(0, 0, W, H));

        // The selected row (index 1, at y=2 after the header) is painted with SelectedBackground.
        var highlighted = false;
        for (var x = 0; x < W; x++)
        {
            if (buffer.GetCell(x, 2).Background == new Color(38, 79, 120))
            {
                highlighted = true;
            }
        }

        await Assert.That(highlighted).IsTrue();
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
