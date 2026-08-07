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

    /// <summary>
    /// Arbitrary payload for the application, ignored by the control.
    /// </summary>
    /// <remarks>
    /// The way back from a selected box to the domain object it stands for. Without it the only
    /// carrier is <see cref="Id"/>, which <see cref="GraphEdge"/> endpoints also match on — so an
    /// application ended up choosing between an id that reads well in the graph and one it can
    /// look its own object up by, and generally smuggled the latter through <see cref="Id"/>.
    /// </remarks>
    public object? Tag { get; set; }
}
