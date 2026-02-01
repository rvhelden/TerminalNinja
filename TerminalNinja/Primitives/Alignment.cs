using System.ComponentModel;

namespace TerminalNinja.Primitives;

/// <summary>
/// Specifies how an control is aligned within its parent container.
/// </summary>
[TypeConverter(typeof(EnumConverter))]
public enum Alignment : byte
{
    /// <summary>Align to the start (left/top).</summary>
    Start,
    
    /// <summary>Align to the center.</summary>
    Center,
    
    /// <summary>Align to the end (right/bottom).</summary>
    End
}
