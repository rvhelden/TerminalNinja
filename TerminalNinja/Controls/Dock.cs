using System.ComponentModel;

namespace TerminalNinja.Controls;

/// <summary>
/// Specifies the edge of a <see cref="DockPanel"/> that a child element is docked against.
/// </summary>
[TypeConverter(typeof(EnumConverter))]
public enum Dock : byte
{
    /// <summary>Child is docked against the left edge, taking the full remaining height.</summary>
    Left,

    /// <summary>Child is docked against the top edge, taking the full remaining width.</summary>
    Top,

    /// <summary>Child is docked against the right edge, taking the full remaining height.</summary>
    Right,

    /// <summary>Child is docked against the bottom edge, taking the full remaining width.</summary>
    Bottom
}
