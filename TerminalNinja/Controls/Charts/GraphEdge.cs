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
}
