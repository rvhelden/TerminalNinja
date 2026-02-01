namespace TerminalNinja.Input;

/// <summary>
/// Represents a mouse action or event type.
/// </summary>
public enum MouseAction
{
    /// <summary>Mouse button pressed.</summary>
    Press = 0,
    
    /// <summary>Mouse button released.</summary>
    Release = 1,
    
    /// <summary>Mouse moved (with or without button held).</summary>
    Move = 2,
    
    /// <summary>Mouse wheel scrolled up.</summary>
    ScrollUp = 3,
    
    /// <summary>Mouse wheel scrolled down.</summary>
    ScrollDown = 4
}
