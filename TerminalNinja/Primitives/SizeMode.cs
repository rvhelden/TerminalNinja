namespace TerminalNinja.Primitives;

/// <summary>
/// Specifies how a size value should be interpreted.
/// </summary>
public enum SizeMode : byte
{
    /// <summary>Size is specified in absolute units (cells/characters).</summary>
    Absolute,
    
    /// <summary>Size is specified as a fraction of the parent size (0.0 to 1.0).</summary>
    Relative,
    
    /// <summary>Size should fill all available space in the parent.</summary>
    Stretch,
    
    /// <summary>Size is determined automatically by the control's content.</summary>
    Auto
}
