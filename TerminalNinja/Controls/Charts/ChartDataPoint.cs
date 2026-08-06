using TerminalNinja.Aot;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls.Charts;

/// <summary>
/// A single value in a <see cref="ChartSeries"/>. Used by <see cref="BarChart"/> and
/// <see cref="LineChart"/>. Declarable inline in XAML, e.g.
/// <c>&lt;ChartDataPoint Label="Q1" Value="42" /&gt;</c>.
/// </summary>
[BindableObject]
public sealed class ChartDataPoint
{
    /// <summary>Category label shown on the axis (bar charts) or tooltip.</summary>
    public string Label { get; set; } = "";

    /// <summary>The numeric value of this point.</summary>
    public double Value { get; set; }

    /// <summary>
    /// Optional per-point color override. When left as <see cref="Color.Transparent"/>
    /// (the default) the owning series or the chart palette supplies the color.
    /// </summary>
    public Color Color { get; set; } = Color.Transparent;
}
