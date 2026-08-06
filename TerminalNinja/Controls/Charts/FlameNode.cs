using System.Windows.Markup;
using TerminalNinja.Aot;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls.Charts;

/// <summary>
/// A node in a <see cref="FlameGraph"/>. Each node represents a frame whose width is
/// proportional to its <see cref="Value"/> (e.g. samples or self+child time). Children
/// are stacked in the row beneath their parent and together should not exceed the
/// parent's value. The <see cref="Children"/> collection is the content property, so a
/// tree can be declared inline in XAML.
/// </summary>
[BindableObject]
[ContentProperty("Children")]
public sealed class FlameNode
{
    /// <summary>Frame label (function / method name).</summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// The node's total weight. When zero, <see cref="FlameGraph"/> derives it from
    /// the sum of the children.
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// Optional color override. When left as <see cref="Color.Transparent"/> the
    /// chart palette supplies the color by depth.
    /// </summary>
    public Color Color { get; set; } = Color.Transparent;

    /// <summary>Child frames stacked below this one.</summary>
    public List<FlameNode> Children { get; } = [];
}
