using System.Windows.Markup;
using TerminalNinja.Aot;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls.Charts;

/// <summary>
/// A single node in a <see cref="NodeGraph"/> — a named vertex drawn as a small
/// labeled box. Referenced by <see cref="GraphEdge.From"/> / <see cref="GraphEdge.To"/>
/// through its <see cref="Id"/>.
/// </summary>
[BindableObject]
[ContentProperty("Name")]
public sealed class GraphNode
{
    /// <summary>Identity used by <see cref="GraphEdge"/> endpoints to reference this node.</summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// Display label drawn inside the node's box. Should be printable text —
    /// control characters are written to the terminal as-is.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>Optional weight. Reserved for sizing/emphasis; unused by the MVP renderer.</summary>
    public double Value { get; set; }

    /// <summary>
    /// Optional color override. When left as <see cref="Color.Transparent"/> the
    /// chart palette supplies the color.
    /// </summary>
    public Color Color { get; set; } = Color.Transparent;
}
