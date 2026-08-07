using TerminalNinja.Aot;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls.Charts;

/// <summary>
/// A connection between two <see cref="GraphNode"/>s in a <see cref="NodeGraph"/>,
/// referencing its endpoints by their <see cref="GraphNode.Id"/>. Edges whose
/// <see cref="From"/> or <see cref="To"/> does not match any node are ignored.
/// </summary>
[BindableObject]
public sealed class GraphEdge
{
    /// <summary>The <see cref="GraphNode.Id"/> of the source node.</summary>
    public string From { get; set; } = "";

    /// <summary>The <see cref="GraphNode.Id"/> of the target node.</summary>
    public string To { get; set; } = "";

    /// <summary>
    /// Optional color override for the edge line. When left as
    /// <see cref="Color.Transparent"/> the chart's axis color is used.
    /// </summary>
    public Color Color { get; set; } = Color.Transparent;

    /// <summary>
    /// Optional short text drawn at the middle of the edge line, in the edge's color.
    /// </summary>
    /// <remarks>
    /// Keep it to a few characters — a rate, a count, a protocol. The label is drawn over the
    /// line but under the node boxes, so a box always wins the cells it occupies, and a label
    /// that would not fit between them is simply not drawn rather than overlapping one.
    /// </remarks>
    public string Label { get; set; } = "";
}
