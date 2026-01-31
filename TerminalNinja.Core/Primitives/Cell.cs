using System.Runtime.InteropServices;

namespace TerminalNinja.Core.Primitives;

/// <summary>
/// Represents a single terminal cell with a character and colors (8 bytes total).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct Cell
{
    /// <summary>Gets the character displayed in this cell.</summary>
    public readonly char Character;
    
    /// <summary>Gets the foreground (text) color.</summary>
    public readonly Color Foreground;
    
    /// <summary>Gets the background color.</summary>
    public readonly Color Background;
    
    /// <summary>
    /// Creates a new cell with the specified character and colors.
    /// </summary>
    public Cell(char character, Color foreground, Color background)
    {
        Character = character;
        Foreground = foreground;
        Background = background;
    }
    
    /// <summary>Gets an empty cell (space with white on black).</summary>
    public static readonly Cell Empty = new(' ', Color.White, Color.Black);
}
