namespace TerminalNinja.Core.Primitives;

/// <summary>
/// Specifies how text is trimmed when it exceeds the available space.
/// </summary>
public enum TextTrimming : byte
{
    /// <summary>Text is not trimmed; it is clipped at the container boundary.</summary>
    None,
    
    /// <summary>Text is trimmed and an ellipsis (...) is added to indicate truncation.</summary>
    Ellipsis
}
