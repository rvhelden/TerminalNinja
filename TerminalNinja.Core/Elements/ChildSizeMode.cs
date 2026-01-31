namespace TerminalNinja.Core.Elements;

/// <summary>
/// Specifies how a child element's size is determined within a Stack container.
/// </summary>
public enum ChildSizeMode : byte
{
    /// <summary>Child uses a fixed size in cells.</summary>
    Fixed,
    
    /// <summary>Child fills remaining space after Fixed children (divided equally among all Stretch children).</summary>
    Stretch,
    
    /// <summary>Child uses its preferred size (intrinsic content size).</summary>
    Auto
}
