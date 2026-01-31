namespace TerminalNinja.Core.Primitives;

/// <summary>
/// Specifies how text wraps when it exceeds the available width.
/// </summary>
public enum TextWrapping : byte
{
    /// <summary>Text does not wrap; it may be truncated or clipped at the edge.</summary>
    NoWrap,
    
    /// <summary>Text wraps to the next line when it reaches the container edge.</summary>
    Wrap
}
