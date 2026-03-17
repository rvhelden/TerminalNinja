using System.Runtime.InteropServices;

namespace TerminalNinja.Primitives;

/// <summary>
/// Represents a single terminal cell with a character, colors, and text decorations.
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
    
    /// <summary>Gets the text decorations (bold, italic, underline, etc.).</summary>
    public readonly TextDecorations Decorations;
    
    /// <summary>
    /// Creates a new cell with the specified character and colors.
    /// </summary>
    public Cell(char character, Color foreground, Color background)
    {
        Character = character;
        Foreground = foreground;
        Background = background;
        Decorations = TextDecorations.None;
    }
    
    /// <summary>
    /// Creates a new cell with the specified character, colors, and text decorations.
    /// </summary>
    public Cell(char character, Color foreground, Color background, TextDecorations decorations)
    {
        Character = character;
        Foreground = foreground;
        Background = background;
        Decorations = decorations;
    }
    
    /// <summary>Gets an empty cell (space with white on black).</summary>
    public static readonly Cell Empty = new(' ', Color.White, Color.Black);
}
