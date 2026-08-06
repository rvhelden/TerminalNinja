using System.Windows.Markup;
using TerminalNinja.Aot;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls.Charts;

/// <summary>
/// A single span in a <see cref="TraceChart"/> waterfall — a named operation that
/// starts at <see cref="StartMs"/> and lasts <see cref="DurationMs"/> milliseconds,
/// nested at <see cref="Depth"/> levels of indentation. Modeled on the spans of a
/// distributed trace (see Grafana's traces panel).
/// </summary>
[BindableObject]
[ContentProperty("Children")]
public sealed class TraceSpan
{
    /// <summary>Operation / service name shown at the start of the row.</summary>
    public string Name { get; set; } = "";

    /// <summary>Start offset from the trace origin, in milliseconds.</summary>
    public double StartMs { get; set; }

    /// <summary>Duration of the span, in milliseconds.</summary>
    public double DurationMs { get; set; }

    /// <summary>
    /// Nesting depth (0 = root). Used to indent the label and, when children are
    /// supplied instead, computed automatically by <see cref="TraceChart"/>.
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// Optional color override. When left as <see cref="Color.Transparent"/> the
    /// chart palette supplies the color.
    /// </summary>
    public Color Color { get; set; } = Color.Transparent;

    /// <summary>
    /// Optional nested child spans. When present, <see cref="TraceChart"/> flattens
    /// the tree into rows and assigns <see cref="Depth"/> from the nesting.
    /// </summary>
    public List<TraceSpan> Children { get; } = [];
}
