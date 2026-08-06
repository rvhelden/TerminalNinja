using System.Windows.Markup;
using TerminalNinja.Aot;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls.Charts;

/// <summary>
/// A named sequence of <see cref="ChartDataPoint"/> values rendered by
/// <see cref="BarChart"/> or <see cref="LineChart"/>. The <see cref="Values"/>
/// collection is the content property, so points can be nested directly in XAML:
/// <code>
/// &lt;ChartSeries Name="Revenue"&gt;
///     &lt;ChartDataPoint Label="Q1" Value="42" /&gt;
///     &lt;ChartDataPoint Label="Q2" Value="55" /&gt;
/// &lt;/ChartSeries&gt;
/// </code>
/// </summary>
[BindableObject]
[ContentProperty("Values")]
public sealed class ChartSeries
{
    /// <summary>Series name shown in the legend.</summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Optional series color override. When left as <see cref="Color.Transparent"/>
    /// (the default) the chart palette supplies the color by series index.
    /// </summary>
    public Color Color { get; set; } = Color.Transparent;

    /// <summary>The data points that make up this series.</summary>
    public List<ChartDataPoint> Values { get; } = [];
}
